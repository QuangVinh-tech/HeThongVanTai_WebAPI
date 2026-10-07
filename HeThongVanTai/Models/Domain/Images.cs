using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace HeThongVanTai.Models.Domain
{
    public class Image
    {
        [Key]
        public int Id { get; set; }
        [NotMapped]
        public IFormFile File { get; set; } = null!;
        public string FileName { get; set; } = "";
        public string? FileDescription { get; set; }
        public string FileExtension { get; set; } = "";
        public long FileSizeInBytes { get; set; }
        public string FilePath { get; set; } = "";
    }
}