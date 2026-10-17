using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.TaiXeChuyen;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Trang đích sau đăng nhập của vai trò Tài xế (/Driver/Trips) - việc 40.
// Tài xế chỉ thấy và đổi trạng thái chuyến của mình (API kiểm quyền; Web chỉ ẩn nút cho gọn).
[Authorize(Roles = AppRoles.Driver)]
[Route("Driver/Trips")]
public class DriverTripsController : Controller
{
    private readonly DriverTripsApiService _svc;
    private readonly ILogger<DriverTripsController> _logger;

    public DriverTripsController(DriverTripsApiService svc, ILogger<DriverTripsController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(bool includePast = false)
    {
        var result = await _svc.GetMineAsync(includePast);
        return View(new DriverTripsModel
        {
            IncludePast = includePast,
            DriverName = result.Data?.DriverName,
            Items = result.Data?.Items ?? new List<DriverTripItem>(),
            NotLinked = result.Status == 404,
            Error = result.Ok || result.Status == 404 ? null : result.Error
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _svc.GetDetailAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy chuyến.";
            return RedirectToAction(nameof(Index));
        }
        return View(result.Data);
    }

    private async Task<IActionResult> ChangeAsync(int id, string status, DateTime? newDepartAt, string okMessage)
    {
        var result = await _svc.SetStatusAsync(id, status, newDepartAt);
        if (result.Ok)
        {
            _logger.LogInformation("{User} chuyển chuyến {Id} sang {Status}", User.Identity?.Name, id, status);
            TempData["Success"] = okMessage;
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không đổi được trạng thái chuyến.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/Start")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Start(int id) => ChangeAsync(id, "Running", null, "Đã bắt đầu chuyến. Chúc bạn thượng lộ bình an!");

    [HttpPost("{id:int}/Complete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(int id) => ChangeAsync(id, "Completed", null, "Đã hoàn thành chuyến.");

    [HttpPost("{id:int}/Delay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delay(int id, DateTime? newDepartAt)
    {
        if (newDepartAt == null)
        {
            TempData["Error"] = "Vui lòng chọn giờ khởi hành mới.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return await ChangeAsync(id, "Delayed", newDepartAt, "Đã báo trễ chuyến.");
    }
}