using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.TaiXeChuyen;

namespace HeThongVanTai.Web.Services;

// Tài xế gọi /api/trips/mine, /api/trips/mine/{id}, PATCH /api/trips/{id}/status
public class DriverTripsApiService
{
    private readonly ApiClient _api;
    public DriverTripsApiService(ApiClient api) => _api = api;

    public Task<ApiResult<DriverTripList>> GetMineAsync(bool includePast) =>
        _api.GetAsync<DriverTripList>("api/trips/mine?includePast=" + (includePast ? "true" : "false"));

    public Task<ApiResult<DriverTripDetail>> GetDetailAsync(int id) =>
        _api.GetAsync<DriverTripDetail>($"api/trips/mine/{id}");

    public Task<ApiResult<string>> SetStatusAsync(int id, string status, DateTime? newDepartAt = null) =>
        _api.PatchAsync<string>($"api/trips/{id}/status", new { status, newDepartAt });
}
