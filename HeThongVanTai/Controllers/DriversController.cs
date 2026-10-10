using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;

namespace HeThongVanTai.Controllers
{
    [Route("api/drivers")]
    [ApiController]
    [Authorize(Roles = AppRoles.AdminOperator)]
    public class DriversController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DriversController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? filterOn,
            [FromQuery] string? filterQuery,
            [FromQuery] string? status,
            [FromQuery] string? licenseClass,
            [FromQuery] string? sortBy,
            [FromQuery] bool isAscending = true,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.Drivers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterOn) && !string.IsNullOrWhiteSpace(filterQuery))
            {
                if (filterOn.Equals("FullName", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(d => d.FullName.Contains(filterQuery));
                else if (filterOn.Equals("Phone", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(d => d.Phone != null && d.Phone.Contains(filterQuery));
                else if (filterOn.Equals("LicenseNo", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(d => d.LicenseNo != null && d.LicenseNo.Contains(filterQuery));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(d => d.Status == status);

            if (!string.IsNullOrWhiteSpace(licenseClass))
                query = query.Where(d => d.LicenseClass == licenseClass);

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                if (sortBy.Equals("FullName", StringComparison.OrdinalIgnoreCase))
                    query = isAscending ? query.OrderBy(d => d.FullName) : query.OrderByDescending(d => d.FullName);
                else if (sortBy.Equals("LicenseExpiry", StringComparison.OrdinalIgnoreCase))
                    query = isAscending ? query.OrderBy(d => d.LicenseExpiry) : query.OrderByDescending(d => d.LicenseExpiry);
            }
            else
            {
                query = query.OrderBy(d => d.Id);
            }

            int totalItems = await query.CountAsync();
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                Items = items
            });
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
            if (!string.IsNullOrWhiteSpace(dto.LicenseNo) &&
                await _context.Drivers.AnyAsync(d => d.LicenseNo == dto.LicenseNo))
            {
                return BadRequest(new { message = "Số giấy phép lái xe (GPLX) đã tồn tại." });
            }

            var driver = new Driver
            {
                FullName = dto.FullName,
                Phone = dto.Phone,
                LicenseNo = dto.LicenseNo,
                LicenseClass = dto.LicenseClass,
                LicenseExpiry = dto.LicenseExpiry,
                Status = dto.Status,
                AvatarUrl = dto.AvatarUrl
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

            if (!string.IsNullOrWhiteSpace(dto.LicenseNo) &&
                await _context.Drivers.AnyAsync(d => d.LicenseNo == dto.LicenseNo && d.Id != id))
            {
                return BadRequest(new { message = "Số giấy phép lái xe (GPLX) đã bị trùng với tài xế khác." });
            }

            driver.FullName = dto.FullName;
            driver.Phone = dto.Phone;
            driver.LicenseNo = dto.LicenseNo;
            driver.LicenseClass = dto.LicenseClass;
            driver.LicenseExpiry = dto.LicenseExpiry;
            driver.Status = dto.Status;
            if (!string.IsNullOrWhiteSpace(dto.AvatarUrl))
                driver.AvatarUrl = dto.AvatarUrl;

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

        [HttpPatch("{id}/avatar")]
        public async Task<IActionResult> UpdateAvatar(int id, [FromBody] UpdateDriverAvatarDto dto)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound(new { message = "Không tìm thấy tài xế." });

            driver.AvatarUrl = dto.AvatarUrl;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật ảnh đại diện thành công.", driver.Id, driver.AvatarUrl });
        }
    }
}