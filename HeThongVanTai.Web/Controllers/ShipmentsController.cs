using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Models.ViewModels.VanChuyen;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Gửi hàng: tính cước, tạo đơn, tra cứu, đơn của tôi (/Shipments/...)</summary>
public class ShipmentsController : Controller
{
    private const int PageSize = 10;

    private readonly VanChuyenApiService _api;
    private readonly DanhMucApiService _danhMuc;
    private readonly ILogger<ShipmentsController> _logger;

    public ShipmentsController(VanChuyenApiService api, DanhMucApiService danhMuc, ILogger<ShipmentsController> logger)
    {
        _api = api;
        _danhMuc = danhMuc;
        _logger = logger;
    }

    private async Task<(List<StationItem> Stations, string? Error)> StationsAsync()
    {
        var r = await _danhMuc.GetStationsAsync(null, null);
        return r.Ok ? (r.Data ?? new List<StationItem>(), null) : (new List<StationItem>(), r.Error);
    }

    private void CheckRules(QuoteBody b)
    {
        if (b.FromStationId > 0 && b.FromStationId == b.ToStationId)
            ModelState.AddModelError(nameof(b.ToStationId), "Bến gửi và bến nhận phải khác nhau.");
        if (b.ServiceType is not ("Standard" or "Express"))
            ModelState.AddModelError(nameof(b.ServiceType), "Loại dịch vụ không hợp lệ.");
        if (b.PickupMode is not ("Door" or "Station"))
            ModelState.AddModelError(nameof(b.PickupMode), "Hình thức nhận hàng không hợp lệ.");
        if (b.DeliveryMode is not ("Door" or "Station"))
            ModelState.AddModelError(nameof(b.DeliveryMode), "Hình thức giao hàng không hợp lệ.");
    }

    // ---------- Tính cước nhanh (không cần đăng nhập) ----------
    [HttpGet]
    public async Task<IActionResult> Quote()
    {
        var (stations, error) = await StationsAsync();
        return View(new QuotePageModel { Stations = stations, Error = error });
    }

    // Chỉ tính giá, không đổi dữ liệu nên bỏ antiforgery để gọi bằng fetch cho gọn
    [HttpPost, IgnoreAntiforgeryToken]
    public async Task<IActionResult> QuoteAjax([FromBody] QuoteBody? body)
    {
        if (body == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });
        CheckRules(body);
        if (!ModelState.IsValid)
        {
            var first = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
            return BadRequest(new { message = first ?? "Dữ liệu không hợp lệ." });
        }

        var r = await _api.QuoteAsync(body);
        return r.Ok ? Json(r.Data) : BadRequest(new { message = r.Error ?? "Không tính được cước." });
    }

    // ---------- Tạo đơn ----------
    [Authorize(Roles = AppRoles.AdminSellerCustomer)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var form = new ShipmentForm();
        await FillStationsAsync(form);
        return View(form);
    }

    [Authorize(Roles = AppRoles.AdminSellerCustomer)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShipmentForm form)
    {
        CheckRules(form);
        if (form.PickupMode == "Door" && string.IsNullOrWhiteSpace(form.SenderAddress))
            ModelState.AddModelError(nameof(form.SenderAddress), "Nhận tận nơi thì phải nhập địa chỉ người gửi.");
        if (form.DeliveryMode == "Door" && string.IsNullOrWhiteSpace(form.ReceiverAddress))
            ModelState.AddModelError(nameof(form.ReceiverAddress), "Giao tận nơi thì phải nhập địa chỉ người nhận.");
        if (form.FeePayer is not ("Sender" or "Receiver" or "Debt"))
            ModelState.AddModelError(nameof(form.FeePayer), "Người trả cước không hợp lệ.");
        if (form.FeePayer == "Debt" && !(User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Seller)))
            ModelState.AddModelError(nameof(form.FeePayer), "Chỉ nhân viên bán vé hoặc Admin mới được ghi nợ.");

        if (!ModelState.IsValid)
        {
            await FillStationsAsync(form);
            return View(form);
        }

        var r = await _api.CreateAsync(form);
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Không tạo được đơn hàng.");
            await FillStationsAsync(form);
            return View(form);
        }

        _logger.LogInformation("{User} tạo đơn hàng {Code}", User.Identity?.Name, r.Data?.Code);
        TempData["Success"] = $"Đã tạo đơn hàng {r.Data!.Code}.";
        return RedirectToAction(nameof(Track), new { code = r.Data.Code });
    }

    private async Task FillStationsAsync(ShipmentForm form)
    {
        var (stations, error) = await StationsAsync();
        form.Stations = stations;
        if (error != null) ModelState.AddModelError("", error);
    }

    // ---------- Tra cứu (không cần đăng nhập) ----------
    [HttpGet]
    public async Task<IActionResult> Track(string? code)
    {
        var vm = new TrackPageModel { Code = code?.Trim() };
        if (string.IsNullOrWhiteSpace(vm.Code)) return View(vm);

        var r = await _api.TrackAsync(vm.Code);
        if (r.Ok) vm.Result = r.Data;
        else vm.Error = r.Error ?? "Không tra cứu được đơn hàng.";
        return View(vm);
    }

    // ---------- Đơn hàng của tôi ----------
    [Authorize(Roles = AppRoles.AdminSellerCustomer)]
    [HttpGet]
    public async Task<IActionResult> Mine(string? status, int page = 1)
    {
        var vm = new ShipmentListViewModel { Status = status };
        var r = await _api.MineAsync(status, Math.Max(page, 1), PageSize);
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

    [Authorize(Roles = AppRoles.AdminSellerCustomer)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var r = await _api.CancelAsync(id);
        if (!r.Ok) TempData["Error"] = r.Error ?? "Không hủy được đơn hàng.";
        else
        {
            _logger.LogInformation("{User} hủy đơn hàng {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã hủy đơn hàng.";
        }
        return RedirectToAction(nameof(Mine));
    }
}