using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Models.ViewModels.XeTaiXe;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin/Vehicles")]
public class VehiclesController : Controller
{
    private readonly XeTaiXeApiService _svc;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(XeTaiXeApiService svc, ILogger<VehiclesController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(VehicleFilter filter)
    {
        if (filter.Page < 1) filter.Page = 1;

        var list = await _svc.GetVehiclesAsync(filter);
        var types = await _svc.GetVehicleTypesAsync();

        var model = new VehicleListModel
        {
            Filter = filter,
            Items = list.Data?.Items ?? new List<VehicleItem>(),
            TotalItems = list.Data?.TotalItems ?? 0,
            TotalPages = list.Data?.TotalPages ?? 0,
            VehicleTypes = types.Data ?? new List<VehicleTypeItem>(),
            LoadError = !list.Ok ? list.Error : (!types.Ok ? types.Error : null)
        };
        return View(model);
    }

    private async Task LoadTypesAsync(VehicleForm model)
    {
        var types = await _svc.GetVehicleTypesAsync();
        if (!types.Ok)
        {
            ModelState.AddModelError(string.Empty, types.Error ?? "Không tải được danh sách loại xe.");
            return;
        }
        model.VehicleTypes = types.Data ?? new List<VehicleTypeItem>();
    }

    private void ValidateImage(VehicleForm model)
    {
        var error = XeTaiXeApiService.CheckImage(model.ImageFile);
        if (error != null) ModelState.AddModelError(nameof(VehicleForm.ImageFile), error);
    }

    private async Task<(bool Ok, string? Url)> UploadIfAnyAsync(VehicleForm model)
    {
        if (model.ImageFile == null || model.ImageFile.Length == 0) return (true, null);

        var upload = await _svc.UploadImageAsync(model.ImageFile, "vehicle");
        if (!upload.Ok)
        {
            ModelState.AddModelError(nameof(VehicleForm.ImageFile), upload.Error ?? "Không tải được ảnh lên.");
            return (false, null);
        }
        return (true, upload.Data);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var model = new VehicleForm();
        await LoadTypesAsync(model);
        return View("Form", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleForm model)
    {
        ValidateImage(model);
        if (!ModelState.IsValid)
        {
            await LoadTypesAsync(model);
            return View("Form", model);
        }

        var upload = await UploadIfAnyAsync(model);
        if (!upload.Ok)
        {
            await LoadTypesAsync(model);
            return View("Form", model);
        }

        var result = await _svc.CreateVehicleAsync(model, upload.Url);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được xe. Vui lòng thử lại.");
            await LoadTypesAsync(model);
            return View("Form", model);
        }

        _logger.LogInformation("{User} thêm xe {Plate}", User.Identity?.Name, model.Plate);
        TempData["Success"] = "Đã thêm xe.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _svc.GetVehicleAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy xe.";
            return RedirectToAction(nameof(Index));
        }

        var v = result.Data;
        var model = new VehicleForm
        {
            Id = v.Id,
            Plate = v.Plate,
            VehicleTypeId = v.VehicleTypeId,
            RegistrationExpiry = v.RegistrationExpiry,
            InsuranceExpiry = v.InsuranceExpiry,
            Status = v.Status,
            ImageUrl = v.ImageUrl
        };
        await LoadTypesAsync(model);
        return View("Form", model);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VehicleForm model)
    {
        model.Id = id;
        ValidateImage(model);
        if (!ModelState.IsValid)
        {
            await LoadTypesAsync(model);
            return View("Form", model);
        }

        var upload = await UploadIfAnyAsync(model);
        if (!upload.Ok)
        {
            await LoadTypesAsync(model);
            return View("Form", model);
        }

        var result = await _svc.UpdateVehicleAsync(id, model, upload.Url);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được xe. Vui lòng thử lại.");
            await LoadTypesAsync(model);
            return View("Form", model);
        }

        _logger.LogInformation("{User} sửa xe {Id}", User.Identity?.Name, id);
        TempData["Success"] = "Đã lưu xe.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _svc.DeleteVehicleAsync(id);
        if (result.Ok)
        {
            _logger.LogInformation("{User} xóa xe {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã xóa xe.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không xóa được xe.";
        }
        return RedirectToAction(nameof(Index));
    }
}