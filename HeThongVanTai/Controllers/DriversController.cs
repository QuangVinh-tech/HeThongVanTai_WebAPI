using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;

namespace HeThongVanTai.Controllers
{
    [Route("api/drivers")]
    [ApiController]
    public class DriversController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DriversController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? licenseClass)
        {
            var query = _context.Drivers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(d => d.Status == status);

            if (!string.IsNullOrWhiteSpace(licenseClass))
                query = query.Where(d => d.LicenseClass == licenseClass);

            return Ok(await query.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound(new { message = "Không tìm thấy tài xế / phụ xe." });
            return Ok(driver);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DriverDto dto)
        {
            var driver = new Driver
            {
                FullName = dto.FullName,
                Phone = dto.Phone,
                LicenseNo = dto.LicenseNo,
                LicenseClass = dto.LicenseClass,
                LicenseExpiry = dto.LicenseExpiry,
                Status = dto.Status
            };

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = driver.Id }, driver);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] DriverDto dto)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound(new { message = "Không tìm thấy tài xế / phụ xe." });

            driver.FullName = dto.FullName;
            driver.Phone = dto.Phone;
            driver.LicenseNo = dto.LicenseNo;
            driver.LicenseClass = dto.LicenseClass;
            driver.LicenseExpiry = dto.LicenseExpiry;
            driver.Status = dto.Status;

            await _context.SaveChangesAsync();
            return Ok(driver);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound(new { message = "Không tìm thấy tài xế." });

            bool hasTrips = await _context.Trips.AnyAsync(t => t.DriverId == id);
            if (hasTrips)
                return BadRequest(new { message = "Tài xế đã được phân công chuyến xe, chỉ nên đổi trạng thái sang Inactive." });

            _context.Drivers.Remove(driver);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa tài xế thành công." });
        }
    }
}