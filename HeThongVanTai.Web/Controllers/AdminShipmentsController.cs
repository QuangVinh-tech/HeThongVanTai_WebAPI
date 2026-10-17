using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.VanChuyen;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Quản lý đơn hàng vận chuyển cho nhân viên: /AdminShipments (Admin, Operator, Seller)</summary>
[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Operator + "," + AppRoles.Seller)]
public class AdminShipmentsController : Controller
{
    private const int PageSize = 10;

    private readonly VanChuyenApiService _api;
    private readonly ILogger<AdminShipmentsController> _logger;

    public AdminShipmentsController(VanChuyenApiService api, ILogger<AdminShipmentsController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ---------- Danh sách: lọc theo trạng thái, tìm theo mã hoặc SĐT, phân trang ----------
    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? keyword, int page = 1)
    {
        var vm = new ShipmentListViewModel { Status = status, Keyword = keyword };

        var r = await _api.ListAsync(status, keyword, Math.Max(page, 1), PageSize);
        if (!r.Ok)
        {
            vm.Error = r.Error;
            return View(vm);
        }

        var d = r.Data ?? new PageResult<ShipmentRow>();
        vm.Items = d.Items;
        vm.Total = d.Total;
        vm.TotalPages = (int)Math.Ceiling(d.Total / (double)PageSize);
        vm.Page = Math.Clamp(page, 1, Math.Max(vm.TotalPages, 1));
        return View(vm);
    }

    // ---------- Chi tiết một đơn ----------
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var r = await _api.DetailAsync(id);
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không tìm thấy đơn hàng.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new ShipmentDetailViewModel { Shipment = r.Data };

        // Chỉ Admin và Operator được gắn chuyến, và chỉ khi đơn còn đang xử lý
        var canOperate = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);
        if (canOperate && r.Data.Status is not ("Delivered" or "Cancelled" or "Returned"))
        {
            var trips = await _api.AvailableTripsAsync(id);
            if (trips.Ok && trips.Data != null) vm.Trips = trips.Data;
        }

        return View(vm);
    }

    // ---------- Đổi trạng thái: Admin, Operator ----------
    [Authorize(Roles = AppRoles.AdminOperator)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string status, string? note)
    {
        var r = await _api.ChangeStatusAsync(id, status, note);
        if (r.Ok)
        {
            _logger.LogInformation("{User} đổi trạng thái đơn hàng {Id} sang {Status}", User.Identity?.Name, id, status);
            TempData["Success"] = "Đã cập nhật trạng thái.";
        }
        else
        {
            TempData["Error"] = r.Error ?? "Không cập nhật được trạng thái.";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ---------- Gắn chuyến xe: Admin, Operator ----------
    [Authorize(Roles = AppRoles.AdminOperator)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTrip(int id, int tripId)
    {
        var r = await _api.AssignTripAsync(id, tripId);
        if (r.Ok)
        {
            _logger.LogInformation("{User} gắn chuyến {TripId} cho đơn hàng {Id}", User.Identity?.Name, tripId, id);
            TempData["Success"] = "Đã gắn chuyến xe.";
        }
        else
        {
            TempData["Error"] = r.Error ?? "Không gắn được chuyến xe.";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ---------- Ghi nhận đã thu cước: Admin, Seller ----------
    [Authorize(Roles = AppRoles.AdminSeller)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CollectFee(int id)
    {
        var r = await _api.CollectFeeAsync(id);
        if (r.Ok)
        {
            _logger.LogInformation("{User} thu cước đơn hàng {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã ghi nhận thu cước.";
        }
        else
        {
            TempData["Error"] = r.Error ?? "Không ghi nhận được thu cước.";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }
}