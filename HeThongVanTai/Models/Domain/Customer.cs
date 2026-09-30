using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace HeThongVanTai.Models.Domain
{
    public class Customer
    {
        [Key] public int Id { get; set; }
        public string? UserId { get; set; }
        [Required] public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
