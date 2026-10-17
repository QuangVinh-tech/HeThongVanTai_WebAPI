using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.DTO
{
    public class RegisterRequestDTO
    {
        [Required][EmailAddress] public string Username { get; set; } = "";
        [Required][MinLength(6)] public string Password { get; set; } = "";
        public string[] Roles { get; set; } = Array.Empty<string>();
    }

    public class LoginRequestDTO
    {
        [Required][EmailAddress] public string Username { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    public class ChangePasswordDTO
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        public string NewPassword { get; set; } = "";

        // Refresh token của phiên đang dùng: được giữ lại, mọi phiên khác bị thu hồi sau khi đổi mật khẩu
        public string? RefreshToken { get; set; }
    }

    public class LoginResponseDTO
    {
        public string JwtToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime RefreshExpiresAt { get; set; }
    }

    public class RefreshRequestDTO
    {
        [Required] public string RefreshToken { get; set; } = "";
    }
}