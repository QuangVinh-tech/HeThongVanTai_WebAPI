using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace HeThongVanTai.Web.Models.ViewModels.VeKhach;

// ----- Danh sách "Vé của tôi" (GET api/bookings/mine) -----
public class MyBookingPage
{
    public List<MyBookingItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(PageSize, 1)));
}

public class MyBookingItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    public string? ContactPhone { get; set; }
    public bool NeedTransfer { get; set; }
    public string? TransferAddress { get; set; }
    public List<MyTicketItem> Tickets { get; set; } = new();
}

public class MyTicketItem
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = "";
    public string SeatCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "";
    public string Route { get; set; } = "";
    public DateTime DepartAt { get; set; }
}

// ----- Chi tiết một đặt vé (GET api/bookings/mine/{code} và POST api/bookings/lookup) -----
public class BookingDetail
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Customer { get; set; } = "";
    public string? ContactPhone { get; set; }
    public bool NeedTransfer { get; set; }
    public string? TransferAddress { get; set; }
    public List<TicketDetail> Tickets { get; set; } = new();
    public List<PaymentDetail> Payments { get; set; } = new();
}

public class TicketDetail
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = "";
    public string SeatCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "";
    public string Route { get; set; } = "";
    public string FromStation { get; set; } = "";
    public string ToStation { get; set; } = "";
    public DateTime DepartAt { get; set; }
    public DateTime? ArriveAt { get; set; }
    public string Plate { get; set; } = "";
    public string? VehicleType { get; set; }
    public string? FromAddress { get; set; }
    public string? ToAddress { get; set; }
    public int DistanceKm { get; set; }
    public int DurationMin { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
}

public class PaymentDetail
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? PaidAt { get; set; }
}

public class RefundPreview
{
    public bool CanCancel { get; set; }
    public string? Reason { get; set; }
    public decimal RefundRate { get; set; }
    public decimal RefundAmount { get; set; }
}

public class CancelResult
{
    public int BookingId { get; set; }
    public string Status { get; set; } = "";
    public decimal RefundRate { get; set; }
    public decimal RefundAmount { get; set; }
}

// Trang chi tiết: dữ liệu + số tiền hoàn dự kiến (nếu còn hủy được)
public class MyBookingDetailViewModel
{
    public BookingDetail Booking { get; set; } = new();
    public RefundPreview? Refund { get; set; }
}

// ----- Tra cứu không cần đăng nhập -----
public class LookupForm
{
    [Required(ErrorMessage = "Vui lòng nhập mã đặt vé")]
    [MaxLength(20, ErrorMessage = "Mã đặt vé quá dài")]
    [Display(Name = "Mã đặt vé")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại hoặc email đã dùng khi đặt")]
    [MaxLength(100, ErrorMessage = "Quá dài")]
    [Display(Name = "Số điện thoại hoặc email")]
    public string Contact { get; set; } = "";
}

public class TraCuuViewModel
{
    public LookupForm Form { get; set; } = new();
    public BookingDetail? Booking { get; set; }
}

// Nhãn tiếng Việt và định dạng hiển thị
public static class VeLabels
{
    private static readonly CultureInfo Vi = new("vi-VN");

    public static readonly string[] BookingStatuses = { "Holding", "Paid", "Cancelled", "Expired" };

    public static string Booking(string status) => status switch
    {
        "Holding" => "Chờ thanh toán",
        "Paid" => "Đã thanh toán",
        "Cancelled" => "Đã hủy",
        "Expired" => "Hết hạn giữ chỗ",
        _ => status
    };

    public static string BookingPill(string status) => status switch
    {
        "Paid" => "pill-ok",
        "Holding" => "pill-warn",
        "Cancelled" or "Expired" => "pill-bad",
        _ => "pill-info"
    };

    public static string Ticket(string status) => status switch
    {
        "Active" => "Còn hiệu lực",
        "CheckedIn" => "Đã soát vé",
        "Cancelled" => "Đã hủy",
        _ => status
    };

    public static string Payment(string status) => status switch
    {
        "Paid" => "Đã thanh toán",
        "Pending" => "Chờ xác nhận",
        "Refunded" => "Đã hoàn tiền",
        "Failed" => "Không thành công",
        _ => status
    };

    public static string Method(string method) => method switch
    {
        "Cash" => "Tiền mặt",
        "BankTransfer" => "Chuyển khoản",
        _ => method
    };

    public static string Money(decimal value) => value.ToString("N0", Vi) + "đ";
    public static string Time(DateTime value) => value.ToString("HH:mm dd/MM/yyyy", Vi);
    public static string Duration(int minutes)
    {
        if (minutes <= 0) return "-";
        var h = minutes / 60; var m = minutes % 60;
        return h > 0 ? (m > 0 ? $"{h} giờ {m} phút" : $"{h} giờ") : $"{m} phút";
    }

    public static string Percent(decimal rate) => (rate * 100).ToString("0", Vi) + "%";
}
