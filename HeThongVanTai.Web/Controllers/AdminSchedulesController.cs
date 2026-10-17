using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Models.ViewModels.LichChay;
using HeThongVanTai.Web.Models.ViewModels.XeTaiXe;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Lịch chạy cố định (việc 39): Admin và Operator. Xem trước rồi mới tạo; trùng giờ thì bị từ chối.
[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin/Schedules")]
public class AdminSchedulesController : Controller
{
    private readonly SchedulesApiService _svc;
    private readonly DanhMucApiService _danhMuc;
    private readonly ILogger<AdminSchedulesController> _logger;

    public AdminSchedulesController(SchedulesApiService svc, DanhMucApiService danhMuc, ILogger<AdminSchedulesController> logger)
    {
        _svc = svc;
        _danhMuc = danhMuc;
        _logger = logger;
    }

    private async Task<ScheduleModel> BuildModelAsync(ScheduleForm form)
    {
        var routes = await _danhMuc.GetRoutesAsync();
        var vehicles = await _svc.GetActiveVehiclesAsync();
        var drivers = await _svc.GetActiveDriversAsync();

        return new ScheduleModel
        {
            Form = form,
            Routes = routes.Data ?? new List<RouteItem>(),
            Vehicles = vehicles.Data?.Items ?? new List<VehicleItem>(),
            Drivers = drivers.Data?.Items ?? new List<DriverItem>(),
            LoadError = !routes.Ok ? routes.Error : !vehicles.Ok ? vehicles.Error : !drivers.Ok ? drivers.Error : null
        };
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var form = new ScheduleForm
        {
            FromDate = DateTime.Today.AddDays(1),
            ToDate = DateTime.Today.AddDays(7)
        };
        return View(await BuildModelAsync(form));
    }

    [HttpPost("Preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview([Bind(Prefix = "Form")] ScheduleForm form)
    {
        var model = await BuildModelAsync(form);
        if (!ModelState.IsValid) return View("Index", model);

        var preview = await _svc.PreviewAsync(form);
        if (!preview.Ok)
        {
            ModelState.AddModelError(string.Empty, preview.Error ?? "Không xem trước được lịch chạy.");
            return View("Index", model);
        }

        model.Preview = preview.Data;
        return View("Index", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] ScheduleForm form)
    {
        var model = await BuildModelAsync(form);
        if (!ModelState.IsValid) return View("Index", model);

        var result = await _svc.CreateAsync(form);
        if (!result.Ok)
        {
            // Hiện lại bảng xem trước để thấy ngày nào bị trùng
            ModelState.AddModelError(string.Empty, result.Error ?? "Không tạo được lịch chạy.");
            var preview = await _svc.PreviewAsync(form);
            if (preview.Ok) model.Preview = preview.Data;
            return View("Index", model);
        }

        _logger.LogInformation("{User} tạo lịch chạy tuyến {Route} từ {From} đến {To}",
            User.Identity?.Name, form.BusRouteId, form.FromDate, form.ToDate);
        TempData["Success"] = "Đã tạo lịch chạy. Các chuyến mới hiện trong danh sách chuyến xe.";
        return RedirectToAction("Index", "AdminTrips");
    }
}
