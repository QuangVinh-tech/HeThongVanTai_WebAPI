using System.Globalization;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.Reports;

namespace HeThongVanTai.Web.Services;

/// <summary>Gọi API báo cáo (Admin, Accountant).</summary>
public class ReportsApiService
{
    private readonly ApiClient _api;

    public ReportsApiService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<RevenuePage>> GetRevenueByRouteAsync(ReportFilter f, int pageSize, bool all = false)
    {
        var query = new List<string>
        {
            "from=" + f.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "to=" + f.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "sortBy=" + Uri.EscapeDataString(f.Sort),
            "desc=" + (f.Desc ? "true" : "false"),
            "page=" + f.Page,
            "pageSize=" + pageSize,
            "all=" + (all ? "true" : "false")
        };
        if (f.RouteId.HasValue) query.Add("routeId=" + f.RouteId.Value);
        return _api.GetAsync<RevenuePage>("api/Reports/get-revenue-by-route?" + string.Join("&", query));
    }
}
