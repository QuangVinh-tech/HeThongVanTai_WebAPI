using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using HeThongVanTai.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace HeThongVanTai.Web.Infrastructure;

/// <summary>
/// Access token của API sống ngắn (mặc định 15 phút). Trước khi sắp hết hạn, Web dùng refresh token trong cookie
/// để xin cặp token mới, nên người dùng không phải đăng nhập lại liên tục.
/// Nếu API từ chối (tài khoản bị khóa, phiên bị thu hồi, hết hạn) thì đăng xuất người dùng.
/// </summary>
public static class TokenRefresher
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";
    public const string AccessExpiresAt = "access_expires_at";

    // Nhiều request song song cùng thấy token sắp hết hạn: chỉ một request gọi API, các request còn lại dùng kết quả đó
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var props = context.Properties;
        var refresh = props.GetTokenValue(RefreshToken);
        if (string.IsNullOrEmpty(refresh)) return;   // cookie cũ chưa có refresh token: giữ nguyên

        var hasExpiry = DateTimeOffset.TryParse(props.GetTokenValue(AccessExpiresAt),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires);
        if (hasExpiry && expires > DateTimeOffset.UtcNow.AddSeconds(60)) return;   // còn hạn, chưa cần làm mới

        var services = context.HttpContext.RequestServices;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("TokenRefresher");
        var cache = services.GetRequiredService<IMemoryCache>();

        LoginResponse? tokens;
        await Gate.WaitAsync();
        try
        {
            if (!cache.TryGetValue(refresh, out tokens))
            {
                var http = services.GetRequiredService<IHttpClientFactory>().CreateClient("api-refresh");
                using var response = await http.PostAsJsonAsync("api/Auth/refresh", new { refreshToken = refresh });
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Làm mới phiên đăng nhập bị từ chối (API trả {Status}), yêu cầu đăng nhập lại", (int)response.StatusCode);
                    await RejectAsync(context);
                    return;
                }

                tokens = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
                if (tokens == null || string.IsNullOrEmpty(tokens.JwtToken) || string.IsNullOrEmpty(tokens.RefreshToken))
                {
                    await RejectAsync(context);
                    return;
                }
                cache.Set(refresh, tokens, TimeSpan.FromSeconds(30));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // API tạm thời không trả lời: giữ phiên hiện tại, lần sau thử lại
            logger.LogWarning(ex, "Không làm mới được phiên đăng nhập do lỗi kết nối tới API");
            return;
        }
        finally
        {
            Gate.Release();
        }

        var info = JwtReader.Read(tokens!.JwtToken, CookieAuthenticationDefaults.AuthenticationScheme);
        if (info == null)
        {
            await RejectAsync(context);
            return;
        }

        props.UpdateTokenValue(AccessToken, tokens.JwtToken);
        props.UpdateTokenValue(RefreshToken, tokens.RefreshToken!);
        props.UpdateTokenValue(AccessExpiresAt, info.ExpiresAt.ToString("o", CultureInfo.InvariantCulture));
        context.ReplacePrincipal(info.Principal);   // vai trò mới (nếu admin vừa đổi) có hiệu lực từ đây
        context.ShouldRenew = true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
