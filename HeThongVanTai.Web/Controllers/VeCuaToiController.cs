using System.Text.RegularExpressions;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.VeKhach;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Khách đã đăng nhập xem vé của chính mình. API kiểm tra lại quyền sở hữu ở mọi lời gọi.
[Authorize(Roles = AppRoles.AdminSellerCustomer)]
[Route("VeCuaToi")]
public class VeCuaToiController : Controller
{
    private const int PageSize = 10;
    private static readonly Regex CodeFormat = new("^[A-Za-z0-9]{4,20}$", RegexOptions.Compiled);

    private readonly VeKhachApiService _svc;
    private readonly ILogger<VeCuaToiController> _logger;

    public VeCuaToiController(VeKhachApiService svc, ILogger<VeCuaToiController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        // Chỉ nhận trạng thái nằm trong danh sách
        if (status != null && !VeLabels.BookingStatuses.Contains(status)) status = null;

        var result = await _svc.GetMineAsync(status, Math.Max(page, 1), PageSize);
        ViewData["Status"] = status;
        ViewData["LoadError"] = result.Ok ? null : result.Error;
        return View(result.Data ?? new MyBookingPage { Page = page, PageSize = PageSize });
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> Details(string code)
    {
        if (!CodeFormat.IsMatch(code)) return NotFound();

        var result = await _svc.GetDetailAsync(code);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy đặt vé.";
            return RedirectToAction(nameof(Index));
        }

        var model = new MyBookingDetailViewModel { Booking = result.Data };
        if (result.Data.Status is "Holding" or "Paid")
        {
            var preview = await _svc.GetRefundPreviewAsync(result.Data.Id);
            if (preview.Ok) model.Refund = preview.Data;
        }

        // Khách hàng: lấy các yêu cầu đổi ghế của đặt vé này để hiện trạng thái trên từng vé
        if (User.IsInRole(AppRoles.Customer))
        {
            var changes = await _svc.GetMySeatChangesAsync();
            if (changes.Ok && changes.Data != null)
                model.SeatChanges = changes.Data.Where(x => x.BookingCode == result.Data.Code).ToList();
        }
        return View(model);
    }

    [HttpPost("{id:int}/Huy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string code)
    {
        if (!CodeFormat.IsMatch(code ?? "")) return NotFound();

        var result = await _svc.CancelAsync(id);
        if (result.Ok && result.Data != null)
        {
            _logger.LogInformation("{User} hủy đặt vé {BookingId}, hoàn {Rate:P0} = {Amount}",
                User.Identity?.Name, id, result.Data.RefundRate, result.Data.RefundAmount);
            TempData["Success"] = result.Data.RefundAmount > 0
                ? $"Đã hủy đặt vé. Số tiền hoàn: {VeLabels.Money(result.Data.RefundAmount)} ({VeLabels.Percent(result.Data.RefundRate)})."
                : "Đã hủy đặt vé.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không hủy được đặt vé. Vui lòng thử lại.";
        }
        return RedirectToAction(nameof(Details), new { code });
    }

    [HttpPost("{id:int}/ThanhToan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, string code)
    {
        if (!CodeFormat.IsMatch(code ?? "")) return NotFound();

        var result = await _svc.PayByTransferAsync(id);
        if (result.Ok)
        {
            _logger.LogInformation("{User} chọn chuyển khoản cho đặt vé {BookingId}", User.Identity?.Name, id);
            TempData["Success"] = "Đã ghi nhận yêu cầu chuyển khoản. Nhân viên sẽ xác nhận sau khi nhận được tiền.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không tạo được giao dịch. Vui lòng thử lại.";
        }
        return RedirectToAction(nameof(Details), new { code });
    }
}
