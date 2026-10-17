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

    private static string Date(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public Task<ApiResult<BreakdownPage>> GetBreakdownAsync(string by, ReportFilter f, int pageSize, bool all = false)
    {
        var query = new List<string>
        {
            "by=" + Uri.EscapeDataString(by),
            "from=" + Date(f.From),
            "to=" + Date(f.To),
            "sortBy=" + Uri.EscapeDataString(f.Sort),
            "desc=" + (f.Desc ? "true" : "false"),
            "page=" + f.Page,
            "pageSize=" + pageSize,
            "all=" + (all ? "true" : "false")
        };
        if (f.RouteId.HasValue) query.Add("routeId=" + f.RouteId.Value);
        return _api.GetAsync<BreakdownPage>("api/Reports/revenue-by?" + string.Join("&", query));
    }

    public Task<ApiResult<DebtPage>> GetDebtAsync(DateTime? from, DateTime? to, string? feePayer, int page, int pageSize, bool all = false)
    {
        var query = new List<string> { "page=" + page, "pageSize=" + pageSize, "all=" + (all ? "true" : "false") };
        if (from.HasValue) query.Add("from=" + Date(from.Value));
        if (to.HasValue) query.Add("to=" + Date(to.Value));
        if (!string.IsNullOrWhiteSpace(feePayer)) query.Add("feePayer=" + Uri.EscapeDataString(feePayer));
        return _api.GetAsync<DebtPage>("api/Reports/debt?" + string.Join("&", query));
    }

    public Task<ApiResult<ReconPage>> GetReconciliationAsync(DateTime from, DateTime to, bool onlyIssues, int page, int pageSize, bool all = false)
    {
        var query = new List<string>
        {
            "from=" + Date(from),
            "to=" + Date(to),
            "onlyIssues=" + (onlyIssues ? "true" : "false"),
            "page=" + page,
            "pageSize=" + pageSize,
            "all=" + (all ? "true" : "false")
        };
        return _api.GetAsync<ReconPage>("api/Reports/reconciliation?" + string.Join("&", query));
    }
}
