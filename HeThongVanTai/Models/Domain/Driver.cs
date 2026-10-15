using System;
using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class Driver
    {
        [Key] public int Id { get; set; }
        [Required] public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? LicenseNo { get; set; }
        public string? LicenseClass { get; set; }
        public DateTime? LicenseExpiry { get; set; }
        public string Status { get; set; } = "Active";
        public string? AvatarUrl { get; set; }

        // Id tài khoản đăng nhập (AspNetUsers.Id) của tài xế; null nếu tài xế chưa có tài khoản.
        // Chỉ đặt qua trang Người dùng của Admin, không nhận từ client khi tạo hoặc sửa hồ sơ.
        [System.ComponentModel.DataAnnotations.MaxLength(450)]
        public string? UserId { get; set; }
    }
}