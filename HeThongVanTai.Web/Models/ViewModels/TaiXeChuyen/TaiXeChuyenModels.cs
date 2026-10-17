namespace HeThongVanTai.Web.Models.ViewModels.TaiXeChuyen;

// Dữ liệu từ /api/trips/mine: KHÔNG có giá vé hay thanh toán (tài xế không được xem tiền)
public class DriverTripItem
{
    public int Id { get; set; }
    public string RouteName { get; set; } = "";
    public string? FromStation { get; set; }
    public string? ToStation { get; set; }
    public string VehiclePlate { get; set; } = "";
    public DateTime DepartAt { get; set; }
    public DateTime? ArriveAt { get; set; }
    public string Status { get; set; } = "";
    public int TotalSeats { get; set; }
    public int BookedSeats { get; set; }
}

public class DriverTripList
{
    public string? DriverName { get; set; }
    public List<DriverTripItem> Items { get; set; } = new();
}

public class PassengerItem
{
    public string SeatCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public string TicketCode { get; set; } = "";
    public string Status { get; set; } = "";
}

public class DriverTripDetail
{
    public int Id { get; set; }
    public string RouteName { get; set; } = "";
    public string? FromStation { get; set; }
    public string? ToStation { get; set; }
    public string VehiclePlate { get; set; } = "";
    public DateTime DepartAt { get; set; }
    public DateTime? ArriveAt { get; set; }
    public string Status { get; set; } = "";
    public int TotalSeats { get; set; }
    public List<PassengerItem> Passengers { get; set; } = new();
}

public class DriverTripsModel
{
    public string? DriverName { get; set; }
    public List<DriverTripItem> Items { get; set; } = new();
    public bool IncludePast { get; set; }
    public string? Error { get; set; }
    public bool NotLinked { get; set; }
}
