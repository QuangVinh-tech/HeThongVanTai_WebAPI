using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.KhachHang;

namespace HeThongVanTai.Web.Services;

// Gọi /api/Customers (Admin, Seller)
public class CustomersApiService
{
    public const int PageSize = 10;
    private readonly ApiClient _api;

    public CustomersApiService(ApiClient api) => _api = api;

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public Task<ApiResult<CustomerPage>> GetPageAsync(string? keyword, int page)
    {
        var url = $"api/Customers?page={page}&pageSize={PageSize}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += "&keyword=" + Uri.EscapeDataString(keyword.Trim());
        return _api.GetAsync<CustomerPage>(url);
    }

    public Task<ApiResult<CustomerItem>> GetAsync(int id) => _api.GetAsync<CustomerItem>($"api/Customers/{id}");

    public Task<ApiResult<List<HistoryBooking>>> GetHistoryAsync(int id) =>
        _api.GetAsync<List<HistoryBooking>>($"api/Customers/{id}/history");

    private static object Body(CustomerForm f) => new { fullName = f.FullName.Trim(), phone = Clean(f.Phone), email = Clean(f.Email) };

    public Task<ApiResult<string>> CreateAsync(CustomerForm f) => _api.PostAsync<string>("api/Customers", Body(f));

    public Task<ApiResult<string>> UpdateAsync(int id, CustomerForm f) => _api.PutAsync<string>($"api/Customers/{id}", Body(f));

    public Task<ApiResult<string>> DeleteAsync(int id) => _api.DeleteAsync<string>($"api/Customers/{id}");
}
