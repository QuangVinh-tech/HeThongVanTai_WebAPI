using System.Security.Claims;
using System.Text.Json;

namespace HeThongVanTai.Web.Infrastructure;

public record JwtInfo(ClaimsPrincipal Principal, DateTimeOffset ExpiresAt);

/// <summary>
/// Chỉ ĐỌC nội dung token để dựng danh tính (email, vai trò) trên Web.
/// Việc kiểm tra chữ ký và hạn của token là của API.
/// </summary>
public static class JwtReader
{
    public static JwtInfo? Read(string jwt, string scheme)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            using var doc = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var claims = new List<Claim>();
            var expires = DateTimeOffset.UtcNow.AddMinutes(30);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name == "exp")
                {
                    if (property.Value.TryGetInt64(out var seconds))
                    {
                        expires = DateTimeOffset.FromUnixTimeSeconds(seconds);
                    }
                    continue;
                }

                var type = MapType(property.Name);
                if (type == null) continue;

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        AddClaim(claims, type, item.GetString());
                    }
                }
                else
                {
                    AddClaim(claims, type, property.Value.GetString());
                }
            }

            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            if (email != null) claims.Add(new Claim(ClaimTypes.Name, email));

            var identity = new ClaimsIdentity(claims, scheme, ClaimTypes.Name, ClaimTypes.Role);
            return new JwtInfo(new ClaimsPrincipal(identity), expires);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // API tạo token với tên claim dạng đầy đủ (URI); chấp nhận cả tên rút gọn cho chắc chắn
    private static string? MapType(string name) => name switch
    {
        "role" or ClaimTypes.Role => ClaimTypes.Role,
        "email" or ClaimTypes.Email => ClaimTypes.Email,
        "nameid" or "sub" or ClaimTypes.NameIdentifier => ClaimTypes.NameIdentifier,
        _ => null
    };

    private static void AddClaim(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !claims.Any(c => c.Type == type && c.Value == value))
        {
            claims.Add(new Claim(type, value));
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
