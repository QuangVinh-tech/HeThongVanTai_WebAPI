using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.DTO;

public class CreateSeatChangeDto
{
    [Range(1, int.MaxValue)] public int TicketId { get; set; }
    [Required] public string NewSeatCode { get; set; } = "";
    [MaxLength(300)] public string? Reason { get; set; }
}

public class ApproveSeatChangeDto
{
    [MaxLength(300)] public string? Note { get; set; }
}

public class RejectSeatChangeDto
{
    [Required, MaxLength(300)] public string Note { get; set; } = "";
}

public class SeatChangeItemDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int TripId { get; set; }
    public string TicketCode { get; set; } = "";
    public string BookingCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public string? CustomerName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Route { get; set; }
    public DateTime? DepartAt { get; set; }
    /// <summary>Ghế hiện tại của vé (có thể khác FromSeat nếu nhân viên đã đổi ghế sau khi khách gửi yêu cầu).</summary>
    public string? CurrentSeat { get; set; }
    public string FromSeat { get; set; } = "";
    public string ToSeat { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    /// <summary>Chỉ có giá trị với yêu cầu đang chờ: ghế mới hiện còn trống hay không.</summary>
    public bool? ToSeatAvailable { get; set; }
}

public class SeatChangeListDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int PendingCount { get; set; }
    public List<SeatChangeItemDto> Items { get; set; } = new();
}
