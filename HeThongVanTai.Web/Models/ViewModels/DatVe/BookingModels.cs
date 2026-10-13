namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

// ===== Dữ liệu API trả về (khớp JSON của /api/bookings và /api/payments) =====
public class BookingTicketDto
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = "";
    public int TripId { get; set; }
    public string SeatCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "";
    public string? Route { get; set; }
    public DateTime? DepartAt { get; set; }
    public string? Plate { get; set; }
}

public class BookingDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? Customer { get; set; }
    public List<BookingTicketDto> Tickets { get; set; } = new();
}

public class PaymentDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = "";
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? PaidAt { get; set; }
    public string? TransferContent { get; set; }
}

public class PromoCheckDto
{
    public string Code { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}

// ===== Dữ liệu form đặt vé (#20) =====
public class PassengerInput
{
    public string SeatCode { get; set; } = "";
    public string? Name { get; set; }
}

public class CreateBookingForm
{
    public int TripId { get; set; }
    public string? PromoCode { get; set; }
    public string? CustomerName { get; set; }    // nhân viên bán tại quầy
    public string? CustomerPhone { get; set; }
    public List<PassengerInput> Passengers { get; set; } = new();
}

public class CreateBookingViewModel
{
    public SeatMapDto Map { get; set; } = new();
    public CreateBookingForm Form { get; set; } = new();
    public bool IsStaff { get; set; }
    public decimal Subtotal => Map.Price * Form.Passengers.Count;
}

// ===== Thanh toán (#21) =====
public class PayViewModel
{
    public BookingDto Booking { get; set; } = new();
    public int RemainingSeconds { get; set; }
    public bool IsStaff { get; set; }
    public string? BankInfo { get; set; }   // tùy chọn: Payment:BankInfo trong appsettings
}

public class PaymentResultViewModel
{
    public BookingDto Booking { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}