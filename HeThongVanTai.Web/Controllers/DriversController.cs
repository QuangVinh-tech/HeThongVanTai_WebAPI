using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.XeTaiXe;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin/Drivers")]
public class DriversController : Controller
{
    private readonly XeTaiXeApiService _svc;
    private readonly ILogger<DriversController> _logger;

    public DriversController(XeTaiXeApiService svc, ILogger<DriversController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DriverFilter filter)
    {
        if (filter.Page < 1) filter.Page = 1;

        var list = await _svc.GetDriversAsync(filter);
        var model = new DriverListModel
        {
            Filter = filter,
            Items = list.Data?.Items ?? new List<DriverItem>(),
            TotalItems = list.Data?.TotalItems ?? 0,
            TotalPages = list.Data?.TotalPages ?? 0,
            LoadError = list.Ok ? null : list.Error
        };
        return View(model);
    }

    private void ValidateImage(DriverForm model)
    {
        var error = XeTaiXeApiService.CheckImage(model.AvatarFile);
        if (error != null) ModelState.AddModelError(nameof(DriverForm.AvatarFile), error);
    }

    private async Task<(bool Ok, string? Url)> UploadIfAnyAsync(DriverForm model)
    {
        if (model.AvatarFile == null || model.AvatarFile.Length == 0) return (true, null);

        var upload = await _svc.UploadImageAsync(model.AvatarFile, "driver");
        if (!upload.Ok)
        {
            ModelState.AddModelError(nameof(DriverForm.AvatarFile), upload.Error ?? "Không tải được ảnh lên.");
            return (false, null);
        }
        return (true, upload.Data);
    }

    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new DriverForm());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DriverForm model)
    {
        ValidateImage(model);
        if (!ModelState.IsValid) return View("Form", model);

        var upload = await UploadIfAnyAsync(model);
        if (!upload.Ok) return View("Form", model);

        var result = await _svc.CreateDriverAsync(model, upload.Url);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được tài xế. Vui lòng thử lại.");
            return View("Form", model);
        }

        _logger.LogInformation("{User} thêm tài xế {Name}", User.Identity?.Name, model.FullName);
        TempData["Success"] = "Đã thêm tài xế.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _svc.GetDriverAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy tài xế.";
            return RedirectToAction(nameof(Index));
        }

        var d = result.Data;
        return View("Form", new DriverForm
        {
            Id = d.Id,
            FullName = d.FullName,
            Phone = d.Phone,
            LicenseNo = d.LicenseNo,
            LicenseClass = d.LicenseClass,
            LicenseExpiry = d.LicenseExpiry,
            Status = d.Status,
            AvatarUrl = d.AvatarUrl
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DriverForm model)
    {
        model.Id = id;
        ValidateImage(model);
        if (!ModelState.IsValid) return View("Form", model);

        var upload = await UploadIfAnyAsync(model);
        if (!upload.Ok) return View("Form", model);

        var result = await _svc.UpdateDriverAsync(id, model, upload.Url);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được tài xế. Vui lòng thử lại.");
            return View("Form", model);
        }

        _logger.LogInformation("{User} sửa tài xế {Id}", User.Identity?.Name, id);
        TempData["Success"] = "Đã lưu tài xế.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _svc.DeleteDriverAsync(id);
        if (result.Ok)
        {
            _logger.LogInformation("{User} xóa tài xế {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã xóa tài xế.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không xóa được tài xế.";
        }
        return RedirectToAction(nameof(Index));
    }
}