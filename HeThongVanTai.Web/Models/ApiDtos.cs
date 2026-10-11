namespace HeThongVanTai.Web.Models;

// Dữ liệu API trả về (tên thuộc tính không phân biệt hoa thường)
public class LoginResponse
{
    public string JwtToken { get; set; } = "";
}

public class MeResponse
{
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
}
