using System.Globalization;
using System.Text.Json;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Quản lý chuyến xe: /Admin/Trips (Admin, Operator)</summary>
[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin/Trips")]
public class AdminTripsController : Controller
{
    private static readonly string[] AllowedStatuses = { "Open", "Delayed", "Cancelled" };

    private readonly ApiClient _api;
    private readonly DanhMucApiService _danhMuc;
    private readonly ILogger<AdminTripsController> _logger;

    public AdminTripsController(ApiClient api, DanhMucApiService danhMuc, ILogger<AdminTripsController> logger)
    {
        _api = api;
        _danhMuc = danhMuc;
        _logger = logger;
    }

    // ---------- Danh sách: lọc, sắp xếp, phân trang ----------
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] TripAdminFilter filter)
    {
        filter.Page = Math.Max(filter.Page, 1);
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 50);

        var (routes, vehicles, drivers) = await LoadLookupsAsync();
        var vm = new AdminTripsViewModel
        {
            Filter = filter,
            Routes = routes,
            Vehicles = vehicles,
            Drivers = drivers
        };

        var r = await _api.GetAsync<ListResult<AdminTripItem>>(BuildListUrl(filter));
        if (r.Ok) vm.Result = r.Data ?? new ListResult<AdminTripItem>();
        else vm.Error = r.Error;
        return View(vm);
    }

    // ---------- Tạo chuyến ----------
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var vm = await BuildCreateAsync(new CreateTripForm { DepartAt = DateTime.Today.AddDays(1).AddHours(8) });
        return View(vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTripForm form)
    {
        if (form.BusRouteId <= 0) ModelState.AddModelError("", "Vui lòng chọn tuyến đường.");
        if (form.VehicleId <= 0) ModelState.AddModelError("", "Vui lòng chọn xe.");
        if (form.DriverId <= 0) ModelState.AddModelError("", "Vui lòng chọn tài xế.");
        if (form.DepartAt == null) ModelState.AddModelError("", "Vui lòng chọn giờ khởi hành.");
        else if (form.DepartAt <= DateTime.Now) ModelState.AddModelError("", "Giờ khởi hành phải ở tương lai.");
        if (form.ArriveAt.HasValue && form.DepartAt.HasValue && form.ArriveAt <= form.DepartAt)
            ModelState.AddModelError("", "Giờ đến phải sau giờ khởi hành.");
        if (form.Price <= 0) ModelState.AddModelError("", "Giá vé phải lớn hơn 0.");

        if (!ModelState.IsValid) return View(await BuildCreateAsync(form));

        var body = new
        {
            busRouteId = form.BusRouteId,
            vehicleId = form.VehicleId,
            driverId = form.DriverId,
            departAt = form.DepartAt,
            arriveAt = form.ArriveAt,
            price = form.Price
        };
        var r = await _api.PostAsync<JsonElement>("api/trips", body);
        if (!r.Ok)
        {
            // API báo rõ lý do: xe/tài xế trùng lịch, hết hạn đăng kiểm hoặc GPLX...
            ModelState.AddModelError("", r.Error ?? "Không tạo được chuyến xe.");
            return View(await BuildCreateAsync(form));
        }

        _logger.LogInformation("{User} tạo chuyến mới tuyến {Route} lúc {Depart}", User.Identity?.Name, form.BusRouteId, form.DepartAt);
        TempData["Success"] = "Đã tạo chuyến xe.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Đổi xe / tài xế ----------
    [HttpPost("{id:int}/ChangeVehicle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeVehicle(int id, int newVehicleId, int? newDriverId)
    {
        if (newVehicleId <= 0)
        {
            TempData["Error"] = "Vui lòng chọn xe mới.";
            return RedirectToAction(nameof(Index));
        }

        var r = await _api.PutAsync<JsonElement>($"api/trips/{id}/change-vehicle",
            new { newVehicleId, newDriverId = newDriverId > 0 ? newDriverId : null });
        if (!r.Ok) TempData["Error"] = r.Error ?? "Không đổi được xe.";
        else
        {
            _logger.LogInformation("{User} đổi xe/tài xế chuyến {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã đổi xe/tài xế cho chuyến.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Đổi trạng thái: mở bán, hoãn (đổi giờ), hủy ----------
    [HttpPost("{id:int}/Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, DateTime? newDepartAt)
    {
        if (!AllowedStatuses.Contains(status))
        {
            TempData["Error"] = "Trạng thái không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }
        if (status == "Delayed" && (newDepartAt == null || newDepartAt <= DateTime.Now))
        {
            TempData["Error"] = "Hoãn chuyến cần chọn giờ khởi hành mới ở tương lai.";
            return RedirectToAction(nameof(Index));
        }

        var r = await _api.PatchAsync<JsonElement>($"api/trips/{id}/status",
            new { status, newDepartAt = status == "Delayed" ? newDepartAt : null });
        if (!r.Ok) TempData["Error"] = r.Error ?? "Không đổi được trạng thái.";
        else
        {
            _logger.LogInformation("{User} đổi trạng thái chuyến {Id} sang {Status}", User.Identity?.Name, id, status);
            TempData["Success"] = $"Đã chuyển chuyến sang trạng thái \"{TripStatusHelper.Text(status)}\".";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Hàm dùng chung ----------
    private async Task<(List<RouteItem>, List<VehicleOption>, List<DriverOption>)> LoadLookupsAsync()
    {
        var routes = await _danhMuc.GetRoutesAsync();
        var vehicles = await _api.GetAsync<ListResult<VehicleOption>>("api/vehicles?status=Active&pageSize=100");
        var drivers = await _api.GetAsync<ListResult<DriverOption>>("api/drivers?status=Active&pageSize=100");

        return (
            routes.Ok && routes.Data != null ? routes.Data : new List<RouteItem>(),
            vehicles.Ok && vehicles.Data != null ? vehicles.Data.Items : new List<VehicleOption>(),
            drivers.Ok && drivers.Data != null ? drivers.Data.Items : new List<DriverOption>());
    }

    private async Task<CreateTripViewModel> BuildCreateAsync(CreateTripForm form)
    {
        var (routes, vehicles, drivers) = await LoadLookupsAsync();
        return new CreateTripViewModel { Form = form, Routes = routes, Vehicles = vehicles, Drivers = drivers };
    }

    private static string BuildListUrl(TripAdminFilter f)
    {
        var inv = CultureInfo.InvariantCulture;
        var q = new List<string>
        {
            $"pageNumber={f.Page}",
            $"pageSize={f.PageSize}",
            $"isAscending={(f.IsAscending ? "true" : "false")}"
        };
        if (f.BusRouteId.HasValue) q.Add($"busRouteId={f.BusRouteId}");
        if (f.Date.HasValue) q.Add($"date={f.Date.Value.ToString("yyyy-MM-dd", inv)}");
        if (!string.IsNullOrWhiteSpace(f.Status)) q.Add($"status={Uri.EscapeDataString(f.Status)}");
        if (!string.IsNullOrWhiteSpace(f.SortBy)) q.Add($"sortBy={Uri.EscapeDataString(f.SortBy)}");
        return "api/trips?" + string.Join("&", q);
    }
}