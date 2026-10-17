using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace HeThongVanTai.Web.Infrastructure;

/// <summary>Tự gắn "Authorization: Bearer {token}" vào mọi lời gọi API của người đang đăng nhập.</summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;

    public BearerTokenHandler(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _accessor.HttpContext;
        if (context != null)
        {
            // API giới hạn số lần đăng nhập theo IP: chuyển địa chỉ của khách thật, không phải của máy chủ Web
            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(clientIp))
            {
                request.Headers.Remove("X-Forwarded-For");
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
            }

            var token = await context.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
