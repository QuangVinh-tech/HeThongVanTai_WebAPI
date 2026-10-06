using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.DTO;

public class SaveCustomerDto
{
    [Required, MaxLength(100)] public string FullName { get; set; } = "";
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "SĐT gồm 10 số, bắt đầu bằng 0")]
    public string? Phone { get; set; }
    [EmailAddress] public string? Email { get; set; }
}

public class SeatDto
{
    [Required, MaxLength(10)] public string SeatCode { get; set; } = "";
    [MaxLength(100)] public string? PassengerName { get; set; }
}

public class CreateBookingDto
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [Range(1, int.MaxValue)] public int TripId { get; set; }
    [Required, MinLength(1)] public List<SeatDto> Seats { get; set; } = new();
    public string? PromoCode { get; set; }
}

public class ChangeTicketDto
{
    [Required] public string NewSeatCode { get; set; } = "";
    public int? NewTripId { get; set; }
}

public class PayDto
{
    [Range(1, int.MaxValue)] public int BookingId { get; set; }
    [Required] public string Method { get; set; } = "Cash";   // Cash | BankTransfer
}

public class SavePromotionDto
{
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [Required] public string DiscountType { get; set; } = "Percent";   // Percent | Amount
    [Range(1, 1000000000)] public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrder { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int? UsageLimit { get; set; }
}

public class ValidatePromoDto
{
    [Required] public string Code { get; set; } = "";
    public decimal Amount { get; set; }
}