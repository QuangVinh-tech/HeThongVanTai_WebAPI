using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    // Nhật ký thao tác: ai, lúc nào, làm gì, trên dữ liệu nào.
    // KHÔNG ghi mật khẩu, token hay nội dung nhạy cảm vào Detail.
    public class AuditLog
    {
        [Key] public int Id { get; set; }
        public DateTime CreatedAt { get; set; }

        [MaxLength(450)] public string? UserId { get; set; }
        [Required, MaxLength(256)] public string UserName { get; set; } = "";

        // Create, Update, Delete, Cancel, Refund, ChangeRole, Lock, Unlock, Status, GenerateSchedule...
        [Required, MaxLength(50)] public string Action { get; set; } = "";

        // Vehicle, Driver, Trip, BusRoute, Promotion, UserRole, Booking
        [Required, MaxLength(50)] public string EntityType { get; set; } = "";
        [MaxLength(50)] public string? EntityId { get; set; }
        [MaxLength(1000)] public string? Detail { get; set; }
    }
}
