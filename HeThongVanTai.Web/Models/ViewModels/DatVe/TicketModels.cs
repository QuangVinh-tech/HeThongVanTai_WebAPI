namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

// Khớp JSON của GET /api/tickets/lookup/{code}
public class TicketInfoDto
{
    public string TicketCode { get; set; } = "";
    public string Status { get; set; } = "";
    public string SeatCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public decimal Price { get; set; }
    public string BookingCode { get; set; } = "";
    public string BookingStatus { get; set; } = "";
    public string? Customer { get; set; }
    public string Route { get; set; } = "";
    public DateTime DepartAt { get; set; }
    public string? Plate { get; set; }
}

public class LookupViewModel
{
    public string? Code { get; set; }
    public string? Error { get; set; }
    public BookingDto? Booking { get; set; }   // tra theo mã đặt vé thì có danh sách vé
}

public class TicketViewModel
{
    public TicketInfoDto Ticket { get; set; } = new();
    public string QrUrl { get; set; } = "";
}