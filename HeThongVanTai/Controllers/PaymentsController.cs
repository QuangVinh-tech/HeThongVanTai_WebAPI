using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _svc;
    private readonly BookingService _bookings;

    public PaymentsController(PaymentService svc, BookingService bookings)
    {
        _svc = svc;
        _bookings = bookings;
    }

    private bool IsStaff => User.IsInRole("Admin") || User.IsInRole("Seller");

    private IActionResult Deny() =>
        StatusCode(403, new { message = "Bạn không có quyền với đặt vé này." });

    // Cash: Paid ngay (chỉ nhân viên quầy). BankTransfer: Pending, chờ xác nhận (khách dùng được)
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(PayDto dto)
    {
        if (!IsStaff)
        {
            if (string.Equals(dto.Method, "Cash", StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, new { message = "Tiền mặt chỉ thu tại quầy, vui lòng chọn chuyển khoản." });

            var uid = UserClaims.UserId(User);
            if (uid == null || !await _bookings.IsOwnerAsync(dto.BookingId, uid))
                return Deny();
        }
        return StatusCode(201, await _svc.CreateAsync(dto));
    }

    // Nhân viên / kế toán xác nhận đã nhận tiền chuyển khoản
    [Authorize(Roles = "Admin,Seller,Accountant")]
    [HttpPut("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id) => Ok(await _svc.ConfirmAsync(id));

    [Authorize]
    [HttpGet("booking/{bookingId:int}")]
    public async Task<IActionResult> ByBooking(int bookingId)
    {
        var uid = UserClaims.UserId(User);
        var ok = IsStaff || User.IsInRole("Accountant")
                 || (uid != null && await _bookings.IsOwnerAsync(bookingId, uid));
        if (!ok) return Deny();
        return Ok(await _svc.ByBookingAsync(bookingId));
    }
}