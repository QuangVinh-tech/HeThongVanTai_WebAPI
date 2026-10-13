using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Vé của tôi: lịch sử, hủy vé, đổi ghế (/Account/MyBookings). Phải đăng nhập.</summary>
[Authorize]
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
        return View(new MyBookingsViewModel { Result = r.Data ?? new BookingListResult() });
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
    public async Task<IActionResult> Change(int ticketId, int tripId, string? seat)
    {
        var r = await _api.GetAsync<SeatMapDto>($"api/trips/{tripId}/seats");
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không tải được sơ đồ ghế.";
            return RedirectToAction(nameof(Index));
        }
        return View(new ChangeSeatViewModel
        {
            TicketId = ticketId,
            TripId = tripId,
            CurrentSeat = (seat ?? "").Trim().ToUpperInvariant(),
            Map = r.Data
        });
    }

    [HttpPost("Change/{ticketId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeConfirmed(int ticketId, int tripId, string? newSeatCode, string? currentSeat)
    {
        if (string.IsNullOrWhiteSpace(newSeatCode))
        {
            TempData["Error"] = "Vui lòng chọn ghế mới.";
            return RedirectToAction(nameof(Change), new { ticketId, tripId, seat = currentSeat });
        }

        var r = await _api.PutAsync<ChangeResultDto>($"api/tickets/{ticketId}/change", new { newSeatCode = newSeatCode.Trim() });
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không đổi được ghế.";
            return RedirectToAction(nameof(Change), new { ticketId, tripId, seat = currentSeat });
        }

        TempData["Success"] = $"Đã đổi sang ghế {r.Data.SeatCode}.";
        return RedirectToAction(nameof(Index));
    }
}