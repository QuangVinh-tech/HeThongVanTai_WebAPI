using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    /// <summary>Refresh token của một phiên đăng nhập. Chỉ lưu bản băm SHA-256, không lưu token gốc.</summary>
    public class RefreshToken
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(450)] public string UserId { get; set; } = "";
        [Required, MaxLength(64)] public string TokenHash { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        // Băm của token thay thế khi xoay vòng (mỗi lần làm mới, token cũ bị thu hồi và cấp token mới)
        [MaxLength(64)] public string? ReplacedByHash { get; set; }
    }
}
