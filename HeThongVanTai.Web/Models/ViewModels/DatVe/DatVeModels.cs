using System.Globalization;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;

namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

// Hàm dùng chung cho các trang đặt vé: định dạng tiền, nhãn trạng thái (dùng class .pill của site.css)
public static class DatVeHelpers
{
    private static readonly CultureInfo Vi = new("vi-VN");

    public static string Money(decimal value) => value.ToString("N0", Vi) + " đ";

    public static string BookingStatusText(string status) => status switch
    {
        "Holding" => "Chờ thanh toán",
        "Paid" => "Đã thanh toán",
        "Cancelled" => "Đã hủy",
        "Expired" => "Hết hạn giữ chỗ",
        _ => status
    };

    public static string BookingStatusClass(string status) => status switch
    {
        "Holding" => "pill-warn",
        "Paid" => "pill-ok",
        "Cancelled" => "pill-bad",
        _ => "pill-info"
    };

    public static string TicketStatusText(string status) => status switch
    {
        "Active" => "Còn hiệu lực",
        "CheckedIn" => "Đã soát vé",
        "Cancelled" => "Đã hủy",
        _ => status
    };

    public static string Duration(int minutes)
    {
        if (minutes <= 0) return "-";
        var h = minutes / 60; var m = minutes % 60;
        return h > 0 ? (m > 0 ? $"{h} giờ {m} phút" : $"{h} giờ") : $"{m} phút";
    }

    public static string PaymentMethodText(string method) => method switch
    {
        "Cash" => "Tiền mặt",
        "BankTransfer" => "Chuyển khoản",
        _ => method
    };
}

// ===== Tìm chuyến (#18): khớp JSON của GET /api/trips/search =====
public class TripItem
{
    public int TripId { get; set; }
    public string Route { get; set; } = "";
    public DateTime DepartAt { get; set; }
    public DateTime? ArriveAt { get; set; }
    public decimal Price { get; set; }
    public string Plate { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
}

public class TripSearchResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<TripItem> Items { get; set; } = new();
}

// Các ô lọc trên form (lấy từ query string)
public class TripSearchFilter
{
    public int? FromStationId { get; set; }
    public int? ToStationId { get; set; }
    public DateTime? Date { get; set; }
    public int Tickets { get; set; } = 1;
    public int? FromHour { get; set; }
    public int? ToHour { get; set; }
    public int? VehicleTypeId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class TripSearchViewModel
{
    public TripSearchFilter Filter { get; set; } = new();
    public List<StationItem> Stations { get; set; } = new();
    public List<VehicleTypeItem> VehicleTypes { get; set; } = new();
    public TripSearchResult? Result { get; set; }   // null nghĩa là chưa bấm Tìm
    public string? Error { get; set; }
}

// ===== Chọn ghế (#19): khớp JSON của GET /api/trips/{id}/seats =====
public class SeatInfo
{
    public string SeatCode { get; set; } = "";
    public int Floor { get; set; }
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = "";
}

public class SeatMapDto
{
    public int TripId { get; set; }
    public string RouteName { get; set; } = "";
    public string VehiclePlate { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public DateTime DepartAt { get; set; }
    public decimal Price { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public List<SeatInfo> Seats { get; set; } = new();
}

public class SeatsViewModel
{
    public SeatMapDto Map { get; set; } = new();
    public int Tickets { get; set; } = 1;   // số ghế tối đa được chọn
}