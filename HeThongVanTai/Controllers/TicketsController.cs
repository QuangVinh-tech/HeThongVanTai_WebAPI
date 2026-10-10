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

    // Đổi ghế hoặc đổi chuyến: chủ vé hoặc Admin/Seller
    [Authorize]
    [HttpPut("{id:int}/change")]
    public async Task<IActionResult> Change(int id, ChangeTicketDto dto)
    {
        var staff = User.IsInRole("Admin") || User.IsInRole("Seller");
        var uid = UserClaims.UserId(User);
        if (!staff && (uid == null || !await _svc.IsTicketOwnerAsync(id, uid)))
            return StatusCode(403, new { message = "Bạn không có quyền với vé này." });
        return Ok(await _svc.ChangeTicketAsync(id, dto));
    }
}