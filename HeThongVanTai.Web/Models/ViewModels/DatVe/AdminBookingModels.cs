namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

// Khách hàng (GET /api/customers) dùng khi bán vé tại quầy
public class CustomerItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class CustomerListResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<CustomerItem> Items { get; set; } = new();
}

// Một dòng trong danh sách quản trị (GET /api/bookings)
public class AdminBookingItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    public string? Customer { get; set; }
    public string? Phone { get; set; }
    public int Seats { get; set; }
}

public class AdminBookingList
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AdminBookingItem> Items { get; set; } = new();
}

public class AdminBookingFilter
{
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? SortBy { get; set; }     // createdAt | total | status
    public bool Desc { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class AdminBookingsViewModel
{
    public AdminBookingFilter Filter { get; set; } = new();
    public AdminBookingList? Result { get; set; }
    public string? Error { get; set; }
}

public class AdminBookingDetailViewModel
{
    public BookingDto Booking { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}