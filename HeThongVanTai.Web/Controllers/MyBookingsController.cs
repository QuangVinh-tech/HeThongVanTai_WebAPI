using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Vé của tôi: lịch sử, hủy vé, đổi ghế (/Account/MyBookings). Phải đăng nhập.</summary>
[Authorize(Roles = AppRoles.AdminSellerCustomer)]
[Route("Account/MyBookings")]
public class MyBookingsController : Controller
{
    private readonly ApiClient _api;
    private readonly ILogger<MyBookingsController> _logger;

    public MyBookingsController(ApiClient api, ILogger<MyBookingsController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ---------- Lịch sử đặt vé ----------
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Max(page, 1);
        var r = await _api.GetAsync<BookingListResult>($"api/bookings/mine?page={page}&pageSize=5");
        if (!r.Ok)
        {
            TempData["Error"] = r.Error ?? "Không tải được danh sách vé.";
            return RedirectToAction("Index", "Home");
        }
        var vm = new MyBookingsViewModel { Result = r.Data ?? new BookingListResult() };
        if (IsRequestMode())
        {
            var sc = await _api.GetAsync<List<SeatChangeItem>>("api/seat-change-requests/mine");
            if (sc.Ok && sc.Data != null) vm.SeatChanges = sc.Data;
        }
        return View(vm);
    }

    // ---------- Hủy đặt vé ----------
    [HttpGet("Cancel/{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var b = await _api.GetAsync<BookingDto>($"api/bookings/{id}");
        if (!b.Ok || b.Data == null)
        {
            TempData["Error"] = b.Error ?? "Không tìm thấy đặt vé.";
            return RedirectToAction(nameof(Index));
        }
        var p = await _api.GetAsync<RefundPreviewDto>($"api/bookings/{id}/refund-preview");
        if (!p.Ok || p.Data == null)
        {
            TempData["Error"] = p.Error ?? "Không tính được số tiền hoàn.";
            return RedirectToAction(nameof(Index));
        }
        return View(new CancelViewModel { Booking = b.Data, Preview = p.Data });
    }

    [HttpPost("Cancel/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirmed(int id)
    {
        var r = await _api.PutAsync<CancelResultDto>($"api/bookings/{id}/cancel");
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không hủy được đặt vé.";
            return RedirectToAction(nameof(Index));
        }

        _logger.LogInformation("{User} hủy đặt vé {Id}, hoàn {Refund}", User.Identity?.Name, id, r.Data.RefundAmount);
        TempData["Success"] = r.Data.RefundAmount > 0
            ? $"Đã hủy đặt vé. Số tiền hoàn lại: {DatVeHelpers.Money(r.Data.RefundAmount)} ({r.Data.RefundRate:P0})."
            : "Đã hủy đặt vé.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Đổi ghế ----------
    [HttpGet("Change/{ticketId:int}")]
    public async Task<IActionResult> Change(int ticketId, int tripId, string? seat, string? returnUrl = null)
    {
        var r = await _api.GetAsync<SeatMapDto>($"api/trips/{tripId}/seats");
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không tải được sơ đồ ghế.";
            return Back(returnUrl);
        }
        ViewData["RequestMode"] = IsRequestMode();
        return View(new ChangeSeatViewModel
        {
            TicketId = ticketId,
            TripId = tripId,
            CurrentSeat = (seat ?? "").Trim().ToUpperInvariant(),
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            Map = r.Data
        });
    }

    [HttpPost("Change/{ticketId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeConfirmed(int ticketId, int tripId, string? newSeatCode, string? currentSeat, string? reason = null, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(newSeatCode))
        {
            TempData["Error"] = "Vui lòng chọn ghế mới.";
            return RedirectToAction(nameof(Change), new { ticketId, tripId, seat = currentSeat, returnUrl });
        }

        // Khách hàng chỉ gửi yêu cầu để Admin xác nhận; nhân viên (Admin, Seller) đổi ngay như trước
        if (IsRequestMode())
        {
            var req = await _api.PostAsync<SeatChangeItem>("api/seat-change-requests",
                new { ticketId, newSeatCode = newSeatCode.Trim(), reason = reason?.Trim() });
            if (!req.Ok || req.Data == null)
            {
                TempData["Error"] = req.Error ?? "Không gửi được yêu cầu đổi ghế.";
                return RedirectToAction(nameof(Change), new { ticketId, tripId, seat = currentSeat, returnUrl });
            }

            TempData["Success"] = $"Đã gửi yêu cầu đổi sang ghế {req.Data.ToSeat}. Quản trị viên sẽ xác nhận, bạn xem kết quả ở mục Vé của tôi.";
            return Back(returnUrl);
        }

        var r = await _api.PutAsync<ChangeResultDto>($"api/tickets/{ticketId}/change", new { newSeatCode = newSeatCode.Trim() });
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không đổi được ghế.";
            return RedirectToAction(nameof(Change), new { ticketId, tripId, seat = currentSeat, returnUrl });
        }

        TempData["Success"] = $"Đã đổi sang ghế {r.Data.SeatCode}.";
        return Back(returnUrl);
    }

    // ---------- Hủy yêu cầu đổi ghế đang chờ ----------
    [HttpPost("SeatChange/{id:int}/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSeatChange(int id, string? returnUrl = null)
    {
        var r = await _api.PutAsync<SeatChangeItem>($"api/seat-change-requests/{id}/cancel");
        if (!r.Ok)
            TempData["Error"] = r.Error ?? "Không hủy được yêu cầu đổi ghế.";
        else
            TempData["Success"] = "Đã hủy yêu cầu đổi ghế.";
        return Back(returnUrl);
    }

    // Quay về trang đã mở chức năng đổi ghế (chỉ nhận đường dẫn nội bộ để tránh chuyển hướng ra ngoài)
    private IActionResult Back(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));

    // Khách hàng gửi yêu cầu chờ xác nhận; Admin, Seller đổi ngay
    private bool IsRequestMode() => !(User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Seller));
}