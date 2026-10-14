using System.Text.Json;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Đặt vé (/Bookings/Create) và thanh toán (/Bookings/{id}/Pay). Phải đăng nhập.</summary>
[Authorize(Roles = AppRoles.Customer + "," + AppRoles.Seller + "," + AppRoles.Admin)]
public class BookingsController : Controller
{
    private readonly ApiClient _api;
    private readonly IConfiguration _cfg;
    private readonly ILogger<BookingsController> _logger;
    private readonly BankQrOptions _bank;

    public BookingsController(ApiClient api, IConfiguration cfg, ILogger<BookingsController> logger,
        Microsoft.Extensions.Options.IOptions<BankQrOptions> bank)
    {
        _api = api;
        _cfg = cfg;
        _logger = logger;
        _bank = bank.Value;
    }

    private bool IsStaff => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Seller);

    // =====================================================================
    // #20 Đặt vé
    // =====================================================================

    [HttpGet("Bookings/Create")]
    public async Task<IActionResult> Create(int tripId, List<string> seats)
    {
        var (vm, redirect) = await BuildCreateAsync(tripId, seats, null);
        if (redirect != null) return redirect;
        return View(vm);
    }

    [HttpPost("Bookings/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBookingForm form)
    {
        var seats = form.Passengers.Select(p => p.SeatCode).ToList();
        var (vm, redirect) = await BuildCreateAsync(form.TripId, seats, form);
        if (redirect != null) return redirect;

        if (form.Passengers.Any(p => string.IsNullOrWhiteSpace(p.Name)))
        {
            ModelState.AddModelError("", "Vui lòng nhập họ tên cho từng hành khách.");
            return View(vm);
        }
        if (form.Passengers.Any(p => p.Name!.Trim().Length > 100))
        {
            ModelState.AddModelError("", "Họ tên hành khách tối đa 100 ký tự.");
            return View(vm);
        }

        // Trung chuyển và số điện thoại liên hệ
        var transferAddress = form.TransferAddress?.Trim();
        if (form.NeedTransfer && string.IsNullOrWhiteSpace(transferAddress))
        {
            ModelState.AddModelError("", "Bạn đã chọn trung chuyển, vui lòng nhập địa chỉ trung chuyển.");
            return View(vm);
        }
        if (transferAddress != null && transferAddress.Length > 300)
        {
            ModelState.AddModelError("", "Địa chỉ trung chuyển tối đa 300 ký tự.");
            return View(vm);
        }
        if (!form.NeedTransfer) transferAddress = null;

        // Bán tại quầy dùng SĐT của khách; khách tự đặt nhập SĐT liên hệ
        var contactPhone = (IsStaff ? form.CustomerPhone : form.ContactPhone)?.Trim();
        if (string.IsNullOrWhiteSpace(contactPhone) || !System.Text.RegularExpressions.Regex.IsMatch(contactPhone, @"^0\d{9}$"))
        {
            ModelState.AddModelError("", "Vui lòng nhập số điện thoại liên hệ gồm 10 số, bắt đầu bằng 0.");
            return View(vm);
        }

        // Bán vé tại quầy: xác định khách theo họ tên và số điện thoại
        var customerId = 0;
        if (IsStaff)
        {
            var (id, error) = await ResolveCustomerAsync(form.CustomerName, form.CustomerPhone);
            if (error != null)
            {
                ModelState.AddModelError("", error);
                return View(vm);
            }
            customerId = id;
        }

        var body = new
        {
            tripId = form.TripId,
            customerId,   // khách thường: API tự lấy theo tài khoản đăng nhập
            seats = form.Passengers.Select(p => new { seatCode = p.SeatCode, passengerName = p.Name!.Trim() }).ToList(),
            promoCode = string.IsNullOrWhiteSpace(form.PromoCode) ? null : form.PromoCode.Trim(),
            contactPhone,
            needTransfer = form.NeedTransfer,
            transferAddress
        };

        var res = await _api.PostAsync<BookingDto>("api/bookings", body);
        if (!res.Ok || res.Data == null)
        {
            // 409: ghế vừa bị người khác giữ, quay lại chọn ghế
            if (res.Status == 409)
            {
                TempData["Error"] = res.Error ?? "Ghế vừa có người khác giữ, vui lòng chọn ghế khác.";
                return RedirectToAction("Seats", "Trips", new { id = form.TripId, tickets = seats.Count });
            }
            ModelState.AddModelError("", res.Error ?? "Không đặt được vé. Vui lòng thử lại.");
            return View(vm);
        }

        _logger.LogInformation("{User} đặt vé {Code}", User.Identity?.Name, res.Data.Code);
        return RedirectToAction(nameof(Pay), new { id = res.Data.Id });
    }

    // Kiểm tra nhanh mã giảm giá (trang đặt vé gọi bằng JavaScript)
    [HttpGet("Bookings/CheckPromo")]
    public async Task<IActionResult> CheckPromo(string? code, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { ok = false, message = "Vui lòng nhập mã giảm giá.", discount = 0m, total = amount });

        var r = await _api.PostAsync<PromoCheckDto>("api/promotions/validate", new { code = code.Trim(), amount });
        if (r.Ok && r.Data != null)
            return Json(new { ok = true, message = "Áp dụng mã thành công.", discount = r.Data.Discount, total = r.Data.Total });

        return Json(new { ok = false, message = r.Error ?? "Mã giảm giá không hợp lệ.", discount = 0m, total = amount });
    }

    // Dựng dữ liệu trang đặt vé; trả về redirect nếu chuyến hoặc ghế không hợp lệ
    private async Task<(CreateBookingViewModel? Vm, IActionResult? Redirect)> BuildCreateAsync(
        int tripId, List<string> seats, CreateBookingForm? form)
    {
        var codes = seats.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();

        if (codes.Count == 0 || codes.Count > 10)
        {
            TempData["Error"] = "Vui lòng chọn từ 1 đến 10 ghế.";
            return (null, RedirectToAction("Seats", "Trips", new { id = tripId }));
        }

        var r = await _api.GetAsync<SeatMapDto>($"api/trips/{tripId}/seats");
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không tải được thông tin chuyến.";
            return (null, RedirectToAction("Search", "Trips"));
        }

        var map = r.Data;
        var unavailable = codes.Where(c =>
            !map.Seats.Any(s => s.IsAvailable && s.SeatCode.Equals(c, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unavailable.Count > 0)
        {
            TempData["Error"] = $"Ghế {string.Join(", ", unavailable)} không còn trống. Vui lòng chọn lại.";
            return (null, RedirectToAction("Seats", "Trips", new { id = tripId, tickets = codes.Count }));
        }

        form ??= new CreateBookingForm
        {
            TripId = tripId,
            Passengers = codes.Select(c => new PassengerInput { SeatCode = c }).ToList()
        };
        return (new CreateBookingViewModel { Map = map, Form = form, IsStaff = IsStaff }, null);
    }

    // Bán vé tại quầy: tìm khách theo số điện thoại, chưa có thì tạo mới
    private async Task<(int Id, string? Error)> ResolveCustomerAsync(string? name, string? phone)
    {
        name = name?.Trim();
        phone = phone?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
            return (0, "Bán vé tại quầy: vui lòng nhập họ tên và số điện thoại khách.");

        var found = await _api.GetAsync<CustomerListResult>(
            $"api/customers?keyword={Uri.EscapeDataString(phone)}&pageSize=20");
        if (!found.Ok) return (0, found.Error ?? "Không tra được khách hàng.");

        var exact = found.Data?.Items.FirstOrDefault(c => c.Phone == phone);
        if (exact != null) return (exact.Id, null);

        var created = await _api.PostAsync<CustomerItem>("api/customers", new { fullName = name, phone });
        if (!created.Ok || created.Data == null) return (0, created.Error ?? "Không tạo được khách hàng.");
        return (created.Data.Id, null);
    }

    // =====================================================================
    // #21 Thanh toán
    // =====================================================================

    [HttpGet("Bookings/{id:int}/Pay")]
    public async Task<IActionResult> Pay(int id)
    {
        var b = await _api.GetAsync<BookingDto>($"api/bookings/{id}");
        if (!b.Ok || b.Data == null)
        {
            TempData["Error"] = b.Error ?? "Không tìm thấy đặt vé.";
            return RedirectToAction("Search", "Trips");
        }
        var bk = b.Data;

        if (bk.Status == "Paid") return RedirectToAction(nameof(Result), new { id });
        if (bk.Status != "Holding")
        {
            TempData["Error"] = "Đặt vé này không còn ở trạng thái chờ thanh toán.";
            return RedirectToAction("Search", "Trips");
        }

        // Đã gửi thanh toán (chờ xác nhận chuyển khoản) thì xem kết quả, không thanh toán lần nữa
        var pays = await _api.GetAsync<List<PaymentDto>>($"api/payments/booking/{id}");
        if (pays.Ok && pays.Data != null && pays.Data.Any(p => p.Status == "Pending" || p.Status == "Paid"))
            return RedirectToAction(nameof(Result), new { id });

        var remaining = (int)Math.Max(0, ((bk.HoldExpiresAt ?? DateTime.Now) - DateTime.Now).TotalSeconds);
        if (remaining == 0)
        {
            TempData["Error"] = "Đã hết thời gian giữ chỗ. Vui lòng chọn lại ghế.";
            var tripId = bk.Tickets.FirstOrDefault()?.TripId ?? 0;
            return tripId > 0
                ? RedirectToAction("Seats", "Trips", new { id = tripId, tickets = Math.Max(1, bk.Tickets.Count) })
                : RedirectToAction("Search", "Trips");
        }

        return View(new PayViewModel
        {
            Booking = bk,
            RemainingSeconds = remaining,
            IsStaff = IsStaff,
            BankInfo = _cfg["Payment:BankInfo"],
            QrUrl = _bank.IsConfigured ? _bank.BuildImageUrl(bk.TotalAmount, bk.Code) : null,
            BankId = _bank.BankId,
            AccountNo = _bank.AccountNo,
            AccountName = _bank.AccountName
        });
    }

    [HttpPost("Bookings/{id:int}/Pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, string method)
    {
        if (method != "Cash" && method != "BankTransfer")
        {
            TempData["Error"] = "Vui lòng chọn phương thức thanh toán.";
            return RedirectToAction(nameof(Pay), new { id });
        }
        if (method == "Cash" && !IsStaff)
        {
            TempData["Error"] = "Tiền mặt chỉ thu tại quầy, vui lòng chọn chuyển khoản.";
            return RedirectToAction(nameof(Pay), new { id });
        }

        // Số tiền do API lấy từ đặt vé, Web không gửi. Chỉ cần biết thành công hay không.
        var r = await _api.PostAsync<JsonElement>("api/payments", new { bookingId = id, method });
        if (!r.Ok)
        {
            TempData["Error"] = r.Error ?? "Không thanh toán được. Vui lòng thử lại.";
            return RedirectToAction(nameof(Pay), new { id });
        }

        _logger.LogInformation("{User} thanh toán đặt vé {Id} bằng {Method}", User.Identity?.Name, id, method);
        return RedirectToAction(nameof(Result), new { id });
    }

    // Trang kết quả gọi mỗi vài giây để biết tiền đã về chưa (webhook ngân hàng tự xác nhận)
    [HttpGet("Bookings/{id:int}/Status")]
    public async Task<IActionResult> Status(int id)
    {
        var b = await _api.GetAsync<BookingDto>($"api/bookings/{id}");
        if (!b.Ok || b.Data == null) return NotFound();
        return Json(new { status = b.Data.Status });
    }

    [HttpGet("Bookings/{id:int}/Result")]
    public async Task<IActionResult> Result(int id)
    {
        var b = await _api.GetAsync<BookingDto>($"api/bookings/{id}");
        if (!b.Ok || b.Data == null)
        {
            TempData["Error"] = b.Error ?? "Không tìm thấy đặt vé.";
            return RedirectToAction("Search", "Trips");
        }
        var pays = await _api.GetAsync<List<PaymentDto>>($"api/payments/booking/{id}");
        return View(new PaymentResultViewModel
        {
            Booking = b.Data,
            Payments = pays.Ok && pays.Data != null ? pays.Data : new List<PaymentDto>()
        });
    }
}