using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.Users;

namespace HeThongVanTai.Web.Services;

/// <summary>Gọi API quản lý người dùng (chỉ Admin).</summary>
public class UsersApiService
{
    private readonly ApiClient _api;

    public UsersApiService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<UserPage>> GetPageAsync(string? keyword, string? role, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add("keyword=" + Uri.EscapeDataString(keyword.Trim()));
        if (!string.IsNullOrWhiteSpace(role)) query.Add("role=" + Uri.EscapeDataString(role));
        return _api.GetAsync<UserPage>("api/Users?" + string.Join("&", query));
    }

    public Task<ApiResult<UserItem>> GetAsync(string id) =>
        _api.GetAsync<UserItem>($"api/Users/{Uri.EscapeDataString(id)}");

    public Task<ApiResult<UserItem>> UpdateRolesAsync(string id, IEnumerable<string> roles) =>
        _api.PutAsync<UserItem>($"api/Users/{Uri.EscapeDataString(id)}/roles", new { roles = roles.ToArray() });

    public Task<ApiResult<UserItem>> SetLockAsync(string id, bool locked) =>
        _api.PatchAsync<UserItem>($"api/Users/{Uri.EscapeDataString(id)}/lock", new { locked });
}
