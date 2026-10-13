using System.Text.RegularExpressions;
using HeThongVanTai.Web.Models.ViewModels.VeKhach;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Tra cứu vé không cần đăng nhập: mã đặt vé + số điện thoại/email đã dùng khi đặt
[AllowAnonymous]
[Route("TraCuuVe")]
public class TraCuuVeController : Controller
{
    private static readonly Regex CodeFormat = new("^[A-Za-z0-9]{4,20}$", RegexOptions.Compiled);

    private readonly VeKhachApiService _svc;
    private readonly ILogger<TraCuuVeController> _logger;

    public TraCuuVeController(VeKhachApiService svc, ILogger<TraCuuVeController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View(new TraCuuViewModel());

    // Gửi bằng POST (không để mã và SĐT trên địa chỉ trang, tránh lộ vào lịch sử trình duyệt và log)
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([Bind(Prefix = "Form")] LookupForm form)
    {
        var model = new TraCuuViewModel { Form = form };
        if (!ModelState.IsValid) return View(model);

        var result = await _svc.LookupAsync(form.Code.Trim(), form.Contact.Trim());
        if (!result.Ok || result.Data == null)
        {
            _logger.LogWarning("Tra cứu vé không khớp (API trả {Status})", result.Status);
            ModelState.AddModelError(string.Empty, result.Status == 404
                ? "Không tìm thấy đặt vé khớp với mã và thông tin liên hệ."
                : result.Error ?? "Không tra cứu được. Vui lòng thử lại.");
            return View(model);
        }

        model.Booking = result.Data;
        return View(model);
    }

    // Ảnh QR của vé: Web lấy từ API rồi trả lại, để trình duyệt không cần gọi thẳng API
    [HttpGet("Qr/{ticketCode}")]
    public async Task<IActionResult> Qr(string ticketCode)
    {
        if (!CodeFormat.IsMatch(ticketCode)) return NotFound();

        var result = await _svc.GetQrAsync(ticketCode);
        if (!result.Ok || result.Data == null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Data, "image/png");
    }
}
