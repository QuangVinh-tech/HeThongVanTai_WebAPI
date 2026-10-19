namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

/// <summary>Một yêu cầu đổi ghế do API trả về (dùng cho trang Vé của tôi và trang quản trị).</summary>
public class SeatChangeItem
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
    public string? CurrentSeat { get; set; }
    public string FromSeat { get; set; } = "";
    public string ToSeat { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public bool? ToSeatAvailable { get; set; }
}

public class SeatChangeList
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int PendingCount { get; set; }
    public List<SeatChangeItem> Items { get; set; } = new();
}

public class AdminSeatChangesViewModel
{
    public SeatChangeList Result { get; set; } = new();
    /// <summary>Pending, Approved, Rejected, Cancelled hoặc All.</summary>
    public string Status { get; set; } = "Pending";
}

public static class SeatChangeText
{
    public static string Status(string status) => status switch
    {
        "Pending" => "Chờ xác nhận",
        "Approved" => "Đã xác nhận",
        "Rejected" => "Bị từ chối",
        "Cancelled" => "Đã hủy",
        _ => status
    };

    public static string BadgeClass(string status) => status switch
    {
        "Pending" => "text-bg-warning",
        "Approved" => "text-bg-success",
        "Rejected" => "text-bg-danger",
        _ => "text-bg-secondary"
    };
}
