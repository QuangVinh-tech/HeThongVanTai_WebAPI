using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class Review
    {
        [Key] public int Id { get; set; }

        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

     
        public int TripId { get; set; }
        public int CustomerId { get; set; }

        [Range(1, 5)] public int Rating { get; set; }
        [MaxLength(500)] public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}