using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HeThongVanTai.Web.Infrastructure;

/// <summary>Kiểm tra Web gọi được API (GET {ApiBaseUrl}/health) cho endpoint /health của Web.</summary>
public class ApiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _factory;

    public ApiHealthCheck(IHttpClientFactory factory) => _factory = factory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(4));
            var http = _factory.CreateClient("api-refresh");   // client không gắn Bearer, địa chỉ gốc là ApiBaseUrl
            using var response = await http.GetAsync("health", cts.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("API trả lời bình thường")
                : HealthCheckResult.Unhealthy($"API trả mã {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Không gọi được API", ex);
        }
    }
}

public static class HealthResponse
{
    // Kết quả JSON gồm trạng thái và từng thành phần; không có chi tiết lỗi
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                ms = (int)e.Value.Duration.TotalMilliseconds
            })
        });
    }
}
