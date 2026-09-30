using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain
{
    public class Station
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }
    }
}