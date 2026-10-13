using System.Globalization;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Tìm chuyến (/Trips/Search) và chọn ghế (/Trips/{id}/Seats). Ai cũng xem được.</summary>
[AllowAnonymous]
public class TripsController : Controller
{
    private readonly ApiClient _api;
    private readonly DanhMucApiService _danhMuc;

    public TripsController(ApiClient api, DanhMucApiService danhMuc)
    {
        _api = api;
        _danhMuc = danhMuc;
    }

    // ---------- #18 Tìm chuyến ----------
    [HttpGet("Trips/Search")]
    public async Task<IActionResult> Search([FromQuery] TripSearchFilter filter)
    {
        filter.Date ??= DateTime.Today;
        filter.Tickets = Math.Clamp(filter.Tickets, 1, 10);
        filter.Page = Math.Max(filter.Page, 1);
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 50);

        var vm = new TripSearchViewModel { Filter = filter };

        var stations = await _danhMuc.GetStationsAsync(null, null);
        if (stations.Ok) vm.Stations = stations.Data ?? new();
        else vm.Error = stations.Error;

        var types = await _danhMuc.GetVehicleTypesAsync();
        if (types.Ok) vm.VehicleTypes = types.Data ?? new();

        // Chưa chọn đủ bến đi và bến đến thì chỉ hiện form
        if (filter.FromStationId is null || filter.ToStationId is null) return View(vm);

        if (filter.FromStationId == filter.ToStationId)
        {
            vm.Error = "Bến đi và bến đến không được trùng nhau.";
            return View(vm);
        }
        if (filter.Date.Value.Date < DateTime.Today)
        {
            vm.Error = "Ngày đi không được ở quá khứ.";
            return View(vm);
        }

        var result = await _api.GetAsync<TripSearchResult>(BuildSearchUrl(filter));
        if (result.Ok) vm.Result = result.Data ?? new TripSearchResult();
        else vm.Error = result.Error;

        return View(vm);
    }

    // ---------- #19 Chọn ghế ----------
    [HttpGet("Trips/{id:int}/Seats")]
    public async Task<IActionResult> Seats(int id, int tickets = 1)
    {
        var r = await _api.GetAsync<SeatMapDto>($"api/trips/{id}/seats");
        if (!r.Ok || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Không tải được sơ đồ ghế của chuyến này.";
            return RedirectToAction(nameof(Search));
        }
        return View(new SeatsViewModel { Map = r.Data, Tickets = Math.Clamp(tickets, 1, 10) });
    }

    // Ghép địa chỉ gọi API (không có dấu "/" ở đầu vì ApiClient đã có địa chỉ gốc)
    private static string BuildSearchUrl(TripSearchFilter f)
    {
        var inv = CultureInfo.InvariantCulture;
        var q = new List<string>
        {
            $"fromStationId={f.FromStationId}",
            $"toStationId={f.ToStationId}",
            $"date={f.Date!.Value.ToString("yyyy-MM-dd", inv)}",
            $"page={f.Page}",
            $"pageSize={f.PageSize}"
        };
        if (f.FromHour.HasValue) q.Add($"fromHour={f.FromHour}");
        if (f.ToHour.HasValue) q.Add($"toHour={f.ToHour}");
        if (f.VehicleTypeId.HasValue) q.Add($"vehicleTypeId={f.VehicleTypeId}");
        if (f.MinPrice.HasValue) q.Add($"minPrice={f.MinPrice.Value.ToString(inv)}");
        if (f.MaxPrice.HasValue) q.Add($"maxPrice={f.MaxPrice.Value.ToString(inv)}");
        if (!string.IsNullOrWhiteSpace(f.Sort)) q.Add($"sort={Uri.EscapeDataString(f.Sort)}");
        return "api/trips/search?" + string.Join("&", q);
    }
}