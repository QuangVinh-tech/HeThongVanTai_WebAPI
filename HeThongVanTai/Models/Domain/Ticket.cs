using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class Ticket
    {
        [Key] public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;
        public int TripId { get; set; }
        public Trip Trip { get; set; } = null!;
        [Required] public string SeatCode { get; set; } = "";

       
        [MaxLength(20)] public string TicketCode { get; set; } = "";
      

        public string? PassengerName { get; set; }
        [Column(TypeName = "decimal(12,0)")] public decimal Price { get; set; }
        public string Status { get; set; } = "Active";
    }
}