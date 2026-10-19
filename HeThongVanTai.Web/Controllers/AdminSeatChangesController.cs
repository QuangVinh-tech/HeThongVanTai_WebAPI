using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Yêu cầu đổi ghế của khách: /Admin/SeatChanges (chỉ Admin)</summary>
[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/SeatChanges")]
public class AdminSeatChangesController : Controller
{
    private static readonly string[] Statuses = { "Pending", "Approved", "Rejected", "Cancelled", "All" };

    private readonly ApiClient _api;
    private readonly ILogger<AdminSeatChangesController> _logger;

    public AdminSeatChangesController(ApiClient api, ILogger<AdminSeatChangesController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status = "Pending", int page = 1)
    {
        status = Statuses.Contains(status) ? status! : "Pending";
        page = Math.Max(page, 1);
        var query = status == "All" ? "" : $"status={status}&";

        var r = await _api.GetAsync<SeatChangeList>($"api/seat-change-requests?{query}page={page}&pageSize=20");
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không tải được danh sách yêu cầu đổi ghế.";
            return View(new AdminSeatChangesViewModel { Status = status });
        }
        return View(new AdminSeatChangesViewModel { Result = r.Data, Status = status });
    }

    [HttpPost("{id:int}/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? status = "Pending")
    {
        var r = await _api.PutAsync<SeatChangeItem>($"api/seat-change-requests/{id}/approve", new { note = "" });
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không xác nhận được yêu cầu.";
        }
        else
        {
            _logger.LogInformation("Admin xác nhận đổi ghế {Id}", id);
            TempData["Success"] = $"Đã xác nhận: vé {r.Data.TicketCode} đổi từ ghế {r.Data.FromSeat} sang ghế {r.Data.ToSeat}.";
        }
        return RedirectToAction(nameof(Index), new { status });
    }

    [HttpPost("{id:int}/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? note, string? status = "Pending")
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            TempData["Error"] = "Vui lòng nhập lý do từ chối.";
            return RedirectToAction(nameof(Index), new { status });
        }

        var r = await _api.PutAsync<SeatChangeItem>($"api/seat-change-requests/{id}/reject", new { note = note.Trim() });
        if (!r.Ok || r.Data == null)
            TempData["Error"] = r.Error ?? "Không từ chối được yêu cầu.";
        else
            TempData["Success"] = $"Đã từ chối yêu cầu đổi ghế của vé {r.Data.TicketCode}.";
        return RedirectToAction(nameof(Index), new { status });
    }
}
