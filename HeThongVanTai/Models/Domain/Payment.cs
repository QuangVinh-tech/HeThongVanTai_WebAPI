using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class Payment
    {
        [Key] public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;
        [Column(TypeName = "decimal(12,0)")] public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string Status { get; set; } = "Pending";
        public DateTime? PaidAt { get; set; }
    }
}
