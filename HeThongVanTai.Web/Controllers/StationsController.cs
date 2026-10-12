using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin/Stations")]
public class StationsController : Controller
{
    private readonly DanhMucApiService _svc;
    private readonly ILogger<StationsController> _logger;

    public StationsController(DanhMucApiService svc, ILogger<StationsController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, string? city)
    {
        var result = await _svc.GetStationsAsync(keyword, city);
        ViewData["Keyword"] = keyword;
        ViewData["City"] = city;
        ViewData["LoadError"] = result.Ok ? null : result.Error;
        return View(result.Data ?? new List<StationItem>());
    }

    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new StationForm());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StationForm model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var result = await _svc.CreateStationAsync(model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được bến xe. Vui lòng thử lại.");
            return View("Form", model);
        }

        _logger.LogInformation("{User} thêm bến xe {Name}", User.Identity?.Name, model.Name);
        TempData["Success"] = "Đã thêm bến xe.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _svc.GetStationAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy bến xe.";
            return RedirectToAction(nameof(Index));
        }

        var s = result.Data;
        return View("Form", new StationForm { Id = s.Id, Name = s.Name, City = s.City, Address = s.Address });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StationForm model)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await _svc.UpdateStationAsync(id, model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được bến xe. Vui lòng thử lại.");
            return View("Form", model);
        }

        _logger.LogInformation("{User} sửa bến xe {Id}", User.Identity?.Name, id);
        TempData["Success"] = "Đã lưu bến xe.";
        return RedirectToAction(nameof(Index));
    }

    // Xóa dùng POST + AntiForgery, không dùng link GET
    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _svc.DeleteStationAsync(id);
        if (result.Ok)
        {
            _logger.LogInformation("{User} xóa bến xe {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã xóa bến xe.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không xóa được bến xe.";
        }
        return RedirectToAction(nameof(Index));
    }
}
