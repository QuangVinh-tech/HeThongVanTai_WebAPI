using HeThongVanTai.Models.Domain;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;

public class Booking
{
    [Key] public int Id { get; set; }
    [Required] public string Code { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    [Column(TypeName = "decimal(12,0)")] public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Holding";
    public DateTime? HoldExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<Ticket> Tickets { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    [Column(TypeName = "decimal(12,0)")] public decimal DiscountAmount { get; set; }
    public int? PromotionId { get; set; }
    public Promotion? Promotion { get; set; }
}