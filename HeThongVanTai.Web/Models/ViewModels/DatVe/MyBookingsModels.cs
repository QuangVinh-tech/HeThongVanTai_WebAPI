namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

public class BookingListResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<BookingDto> Items { get; set; } = new();
}

public class RefundPreviewDto
{
    public bool CanCancel { get; set; }
    public string? Reason { get; set; }
    public decimal RefundRate { get; set; }
    public decimal RefundAmount { get; set; }
}

public class CancelResultDto
{
    public int BookingId { get; set; }
    public string Status { get; set; } = "";
    public decimal RefundRate { get; set; }
    public decimal RefundAmount { get; set; }
}

public class ChangeResultDto
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = "";
    public int TripId { get; set; }
    public string SeatCode { get; set; } = "";
    public decimal Price { get; set; }
    public string Status { get; set; } = "";
}

public class MyBookingsViewModel
{
    public BookingListResult Result { get; set; } = new();
    /// <summary>Các yêu cầu đổi ghế của khách (để hiện trạng thái cạnh từng vé).</summary>
    public List<SeatChangeItem> SeatChanges { get; set; } = new();
}

public class CancelViewModel
{
    public BookingDto Booking { get; set; } = new();
    public RefundPreviewDto Preview { get; set; } = new();
}

public class ChangeSeatViewModel
{
    public int TicketId { get; set; }
    public int TripId { get; set; }
    public string CurrentSeat { get; set; } = "";
    public string? ReturnUrl { get; set; }   // trang quay lại sau khi đổi ghế (Vé của tôi hoặc chi tiết đặt vé của admin)
    public SeatMapDto Map { get; set; } = new();
}