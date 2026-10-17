namespace HeThongVanTai.Web.Models;

// Dữ liệu API trả về (tên thuộc tính không phân biệt hoa thường)
public class LoginResponse
{
    public string JwtToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshExpiresAt { get; set; }
}

public class MeResponse
{
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
}

// API trả về sau khi đăng ký
public class RegisterResponse
{
    public string Message { get; set; } = "";
    public bool RequiresVerification { get; set; }
    public bool EmailSent { get; set; }
}

public class MessageResponse
{
    public string Message { get; set; } = "";
}
