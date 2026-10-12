using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;

namespace HeThongVanTai.Web.Services;

/// <summary>Gọi API Danh mục: bến xe, tuyến đường, loại xe và sơ đồ ghế.</summary>
public class DanhMucApiService
{
    private readonly ApiClient _api;

    public DanhMucApiService(ApiClient api)
    {
        _api = api;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ===== Bến xe =====
    public Task<ApiResult<List<StationItem>>> GetStationsAsync(string? keyword, string? city)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add("keyword=" + Uri.EscapeDataString(keyword.Trim()));
        if (!string.IsNullOrWhiteSpace(city)) query.Add("city=" + Uri.EscapeDataString(city.Trim()));
        var url = "api/stations" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return _api.GetAsync<List<StationItem>>(url);
    }

    public Task<ApiResult<StationItem>> GetStationAsync(int id) =>
        _api.GetAsync<StationItem>($"api/stations/{id}");

    public Task<ApiResult<string>> CreateStationAsync(StationForm form) =>
        _api.PostAsync<string>("api/stations", new { name = form.Name.Trim(), city = Clean(form.City), address = Clean(form.Address) });

    public Task<ApiResult<string>> UpdateStationAsync(int id, StationForm form) =>
        _api.PutAsync<string>($"api/stations/{id}", new { name = form.Name.Trim(), city = Clean(form.City), address = Clean(form.Address) });

    public Task<ApiResult<string>> DeleteStationAsync(int id) =>
        _api.DeleteAsync<string>($"api/stations/{id}");

    // ===== Tuyến đường =====
    public Task<ApiResult<List<RouteItem>>> GetRoutesAsync() =>
        _api.GetAsync<List<RouteItem>>("api/routes");

    public Task<ApiResult<RouteItem>> GetRouteAsync(int id) =>
        _api.GetAsync<RouteItem>($"api/routes/{id}");

    private static object RouteBody(RouteForm f) => new
    {
        name = f.Name.Trim(),
        fromStationId = f.FromStationId,
        toStationId = f.ToStationId,
        distanceKm = f.DistanceKm ?? 0,
        durationMin = f.DurationMin ?? 0
    };

    public Task<ApiResult<string>> CreateRouteAsync(RouteForm form) =>
        _api.PostAsync<string>("api/routes", RouteBody(form));

    public Task<ApiResult<string>> UpdateRouteAsync(int id, RouteForm form) =>
        _api.PutAsync<string>($"api/routes/{id}", RouteBody(form));

    public Task<ApiResult<string>> DeleteRouteAsync(int id) =>
        _api.DeleteAsync<string>($"api/routes/{id}");

    // ===== Loại xe và sơ đồ ghế =====
    public Task<ApiResult<List<VehicleTypeItem>>> GetVehicleTypesAsync() =>
        _api.GetAsync<List<VehicleTypeItem>>("api/vehicle-types");

    public Task<ApiResult<VehicleTypeItem>> GetVehicleTypeAsync(int id) =>
        _api.GetAsync<VehicleTypeItem>($"api/vehicle-types/{id}");

    public Task<ApiResult<string>> CreateVehicleTypeAsync(VehicleTypeForm form) =>
        _api.PostAsync<string>("api/vehicle-types", new
        {
            name = form.Name.Trim(),
            seatCount = form.SeatCount ?? 0,
            floors = form.Floors
        });

    public Task<ApiResult<string>> UpdateSeatsAsync(int id, IEnumerable<SeatItem> seats) =>
        _api.PutAsync<string>($"api/vehicle-types/{id}/seats",
            seats.Select(s => new { seatCode = s.SeatCode, floor = s.Floor }).ToList());

    public Task<ApiResult<string>> DeleteVehicleTypeAsync(int id) =>
        _api.DeleteAsync<string>($"api/vehicle-types/{id}");
}
