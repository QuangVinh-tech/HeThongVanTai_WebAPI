using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using HeThongVanTai.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Controllers;

/// <summary>
/// Yêu cầu đổi ghế: khách gửi, Admin xác nhận hoặc từ chối.
/// Nhân viên (Admin, Seller) vẫn đổi ghế ngay bằng PUT /api/tickets/{id}/change.
/// </summary>
[ApiController]
[Route("api/seat-change-requests")]
public class SeatChangeRequestsController : ControllerBase
{
    private readonly SeatChangeService _svc;
    private readonly IAuditService? _audit;

    public SeatChangeRequestsController(SeatChangeService svc, IAuditService? audit = null)
    {
        _svc = svc;
        _audit = audit;
    }

    // ----- Khách hàng -----

    [Authorize(Roles = AppRoles.Customer)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateSeatChangeDto dto)
    {
        var uid = UserClaims.UserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _svc.CreateAsync(dto, uid));
    }

    [Authorize(Roles = AppRoles.Customer)]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var uid = UserClaims.UserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _svc.MineAsync(uid));
    }

    [Authorize(Roles = AppRoles.Customer)]
    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var uid = UserClaims.UserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _svc.CancelAsync(id, uid));
    }

    // ----- Admin -----

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> List(string? status, int page = 1, int pageSize = 20) =>
        Ok(await _svc.ListAsync(status, page, pageSize));

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, ApproveSeatChangeDto? dto)
    {
        var item = await _svc.ApproveAsync(id, UserClaims.Email(User) ?? User.Identity?.Name, dto?.Note);
        await _audit.AuditAsync("Approve", "SeatChange", id, $"Xác nhận đổi ghế vé {item.TicketCode}: {item.FromSeat} sang {item.ToSeat}");
        return Ok(item);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPut("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectSeatChangeDto dto)
    {
        var item = await _svc.RejectAsync(id, UserClaims.Email(User) ?? User.Identity?.Name, dto.Note);
        await _audit.AuditAsync("Reject", "SeatChange", id, $"Từ chối đổi ghế vé {item.TicketCode}: {item.FromSeat} sang {item.ToSeat}");
        return Ok(item);
    }
}
