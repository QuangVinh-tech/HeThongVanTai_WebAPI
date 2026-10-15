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
    }

    public class LoginResponseDTO
    {
        public string JwtToken { get; set; } = "";
    }
}