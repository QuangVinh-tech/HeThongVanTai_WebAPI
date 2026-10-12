using System.Text;
using System.Text.Json;

namespace HeThongVanTai.Web.Infrastructure;

public class ApiResult<T>
{
    public bool Ok { get; init; }
    public int Status { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Client dùng chung để gọi API. Controller chỉ gọi ApiClient, không tự tạo HttpClient
/// và không viết cứng địa chỉ API. Token của người dùng được BearerTokenHandler gắn tự động.
/// </summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, ILogger<ApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url, null);
    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) => SendAsync<T>(HttpMethod.Post, url, body);
    public Task<ApiResult<T>> PutAsync<T>(string url, object? body = null) => SendAsync<T>(HttpMethod.Put, url, body);
    public Task<ApiResult<T>> PatchAsync<T>(string url, object? body = null) => SendAsync<T>(HttpMethod.Patch, url, body);
    public Task<ApiResult<T>> DeleteAsync<T>(string url) => SendAsync<T>(HttpMethod.Delete, url, null);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                // Chỉ ghi phương thức, địa chỉ và mã lỗi; không ghi nội dung gửi đi (có thể chứa mật khẩu)
                if (status >= 500) _logger.LogError("API lỗi {Status}: {Method} {Url}", status, method, url);
                else _logger.LogWarning("API từ chối {Status}: {Method} {Url}", status, method, url);
                return new ApiResult<T> { Ok = false, Status = status, Error = ExtractError(text, status) };
            }
            return new ApiResult<T> { Ok = true, Status = status, Data = Parse<T>(text) };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được tới API: {Method} {Url}", method, url);
            return Fail<T>("Không kết nối được tới API. Hãy kiểm tra API đã chạy chưa.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "API phản hồi quá lâu: {Method} {Url}", method, url);
            return Fail<T>("API phản hồi quá lâu. Vui lòng thử lại.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "API trả về dữ liệu sai định dạng: {Method} {Url}", method, url);
            return Fail<T>("API trả về dữ liệu không đúng định dạng.");
        }
    }

    private static ApiResult<T> Fail<T>(string message) => new() { Ok = false, Status = 0, Error = message };

    private static T? Parse<T>(string text)
    {
        if (typeof(T) == typeof(string)) return (T)(object)text;
        if (string.IsNullOrWhiteSpace(text)) return default;
        return JsonSerializer.Deserialize<T>(text, Json);
    }

    // API có nhiều kiểu trả lỗi: chuỗi thường, chuỗi JSON, mảng, {message}, ProblemDetails, lỗi validate {errors}
    private static string ExtractError(string text, int status)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return status switch
            {
                401 => "Bạn cần đăng nhập.",
                403 => "Bạn không có quyền thực hiện thao tác này.",
                404 => "Không tìm thấy dữ liệu.",
                _ => $"API trả về lỗi {status}."
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            switch (root.ValueKind)
            {
                case JsonValueKind.String:
                    return root.GetString() ?? $"API trả về lỗi {status}.";
                case JsonValueKind.Array:
                    return string.Join(" ", root.EnumerateArray().Select(e => e.ToString()));
                case JsonValueKind.Object:
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = errors.EnumerateObject()
                            .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                            .SelectMany(p => p.Value.EnumerateArray().Select(e => e.ToString()));
                        return string.Join(" ", messages);
                    }
                    foreach (var name in new[] { "message", "detail", "title" })
                    {
                        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                        {
                            return value.GetString() ?? $"API trả về lỗi {status}.";
                        }
                    }
                    break;
            }
        }
        catch (JsonException)
        {
            // Không phải JSON: API trả về chuỗi thường
            return text.Length > 300 ? text[..300] : text;
        }

        return $"API trả về lỗi {status}.";
    }
}
