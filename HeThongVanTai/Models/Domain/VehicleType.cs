using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class VehicleType
    {
        [Key] public int Id { get; set; }
        [Required] public string Name { get; set; } = "";
        public int SeatCount { get; set; }
        public List<SeatTemplate> SeatTemplates { get; set; } = new();
    }
}