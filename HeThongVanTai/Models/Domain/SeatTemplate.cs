using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongVanTai.Models.Domain
{
    public class SeatTemplate
    {
        [Key] public int Id { get; set; }
        public int VehicleTypeId { get; set; }
        public VehicleType VehicleType { get; set; } = null!;
        [Required] public string SeatCode { get; set; } = "";
        public int Floor { get; set; } = 1;
    }
}
