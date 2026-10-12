using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin/Routes")]
public class RoutesController : Controller
{
    private readonly DanhMucApiService _svc;
    private readonly ILogger<RoutesController> _logger;

    public RoutesController(DanhMucApiService svc, ILogger<RoutesController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await _svc.GetRoutesAsync();
        ViewData["LoadError"] = result.Ok ? null : result.Error;
        return View(result.Data ?? new List<RouteItem>());
    }

    // Nạp danh sách bến vào form; trả về false nếu API lỗi
    private async Task<bool> LoadStationsAsync(RouteForm model)
    {
        var stations = await _svc.GetStationsAsync(null, null);
        if (!stations.Ok)
        {
            ModelState.AddModelError(string.Empty, stations.Error ?? "Không tải được danh sách bến xe.");
            return false;
        }
        model.Stations = stations.Data ?? new List<StationItem>();
        return true;
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var model = new RouteForm();
        await LoadStationsAsync(model);
        return View("Form", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RouteForm model)
    {
        ValidateRoute(model);
        if (!ModelState.IsValid)
        {
            await LoadStationsAsync(model);
            return View("Form", model);
        }

        var result = await _svc.CreateRouteAsync(model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được tuyến đường. Vui lòng thử lại.");
            await LoadStationsAsync(model);
            return View("Form", model);
        }

        _logger.LogInformation("{User} thêm tuyến đường {Name}", User.Identity?.Name, model.Name);
        TempData["Success"] = "Đã thêm tuyến đường.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _svc.GetRouteAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy tuyến đường.";
            return RedirectToAction(nameof(Index));
        }

        var r = result.Data;
        var model = new RouteForm
        {
            Id = r.Id,
            Name = r.Name,
            FromStationId = r.FromStationId,
            ToStationId = r.ToStationId,
            DistanceKm = r.DistanceKm,
            DurationMin = r.DurationMin
        };
        await LoadStationsAsync(model);
        return View("Form", model);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RouteForm model)
    {
        model.Id = id;
        ValidateRoute(model);
        if (!ModelState.IsValid)
        {
            await LoadStationsAsync(model);
            return View("Form", model);
        }

        var result = await _svc.UpdateRouteAsync(id, model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được tuyến đường. Vui lòng thử lại.");
            await LoadStationsAsync(model);
            return View("Form", model);
        }

        _logger.LogInformation("{User} sửa tuyến đường {Id}", User.Identity?.Name, id);
        TempData["Success"] = "Đã lưu tuyến đường.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _svc.DeleteRouteAsync(id);
        if (result.Ok)
        {
            _logger.LogInformation("{User} xóa tuyến đường {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã xóa tuyến đường.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không xóa được tuyến đường.";
        }
        return RedirectToAction(nameof(Index));
    }

    private void ValidateRoute(RouteForm model)
    {
        if (model.FromStationId > 0 && model.FromStationId == model.ToStationId)
        {
            ModelState.AddModelError(nameof(RouteForm.ToStationId), "Bến đến phải khác bến đi");
        }
    }
}
