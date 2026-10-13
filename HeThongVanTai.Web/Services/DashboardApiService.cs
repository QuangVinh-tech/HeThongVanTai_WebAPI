using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.Dashboard;

namespace HeThongVanTai.Web.Services;

/// <summary>Gọi API Dashboard: chỉ số tổng quan và cảnh báo.</summary>
public class DashboardApiService
{
    private readonly ApiClient _api;

    public DashboardApiService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<DashboardSummary>> GetSummaryAsync(int days = 7) =>
        _api.GetAsync<DashboardSummary>($"api/Dashboard/get-summary?days={days}");

    public Task<ApiResult<List<DashboardAlert>>> GetAlertsAsync(int days = 30) =>
        _api.GetAsync<List<DashboardAlert>>($"api/Dashboard/get-alerts?days={days}");
}
