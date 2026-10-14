using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.DTO;

public class VerifyEmailDTO
{
    [Required][EmailAddress] public string Email { get; set; } = "";
    [Required][RegularExpression(@"^\d{6}$", ErrorMessage = "Mã gồm 6 chữ số.")] public string Code { get; set; } = "";
}

public class ResendOtpDTO
{
    [Required][EmailAddress] public string Email { get; set; } = "";
}

public class RegisterResponseDTO
{
    public string Message { get; set; } = "";
    // true: phải nhập mã OTP gửi qua email mới đăng nhập được
    public bool RequiresVerification { get; set; }
    public bool EmailSent { get; set; }
}

// Tra cứu đặt vé không cần đăng nhập: mã đặt vé + SĐT hoặc email đã dùng khi đặt
public class LookupBookingDto
{
    [Required, MaxLength(20)] public string Code { get; set; } = "";
    [Required, MaxLength(100)] public string Contact { get; set; } = "";
}

public class BookingDetailDto
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
    public List<TicketDetailDto> Tickets { get; set; } = new();
    public List<PaymentDetailDto> Payments { get; set; } = new();
}

public class TicketDetailDto
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

public class PaymentDetailDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? PaidAt { get; set; }
}
