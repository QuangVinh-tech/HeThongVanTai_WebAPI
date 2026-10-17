using HeThongVanTai.Web.Models.ViewModels.DanhMuc;

namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

// Kết quả phân trang của /api/trips, /api/vehicles, /api/drivers
public class ListResult<T>
{
    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new();
}

public class AdminTripItem
{
    public int Id { get; set; }
    public int BusRouteId { get; set; }
    public string RouteName { get; set; } = "";
    public string? FromStation { get; set; }
    public string? ToStation { get; set; }
    public int VehicleId { get; set; }
    public string VehiclePlate { get; set; } = "";
    public string? VehicleType { get; set; }
    public int DriverId { get; set; }
    public string? DriverName { get; set; }
    public DateTime DepartAt { get; set; }
    public DateTime? ArriveAt { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "";
    public int TotalSeats { get; set; }
    public int BookedSeats { get; set; }
}

public class VehicleOption
{
    public int Id { get; set; }
    public string Plate { get; set; } = "";
    public string? VehicleTypeName { get; set; }
    public int SeatCount { get; set; }
    public string Status { get; set; } = "";
}

public class DriverOption
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? LicenseExpiry { get; set; }
}

public class TripAdminFilter
{
    public int? BusRouteId { get; set; }
    public DateTime? Date { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }          // DepartAt | Price
    public bool IsAscending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class AdminTripsViewModel
{
    public TripAdminFilter Filter { get; set; } = new();
    public List<RouteItem> Routes { get; set; } = new();
    public List<VehicleOption> Vehicles { get; set; } = new();
    public List<DriverOption> Drivers { get; set; } = new();
    public ListResult<AdminTripItem>? Result { get; set; }
    public string? Error { get; set; }
}

public class CreateTripForm
{
    public int BusRouteId { get; set; }
    public int VehicleId { get; set; }
    public int DriverId { get; set; }
    public DateTime? DepartAt { get; set; }
    public DateTime? ArriveAt { get; set; }
    public decimal Price { get; set; }
}

public class CreateTripViewModel
{
    public CreateTripForm Form { get; set; } = new();
    public List<RouteItem> Routes { get; set; } = new();
    public List<VehicleOption> Vehicles { get; set; } = new();
    public List<DriverOption> Drivers { get; set; } = new();
}

// Nhãn trạng thái chuyến (class .pill của site.css)
public static class TripStatusHelper
{
    public static string Text(string status) => status switch
    {
        "Open" => "Mở bán",
        "Delayed" => "Hoãn",
        "Cancelled" => "Đã hủy",
        "Running" => "Đang chạy",
        "Completed" => "Đã hoàn thành",
        _ => status
    };

    public static string Class(string status) => status switch
    {
        "Open" => "pill-ok",
        "Delayed" => "pill-warn",
        "Cancelled" => "pill-bad",
        "Running" => "pill-info",
        _ => "pill-info"
    };
}