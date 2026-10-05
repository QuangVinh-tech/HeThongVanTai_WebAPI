using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly BookingService _svc;
    public TicketsController(BookingService svc) => _svc = svc;

    // Tra cứu vé theo mã vé
    [HttpGet("lookup/{code}")]
    public async Task<IActionResult> Lookup(string code) => Ok(await _svc.GetTicketInfoAsync(code));

    // Ảnh QR (PNG) chứa mã vé
    [HttpGet("{code}/qr")]
    public async Task<IActionResult> Qr(string code)
    {
        await _svc.GetTicketInfoAsync(code);   // ném 404 nếu mã vé không tồn tại

        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(code.Trim().ToUpperInvariant(), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10);
        return File(png, "image/png");
    }

    // Đổi ghế (cùng chuyến) hoặc đổi sang chuyến khác cùng tuyến
    [HttpPut("{id:int}/change")]
    public async Task<IActionResult> Change(int id, ChangeTicketDto dto)
        => Ok(await _svc.ChangeTicketAsync(id, dto));
}