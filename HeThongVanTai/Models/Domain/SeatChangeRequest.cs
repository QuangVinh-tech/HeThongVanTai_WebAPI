using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Models.Domain;

/// <summary>
/// Yêu cầu đổi ghế của khách (trong cùng một chuyến). Khách gửi, Admin xác nhận hoặc từ chối.
/// Chỉ khi Admin xác nhận và ghế mới còn trống thì vé mới thật sự đổi ghế.
/// </summary>
[Index(nameof(TicketId), nameof(Status))]
[Index(nameof(Status), nameof(RequestedAt))]
public class SeatChangeRequest
{
    [Key] public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    /// <summary>Chuyến của vé lúc gửi yêu cầu (không đặt khóa ngoại để tránh xóa dây chuyền).</summary>
    public int TripId { get; set; }

    [Required, MaxLength(20)] public string FromSeat { get; set; } = "";
    [Required, MaxLength(20)] public string ToSeat { get; set; } = "";
    [MaxLength(300)] public string? Reason { get; set; }

    /// <summary>Pending (chờ xác nhận), Approved (đã xác nhận), Rejected (từ chối), Cancelled (khách tự hủy).</summary>
    [Required, MaxLength(20)] public string Status { get; set; } = "Pending";

    [MaxLength(450)] public string? RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.Now;

    [MaxLength(256)] public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(300)] public string? ReviewNote { get; set; }
}
