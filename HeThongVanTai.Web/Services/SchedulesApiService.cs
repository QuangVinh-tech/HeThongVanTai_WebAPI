using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.LichChay;
using HeThongVanTai.Web.Models.ViewModels.XeTaiXe;

namespace HeThongVanTai.Web.Services;

// Lịch chạy cố định: /api/trips/generate-schedule (+ /preview), danh sách xe và tài xế đang hoạt động
public class SchedulesApiService
{
    private readonly ApiClient _api;
    public SchedulesApiService(ApiClient api) => _api = api;

    public Task<ApiResult<PagedResult<VehicleItem>>> GetActiveVehiclesAsync() =>
        _api.GetAsync<PagedResult<VehicleItem>>("api/vehicles?status=Active&sortBy=Plate&pageNumber=1&pageSize=200");

    public Task<ApiResult<PagedResult<DriverItem>>> GetActiveDriversAsync() =>
        _api.GetAsync<PagedResult<DriverItem>>("api/drivers?status=Active&sortBy=FullName&pageNumber=1&pageSize=200");

    private static object Body(ScheduleForm f) => new
    {
        busRouteId = f.BusRouteId,
        vehicleId = f.VehicleId,
        driverId = f.DriverId,
        fromDate = f.FromDate,
        toDate = f.ToDate,
        departureTime = f.DepartureTime,
        daysOfWeek = f.DaysOfWeek.Distinct().ToList(),
        price = f.Price ?? 0
    };

    public Task<ApiResult<PlanResult>> PreviewAsync(ScheduleForm f) =>
        _api.PostAsync<PlanResult>("api/trips/generate-schedule/preview", Body(f));

    public Task<ApiResult<string>> CreateAsync(ScheduleForm f) =>
        _api.PostAsync<string>("api/trips/generate-schedule", Body(f));
}
