using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.DTO;

public class ParcelDto
{
    [MaxLength(20)] public string GoodsType { get; set; } = "Other";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    [Range(0.01, 1000)] public decimal Weight { get; set; }
    [Range(0, 300)] public int Length { get; set; }
    [Range(0, 300)] public int Width { get; set; }
    [Range(0, 300)] public int Height { get; set; }
    [Range(1, 100)] public int Quantity { get; set; } = 1;
}

public class QuoteRequestDto
{
    public int FromStationId { get; set; }
    public int ToStationId { get; set; }
    public string ServiceType { get; set; } = "Standard";
    public string PickupMode { get; set; } = "Door";
    public string DeliveryMode { get; set; } = "Door";
    [Range(0, 1_000_000_000)] public decimal DeclaredValue { get; set; }
    public string? PromoCode { get; set; }
    [MinLength(1)] public List<ParcelDto> Parcels { get; set; } = new();
}

public class CreateShipmentDto : QuoteRequestDto
{
    [Required, MaxLength(100)] public string SenderName { get; set; } = "";
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "SĐT phải đủ 10 số.")] public string SenderPhone { get; set; } = "";
    [MaxLength(300)] public string? SenderAddress { get; set; }
    [Required, MaxLength(100)] public string ReceiverName { get; set; } = "";
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "SĐT phải đủ 10 số.")] public string ReceiverPhone { get; set; } = "";
    [MaxLength(300)] public string? ReceiverAddress { get; set; }
    public string FeePayer { get; set; } = "Sender";
    [MaxLength(500)] public string? Note { get; set; }
}

public class QuoteResultDto
{
    public int DistanceKm { get; set; }
    public decimal ChargeWeightKg { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}

public class ChangeStatusDto
{
    [Required] public string Status { get; set; } = "";
    [MaxLength(300)] public string? Note { get; set; }
}
public class AssignTripDto
{
    [Range(1, int.MaxValue)] public int TripId { get; set; }
}