using System;
using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class Vehicle
    {
        [Key] public int Id { get; set; }
        [Required] public string Plate { get; set; } = "";
        public int VehicleTypeId { get; set; }
        public VehicleType VehicleType { get; set; } = null!;
        public DateTime? RegistrationExpiry { get; set; }
        public DateTime? InsuranceExpiry { get; set; }
        public string Status { get; set; } = "Active";
        public string? ImageUrl { get; set; }
    }
}