using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.NhatKy;
using HeThongVanTai.Web.Models.ViewModels.XeTaiXe;

namespace HeThongVanTai.Web.Services;

public class AuditLogsApiService
{
    public const int PageSize = 20;
    private readonly ApiClient _api;
    public AuditLogsApiService(ApiClient api) => _api = api;

    public Task<ApiResult<PagedResult<AuditLogItem>>> GetAsync(AuditFilter f)
    {
        var parts = new List<string> { "pageNumber=" + f.Page, "pageSize=" + PageSize };
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) parts.Add(key + "=" + Uri.EscapeDataString(value.Trim()));
        }
        Add("keyword", f.Keyword);
        Add("entityType", f.EntityType);
        Add("action", f.Operation);
        Add("from", f.From?.ToString("yyyy-MM-dd"));
        Add("to", f.To?.ToString("yyyy-MM-dd"));
        return _api.GetAsync<PagedResult<AuditLogItem>>("api/AuditLogs?" + string.Join("&", parts));
    }
}
