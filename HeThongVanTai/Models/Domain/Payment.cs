
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongVanTai.Models.Domain
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        public int BookingId { get; set; }

        public Booking Booking { get; set; } = null!;

        [Column(TypeName = "decimal(12,0)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(30)]
        public string Method { get; set; } = "Cash";

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime? PaidAt { get; set; }

        // ID giao dịch do SePay cấp, dùng để chống xử lý webhook trùng.
        [MaxLength(100)]
        public string? SePayTransactionId { get; set; }

        // Mã tham chiếu do ngân hàng cung cấp, dùng để tra soát.
        [MaxLength(100)]
        public string? BankReference { get; set; }
    }
}
