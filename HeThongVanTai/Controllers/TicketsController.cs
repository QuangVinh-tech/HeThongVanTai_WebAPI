using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly BookingService _svc;
    public TicketsController(BookingService svc) => _svc = svc;

    // Công khai: tra cứu vé theo mã vé
    [AllowAnonymous]
    [HttpGet("lookup/{code}")]
    public async Task<IActionResult> Lookup(string code) => Ok(await _svc.GetTicketInfoAsync(code));

    // Công khai: ảnh QR (PNG) chứa mã vé
    [AllowAnonymous]
    [HttpGet("{code}/qr")]
    public async Task<IActionResult> Qr(string code)
    {
        await _svc.GetTicketInfoAsync(code);   // ném 404 nếu mã vé không tồn tại

        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(code.Trim().ToUpperInvariant(), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10);
        return File(png, "image/png");
    }

    // Soát vé: nhân viên quét mã QR hoặc nhập mã vé
    [Authorize(Roles = AppRoles.CheckInStaff)]
    [HttpPost("{code}/checkin")]
    public async Task<IActionResult> CheckIn(string code) => Ok(await _svc.CheckInAsync(code));

    // Đổi ghế hoặc đổi chuyến ngay: chỉ nhân viên (Admin, Seller).
    // Khách hàng gửi yêu cầu đổi ghế (POST /api/seat-change-requests) để Admin xác nhận.
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Seller)]
    [HttpPut("{id:int}/change")]
    public async Task<IActionResult> Change(int id, ChangeTicketDto dto) => Ok(await _svc.ChangeTicketAsync(id, dto));
}
