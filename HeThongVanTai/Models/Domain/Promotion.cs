using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongVanTai.Models.Domain
{
    public class Promotion
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(30)] public string Code { get; set; } = "";
        [MaxLength(10)] public string DiscountType { get; set; } = "Percent";   // Percent | Amount
        [Column(TypeName = "decimal(12,0)")] public decimal Value { get; set; }
        [Column(TypeName = "decimal(12,0)")] public decimal? MaxDiscount { get; set; }
        [Column(TypeName = "decimal(12,0)")] public decimal MinOrder { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; }
        public bool IsActive { get; set; } = true;
    }
}