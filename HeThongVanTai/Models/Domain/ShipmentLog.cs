using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class ShipmentLog
    {
        [Key] public int Id { get; set; }
        public int ShipmentId { get; set; }
        public Shipment Shipment { get; set; } = null!;
        [MaxLength(20)] public string Status { get; set; } = "";
        [MaxLength(300)] public string? Note { get; set; }
        public DateTime At { get; set; } = DateTime.Now;
        [MaxLength(256)] public string? ByEmail { get; set; }
    }
}
