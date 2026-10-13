using System.Globalization;
using System.Text.Json;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Quản lý đặt vé: /Admin/Bookings (Admin, Seller)</summary>
[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Seller)]
[Route("Admin/Bookings")]
public class AdminBookingsController : Controller
{
    private readonly ApiClient _api;
    private readonly ILogger<AdminBookingsController> _logger;

    public AdminBookingsController(ApiClient api, ILogger<AdminBookingsController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ---------- Danh sách: lọc, sắp xếp, phân trang ----------
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminBookingFilter filter)
    {
        filter.Page = Math.Max(filter.Page, 1);
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 50);

        var vm = new AdminBookingsViewModel { Filter = filter };
        var r = await _api.GetAsync<AdminBookingList>(BuildListUrl(filter));
        if (r.Ok) vm.Result = r.Data ?? new AdminBookingList();
        else vm.Error = r.Error;
        return View(vm);
    }

    // ---------- Chi tiết một đặt vé ----------
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var basic = await _api.GetAsync<BookingDto>($"api/bookings/{id}");
        if (!basic.Ok || basic.Data == null)
        {
            TempData["Error"] = basic.Error ?? "Không tìm thấy đặt vé.";
            return RedirectToAction(nameof(Index));
        }

        // Tra theo mã để có thêm tên khách, tuyến, giờ chạy
        var rich = await _api.GetAsync<BookingDto>($"api/bookings/by-code/{Uri.EscapeDataString(basic.Data.Code)}");
        var booking = rich.Ok && rich.Data != null ? rich.Data : basic.Data;

        var pays = await _api.GetAsync<List<PaymentDto>>($"api/payments/booking/{id}");
        return View(new AdminBookingDetailViewModel
        {
            Booking = booking,
            Payments = pays.Ok && pays.Data != null ? pays.Data : new List<PaymentDto>()
        });
    }

    // ---------- Xác nhận đã nhận tiền chuyển khoản ----------
    [HttpPost("{id:int}/ConfirmPayment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id, int paymentId)
    {
        var r = await _api.PutAsync<JsonElement>($"api/payments/{paymentId}/confirm");
        if (!r.Ok)
        {
            TempData["Error"] = r.Error ?? "Không xác nhận được thanh toán.";
        }
        else
        {
            _logger.LogInformation("{User} xác nhận thanh toán {PaymentId} của đặt vé {Id}", User.Identity?.Name, paymentId, id);
            TempData["Success"] = "Đã xác nhận thanh toán.";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ---------- Thu tiền mặt tại quầy ----------
    [HttpPost("{id:int}/CollectCash")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CollectCash(int id)
    {
        var r = await _api.PostAsync<JsonElement>("api/payments", new { bookingId = id, method = "Cash" });
        if (!r.Ok)
        {
            TempData["Error"] = r.Error ?? "Không thu được tiền mặt.";
        }
        else
        {
            _logger.LogInformation("{User} thu tiền mặt đặt vé {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã thu tiền mặt, đặt vé chuyển sang Đã thanh toán.";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ---------- Hủy đặt vé (nhân viên) ----------
    [HttpPost("{id:int}/CancelBooking")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var r = await _api.PutAsync<CancelResultDto>($"api/bookings/{id}/cancel");
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không hủy được đặt vé.";
        }
        else
        {
            _logger.LogInformation("{User} hủy đặt vé {Id}, hoàn {Refund}", User.Identity?.Name, id, r.Data.RefundAmount);
            TempData["Success"] = r.Data.RefundAmount > 0
                ? $"Đã hủy đặt vé. Hoàn lại {DatVeHelpers.Money(r.Data.RefundAmount)} ({r.Data.RefundRate:P0})."
                : "Đã hủy đặt vé.";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    private static string BuildListUrl(AdminBookingFilter f)
    {
        var inv = CultureInfo.InvariantCulture;
        var q = new List<string>
        {
            $"page={f.Page}",
            $"pageSize={f.PageSize}",
            $"desc={(f.Desc ? "true" : "false")}"
        };
        if (!string.IsNullOrWhiteSpace(f.Status)) q.Add($"status={Uri.EscapeDataString(f.Status)}");
        if (!string.IsNullOrWhiteSpace(f.Keyword)) q.Add($"keyword={Uri.EscapeDataString(f.Keyword.Trim())}");
        if (f.From.HasValue) q.Add($"from={f.From.Value.ToString("yyyy-MM-dd", inv)}");
        if (f.To.HasValue) q.Add($"to={f.To.Value.ToString("yyyy-MM-dd", inv)}");
        if (!string.IsNullOrWhiteSpace(f.SortBy)) q.Add($"sortBy={Uri.EscapeDataString(f.SortBy)}");
        return "api/bookings?" + string.Join("&", q);
    }
}