using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Web.Models.ViewModels.KhachHang;

public class CustomerItem
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

// API /api/Customers trả { total, page, pageSize, items }
public class CustomerPage
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<CustomerItem> Items { get; set; } = new();
}

public class CustomerListModel
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(Total / (double)PageSize) : 0;
    public List<CustomerItem> Items { get; set; } = new();
    public string? LoadError { get; set; }
}

public class CustomerForm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại gồm 10 số, bắt đầu bằng 0")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [MaxLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
    [Display(Name = "Email")]
    public string? Email { get; set; }
}

public class HistoryTicket
{
    public string TicketCode { get; set; } = "";
    public string SeatCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "";
    public string? Route { get; set; }
    public DateTime DepartAt { get; set; }
}

public class HistoryBooking
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<HistoryTicket> Tickets { get; set; } = new();
}

public class CustomerHistoryModel
{
    public CustomerItem Customer { get; set; } = new();
    public List<HistoryBooking> Bookings { get; set; } = new();
    public string? LoadError { get; set; }
}

public static class BookingStatusHelper
{
    public static string Text(string? s) => s switch
    {
        "Holding" => "Đang giữ chỗ",
        "Paid" => "Đã thanh toán",
        "Cancelled" => "Đã hủy",
        "Expired" => "Hết hạn giữ chỗ",
        "Active" => "Còn hiệu lực",
        _ => s ?? ""
    };

    public static string Css(string? s) => s switch
    {
        "Paid" or "Active" => "pill-ok",
        "Holding" => "pill-warn",
        "Cancelled" or "Expired" => "pill-bad",
        _ => "pill-info"
    };
}
