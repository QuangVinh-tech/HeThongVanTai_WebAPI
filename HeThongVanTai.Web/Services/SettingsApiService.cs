using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.CaiDat;
using Microsoft.Extensions.Caching.Memory;

namespace HeThongVanTai.Web.Services;

/// <summary>
/// Đọc cài đặt công khai (tên công ty, hotline, email, mốc hoàn tiền) cho chân trang, trang Liên hệ và Chính sách.
/// Cache ngắn để không gọi API ở mọi trang; Admin lưu xong thì xóa cache nên đổi có hiệu lực ngay.
/// API lỗi thì dùng giá trị mặc định, không làm hỏng trang.
/// </summary>
public class SettingsApiService
{
    private const string CacheKey = "public-settings";
    private readonly ApiClient _api;
    private readonly IMemoryCache _cache;

    public SettingsApiService(ApiClient api, IMemoryCache cache)
    {
        _api = api;
        _cache = cache;
    }

    public async Task<PublicSettings> GetPublicAsync()
    {
        if (_cache.TryGetValue(CacheKey, out PublicSettings? cached) && cached != null) return cached;

        var r = await _api.GetAsync<PublicSettings>("api/settings/public");
        if (r.Ok && r.Data != null)
        {
            _cache.Set(CacheKey, r.Data, TimeSpan.FromSeconds(60));
            return r.Data;
        }

        var fallback = new PublicSettings();
        _cache.Set(CacheKey, fallback, TimeSpan.FromSeconds(15));   // tránh gọi lại liên tục khi API đang lỗi
        return fallback;
    }

    public void Invalidate() => _cache.Remove(CacheKey);
}
