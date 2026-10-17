using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongVanTai.Models.Domain
{
    public class Shipment
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(20)] public string Code { get; set; } = "";   // mã vận đơn

        public int FromStationId { get; set; }
        public Station FromStation { get; set; } = null!;
        public int ToStationId { get; set; }
        public Station ToStation { get; set; } = null!;

        [Required, MaxLength(100)] public string SenderName { get; set; } = "";
        [Required, MaxLength(15)] public string SenderPhone { get; set; } = "";
        [MaxLength(300)] public string? SenderAddress { get; set; }     // bắt buộc khi PickupMode = Door

        [Required, MaxLength(100)] public string ReceiverName { get; set; } = "";
        [Required, MaxLength(15)] public string ReceiverPhone { get; set; } = "";
        [MaxLength(300)] public string? ReceiverAddress { get; set; }   // bắt buộc khi DeliveryMode = Door

        [MaxLength(10)] public string PickupMode { get; set; } = "Door";    // Door | Station
        [MaxLength(10)] public string DeliveryMode { get; set; } = "Door";  // Door | Station
        [MaxLength(20)] public string ServiceType { get; set; } = "Standard"; // Standard | Express
        [MaxLength(10)] public string FeePayer { get; set; } = "Sender";    // Sender | Receiver | Debt

        [Column(TypeName = "decimal(12,0)")] public decimal DeclaredValue { get; set; }
        [Column(TypeName = "decimal(12,0)")] public decimal ShippingFee { get; set; }
        [Column(TypeName = "decimal(12,0)")] public decimal DiscountAmount { get; set; }
        [Column(TypeName = "decimal(12,0)")] public decimal TotalAmount { get; set; }
        public int? PromotionId { get; set; }
        public Promotion? Promotion { get; set; }

        [MaxLength(20)] public string Status { get; set; } = "Pending";
        [MaxLength(10)] public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid | Paid
        public DateTime? PaidAt { get; set; }

        public int? TripId { get; set; }          // chuyến xe chở hàng (Operator gắn sau)
        public Trip? Trip { get; set; }

        [MaxLength(500)] public string? Note { get; set; }
        [MaxLength(450)] public string? CreatedByUserId { get; set; }   // Id tài khoản tạo đơn
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<ShipmentItem> Items { get; set; } = new();
        public List<ShipmentLog> Logs { get; set; } = new();
    }
}