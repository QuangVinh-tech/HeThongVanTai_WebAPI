using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class ShipmentItem
    {
        [Key] public int Id { get; set; }
        public int ShipmentId { get; set; }
        public Shipment Shipment { get; set; } = null!;
        [MaxLength(20)] public string GoodsType { get; set; } = "Other"; // Document, Clothes, Electronics, Food, Fragile, Other
        [Required, MaxLength(200)] public string Name { get; set; } = "";
        [Column(TypeName = "decimal(8,2)")] public decimal Weight { get; set; }        // kg thực
        public int Length { get; set; }   // cm
        public int Width { get; set; }
        public int Height { get; set; }
        public int Quantity { get; set; } = 1;
        [Column(TypeName = "decimal(8,2)")] public decimal ChargeWeight { get; set; } // kg tính cước của 1 kiện
    }
}
