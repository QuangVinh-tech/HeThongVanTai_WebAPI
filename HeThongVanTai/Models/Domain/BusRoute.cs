using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace HeThongVanTai.Models.Domain
{
    public class BusRoute
    {
        [Key] public int Id { get; set; }
        [Required] public string Name { get; set; } = "";
        public int FromStationId { get; set; }
        public Station FromStation { get; set; } = null!;
        public int ToStationId { get; set; }
        public Station ToStation { get; set; } = null!;
        public int DistanceKm { get; set; }
        public int DurationMin { get; set; }
        public List<Trip> Trips { get; set; } = new();
    }
}
