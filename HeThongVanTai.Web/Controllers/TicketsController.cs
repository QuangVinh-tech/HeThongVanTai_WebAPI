using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Vé điện tử QR (/Tickets/{code}) và tra cứu vé (/Tickets/Lookup). Ai cũng xem được.</summary>
[AllowAnonymous]
public class TicketsController : Controller
{
    private readonly ApiClient _api;
    private readonly IConfiguration _cfg;

    public TicketsController(ApiClient api, IConfiguration cfg)
    {
        _api = api;
        _cfg = cfg;
    }

    // Nhập mã vé hoặc mã đặt vé
    [HttpGet("Tickets/Lookup")]
    public async Task<IActionResult> Lookup(string? code)
    {
        var vm = new LookupViewModel { Code = code };
        if (string.IsNullOrWhiteSpace(code)) return View(vm);

        var c = code.Trim().ToUpperInvariant();
        var safe = Uri.EscapeDataString(c);

        // Thử theo mã vé trước
        var t = await _api.GetAsync<TicketInfoDto>($"api/tickets/lookup/{safe}");
        if (t.Ok && t.Data != null)
            return RedirectToAction(nameof(Detail), new { code = c });

        // Không phải mã vé thì thử mã đặt vé
        if (t.Status == 404)
        {
            var b = await _api.GetAsync<BookingDto>($"api/bookings/by-code/{safe}");
            if (b.Ok && b.Data != null)
            {
                vm.Booking = b.Data;
                return View(vm);
            }
            vm.Error = "Không tìm thấy vé hoặc mã đặt vé này. Vui lòng kiểm tra lại mã.";
            return View(vm);
        }

        vm.Error = t.Error ?? "Không tra cứu được lúc này. Vui lòng thử lại.";
        return View(vm);
    }

    // Vé điện tử có mã QR
    [HttpGet("Tickets/{code}")]
    public async Task<IActionResult> Detail(string code)
    {
        var t = await _api.GetAsync<TicketInfoDto>($"api/tickets/lookup/{Uri.EscapeDataString(code)}");
        if (!t.Ok || t.Data == null)
        {
            TempData["Error"] = t.Status == 404 ? "Không tìm thấy vé này." : (t.Error ?? "Không tải được vé.");
            return RedirectToAction(nameof(Lookup));
        }

        // Ảnh QR do API tạo; trình duyệt tải trực tiếp từ địa chỉ API
        var apiBase = new Uri((_cfg["ApiBaseUrl"] ?? "").TrimEnd('/') + "/");
        var qr = new Uri(apiBase, $"/api/tickets/{Uri.EscapeDataString(t.Data.TicketCode)}/qr");

        return View(new TicketViewModel { Ticket = t.Data, QrUrl = qr.ToString() });
    }
}