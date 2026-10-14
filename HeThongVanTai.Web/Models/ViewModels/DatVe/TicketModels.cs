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
    public DateTime? BookingCreatedAt { get; set; }
    public string? Customer { get; set; }
    public string? ContactPhone { get; set; }
    public bool NeedTransfer { get; set; }
    public string? TransferAddress { get; set; }
    public string Route { get; set; } = "";
    public string? FromStation { get; set; }
    public string? FromAddress { get; set; }
    public string? ToStation { get; set; }
    public string? ToAddress { get; set; }
    public int DistanceKm { get; set; }
    public int DurationMin { get; set; }
    public DateTime DepartAt { get; set; }
    public DateTime? ArriveAt { get; set; }
    public string? Plate { get; set; }
    public string? VehicleType { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
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