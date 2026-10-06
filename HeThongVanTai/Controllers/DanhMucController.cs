using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;

namespace HeThongVanTai.Controllers
{
    [Route("api")]
    [ApiController]
    public class DanhMucController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DanhMucController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("stations")]
        public async Task<IActionResult> GetStations([FromQuery] string? city, [FromQuery] string? keyword)
        {
            var query = _context.Stations.AsQueryable();

            if (!string.IsNullOrWhiteSpace(city))
                query = query.Where(s => s.City != null && s.City.Contains(city));

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(s => s.Name.Contains(keyword) || (s.Address != null && s.Address.Contains(keyword)));

            return Ok(await query.ToListAsync());
        }

        [HttpGet("stations/{id}")]
        public async Task<IActionResult> GetStationById(int id)
        {
            var station = await _context.Stations.FindAsync(id);
            if (station == null) return NotFound(new { message = "Không tìm thấy bến xe / điểm đón trả." });
            return Ok(station);
        }

        [HttpPost("stations")]
        public async Task<IActionResult> CreateStation([FromBody] StationDto dto)
        {
            var station = new Station
            {
                Name = dto.Name,
                City = dto.City,
                Address = dto.Address
            };

            _context.Stations.Add(station);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetStationById), new { id = station.Id }, station);
        }

        [HttpPut("stations/{id}")]
        public async Task<IActionResult> UpdateStation(int id, [FromBody] StationDto dto)
        {
            var station = await _context.Stations.FindAsync(id);
            if (station == null) return NotFound(new { message = "Không tìm thấy bến xe / điểm đón trả." });

            station.Name = dto.Name;
            station.City = dto.City;
            station.Address = dto.Address;

            await _context.SaveChangesAsync();
            return Ok(station);
        }

        [HttpDelete("stations/{id}")]
        public async Task<IActionResult> DeleteStation(int id)
        {
            var station = await _context.Stations.FindAsync(id);
            if (station == null) return NotFound(new { message = "Không tìm thấy bến xe." });

            bool isUsed = await _context.BusRoutes.AnyAsync(r => r.FromStationId == id || r.ToStationId == id);
            if (isUsed)
                return BadRequest(new { message = "Không thể xóa bến xe đang được sử dụng trong tuyến đường." });

            _context.Stations.Remove(station);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa bến xe thành công." });
        }

        [HttpGet("routes")]
        public async Task<IActionResult> GetBusRoutes()
        {
            var routes = await _context.BusRoutes
                .Include(r => r.FromStation)
                .Include(r => r.ToStation)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.FromStationId,
                    FromStationName = r.FromStation.Name,
                    FromCity = r.FromStation.City,
                    r.ToStationId,
                    ToStationName = r.ToStation.Name,
                    ToCity = r.ToStation.City,
                    r.DistanceKm,
                    r.DurationMin
                })
                .ToListAsync();

            return Ok(routes);
        }

        [HttpGet("routes/{id}")]
        public async Task<IActionResult> GetBusRouteById(int id)
        {
            var route = await _context.BusRoutes
                .Include(r => r.FromStation)
                .Include(r => r.ToStation)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (route == null) return NotFound(new { message = "Không tìm thấy tuyến đường." });
            return Ok(route);
        }

        [HttpPost("routes")]
        public async Task<IActionResult> CreateBusRoute([FromBody] BusRouteDto dto)
        {
            if (dto.FromStationId == dto.ToStationId)
                return BadRequest(new { message = "Bến đi và bến đến không được trùng nhau." });

            var fromExists = await _context.Stations.AnyAsync(s => s.Id == dto.FromStationId);
            var toExists = await _context.Stations.AnyAsync(s => s.Id == dto.ToStationId);
            if (!fromExists || !toExists)
                return BadRequest(new { message = "Bến đi hoặc bến đến không tồn tại." });

            var route = new BusRoute
            {
                Name = dto.Name,
                FromStationId = dto.FromStationId,
                ToStationId = dto.ToStationId,
                DistanceKm = dto.DistanceKm,
                DurationMin = dto.DurationMin
            };

            _context.BusRoutes.Add(route);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetBusRouteById), new { id = route.Id }, route);
        }

        [HttpPut("routes/{id}")]
        public async Task<IActionResult> UpdateBusRoute(int id, [FromBody] BusRouteDto dto)
        {
            if (dto.FromStationId == dto.ToStationId)
                return BadRequest(new { message = "Bến đi và bến đến không được trùng nhau." });

            var route = await _context.BusRoutes.FindAsync(id);
            if (route == null) return NotFound(new { message = "Không tìm thấy tuyến đường." });

            route.Name = dto.Name;
            route.FromStationId = dto.FromStationId;
            route.ToStationId = dto.ToStationId;
            route.DistanceKm = dto.DistanceKm;
            route.DurationMin = dto.DurationMin;

            await _context.SaveChangesAsync();
            return Ok(route);
        }

        [HttpDelete("routes/{id}")]
        public async Task<IActionResult> DeleteBusRoute(int id)
        {
            var route = await _context.BusRoutes.FindAsync(id);
            if (route == null) return NotFound(new { message = "Không tìm thấy tuyến đường." });

            bool hasTrips = await _context.Trips.AnyAsync(t => t.BusRouteId == id);
            if (hasTrips)
                return BadRequest(new { message = "Không thể xóa tuyến đường đã có chuyến xe hoạt động." });

            _context.BusRoutes.Remove(route);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa tuyến đường thành công." });
        }
    }
}