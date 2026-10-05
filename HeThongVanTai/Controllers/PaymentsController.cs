using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _svc;
    public PaymentsController(PaymentService svc) => _svc = svc;

    // Cash: Paid ngay. BankTransfer: Pending, chờ xác nhận
    // TODO sau Auth: [Authorize(Roles = "...")]
    [HttpPost]
    public async Task<IActionResult> Create(PayDto dto) => StatusCode(201, await _svc.CreateAsync(dto));

    // Kế toán xác nhận đã nhận tiền chuyển khoản
    [HttpPut("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id) => Ok(await _svc.ConfirmAsync(id));

    [HttpGet("booking/{bookingId:int}")]
    public async Task<IActionResult> ByBooking(int bookingId) => Ok(await _svc.ByBookingAsync(bookingId));
}