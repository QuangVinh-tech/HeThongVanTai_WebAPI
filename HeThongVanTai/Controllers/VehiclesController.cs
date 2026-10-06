using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;

namespace HeThongVanTai.Controllers
{
    [Route("api/vehicles")]
    [ApiController]
    public class VehiclesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VehiclesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int? vehicleTypeId)
        {
            var query = _context.Vehicles
                .Include(v => v.VehicleType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(v => v.Status == status);

            if (vehicleTypeId.HasValue)
                query = query.Where(v => v.VehicleTypeId == vehicleTypeId.Value);

            var result = await query.Select(v => new
            {
                v.Id,
                v.Plate,
                v.VehicleTypeId,
                VehicleTypeName = v.VehicleType.Name,
                SeatCount = v.VehicleType.SeatCount,
                v.RegistrationExpiry,
                v.InsuranceExpiry,
                v.Status
            }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var v = await _context.Vehicles
                .Include(x => x.VehicleType)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (v == null) return NotFound(new { message = "Không tìm thấy xe." });
            return Ok(v);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VehicleDto dto)
        {
            if (await _context.Vehicles.AnyAsync(v => v.Plate == dto.Plate))
                return BadRequest(new { message = "Biển số xe đã tồn tại trong hệ thống." });

            if (!await _context.VehicleTypes.AnyAsync(vt => vt.Id == dto.VehicleTypeId))
                return BadRequest(new { message = "Loại xe không hợp lệ." });

            var vehicle = new Vehicle
            {
                Plate = dto.Plate,
                VehicleTypeId = dto.VehicleTypeId,
                RegistrationExpiry = dto.RegistrationExpiry,
                InsuranceExpiry = dto.InsuranceExpiry,
                Status = dto.Status
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, vehicle);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] VehicleDto dto)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null) return NotFound(new { message = "Không tìm thấy xe." });

            if (await _context.Vehicles.AnyAsync(v => v.Plate == dto.Plate && v.Id != id))
                return BadRequest(new { message = "Biển số xe đã bị trùng với xe khác." });

            vehicle.Plate = dto.Plate;
            vehicle.VehicleTypeId = dto.VehicleTypeId;
            vehicle.RegistrationExpiry = dto.RegistrationExpiry;
            vehicle.InsuranceExpiry = dto.InsuranceExpiry;
            vehicle.Status = dto.Status;

            await _context.SaveChangesAsync();
            return Ok(vehicle);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null) return NotFound(new { message = "Không tìm thấy xe." });

            bool hasTrips = await _context.Trips.AnyAsync(t => t.VehicleId == id);
            if (hasTrips)
                return BadRequest(new { message = "Xe đã có lịch sử chạy chuyến, chỉ nên chuyển trạng thái sang Inactive." });

            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa xe thành công." });
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetVehicleAlerts([FromQuery] int daysThreshold = 30)
        {
            var today = DateTime.Today;
            var warningDate = today.AddDays(daysThreshold);

            var vehicles = await _context.Vehicles
                .Include(v => v.VehicleType)
                .Where(v => v.Status == "Maintenance"
                         || (v.RegistrationExpiry.HasValue && v.RegistrationExpiry.Value <= warningDate)
                         || (v.InsuranceExpiry.HasValue && v.InsuranceExpiry.Value <= warningDate))
                .ToListAsync();

            var alerts = vehicles.Select(v =>
            {
                int? regDaysLeft = v.RegistrationExpiry.HasValue ? (v.RegistrationExpiry.Value.Date - today).Days : null;
                int? insDaysLeft = v.InsuranceExpiry.HasValue ? (v.InsuranceExpiry.Value.Date - today).Days : null;

                return new
                {
                    VehicleId = v.Id,
                    v.Plate,
                    VehicleType = v.VehicleType.Name,
                    v.Status,
                    v.RegistrationExpiry,
                    RegistrationDaysLeft = regDaysLeft,
                    IsRegistrationExpired = regDaysLeft.HasValue && regDaysLeft.Value < 0,
                    IsRegistrationExpiringSoon = regDaysLeft.HasValue && regDaysLeft.Value >= 0 && regDaysLeft.Value <= daysThreshold,
                    v.InsuranceExpiry,
                    InsuranceDaysLeft = insDaysLeft,
                    IsInsuranceExpired = insDaysLeft.HasValue && insDaysLeft.Value < 0,
                    IsInsuranceExpiringSoon = insDaysLeft.HasValue && insDaysLeft.Value >= 0 && insDaysLeft.Value <= daysThreshold,
                    IsUnderMaintenance = v.Status == "Maintenance"
                };
            }).ToList();

            return Ok(new
            {
                TotalAlerts = alerts.Count,
                ExpiredRegistrationCount = alerts.Count(a => a.IsRegistrationExpired),
                ExpiringRegistrationCount = alerts.Count(a => a.IsRegistrationExpiringSoon),
                ExpiredInsuranceCount = alerts.Count(a => a.IsInsuranceExpired),
                ExpiringInsuranceCount = alerts.Count(a => a.IsInsuranceExpiringSoon),
                MaintenanceCount = alerts.Count(a => a.IsUnderMaintenance),
                Items = alerts
            });
        }
    }
}