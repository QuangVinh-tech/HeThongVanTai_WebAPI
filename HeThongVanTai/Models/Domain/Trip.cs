using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Sockets;

namespace HeThongVanTai.Models.Domain
{
    public class Trip
    {
        [Key] public int Id { get; set; }
        public int BusRouteId { get; set; }
        public BusRoute BusRoute { get; set; } = null!;
        public int VehicleId { get; set; }
        public Vehicle Vehicle { get; set; } = null!;
        public int DriverId { get; set; }
        public Driver Driver { get; set; } = null!;
        public DateTime DepartAt { get; set; }
        public DateTime? ArriveAt { get; set; }
        [Column(TypeName = "decimal(12,0)")] public decimal Price { get; set; }
        public string Status { get; set; } = "Open";
        public List<Ticket> Tickets { get; set; } = new();
    }
}