using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;

namespace HeThongVanTai.Repositories
{
    public class LocalImageRepository : IImageRepository
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _context;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        public LocalImageRepository(IWebHostEnvironment webHostEnvironment, IHttpContextAccessor httpContextAccessor, AppDbContext context,
            Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _config = config;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public async Task<IEnumerable<Image>> GetAll()
        {
            return await _context.Images.ToListAsync();
        }

        public async Task<Image> Upload(Image image)
        {
            var localFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", image.FileName);

            using var stream = new FileStream(localFilePath, FileMode.Create);
            await image.File.CopyToAsync(stream);

            // Triển khai: Web gọi API qua mạng nội bộ (ví dụ http://api:8080) nên địa chỉ lấy từ request sẽ không mở được
            // từ trình duyệt; đặt PublicUrls:ApiBaseUrl (ví dụ https://api.example.com) để ảnh luôn có địa chỉ công khai.
            var publicBase = _config["PublicUrls:ApiBaseUrl"]?.TrimEnd('/');
            var urlFilePath = !string.IsNullOrWhiteSpace(publicBase)
                ? $"{publicBase}/Images/{image.FileName}"
                : $"{_httpContextAccessor.HttpContext.Request.Scheme}://{_httpContextAccessor.HttpContext.Request.Host}{_httpContextAccessor.HttpContext.Request.PathBase}/Images/{image.FileName}";

            image.FilePath = urlFilePath;

            await _context.Images.AddAsync(image);
            await _context.SaveChangesAsync();

            return image;
        }
    }
}