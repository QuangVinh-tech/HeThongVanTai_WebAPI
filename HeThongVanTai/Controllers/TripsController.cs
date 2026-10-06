using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;

namespace HeThongVanTai.Controllers
{
    [Route("api/trips")]
    [ApiController]
    public class TripsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TripsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{id}/seats")]
        public async Task<IActionResult> GetTripSeats(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.BusRoute)
                .Include(t => t.Vehicle)
                    .ThenInclude(v => v.VehicleType)
                        .ThenInclude(vt => vt.SeatTemplates)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
                return NotFound(new { message = "Không tìm thấy chuyến xe." });

            var activeTickets = trip.Tickets
                .Where(tk => tk.Status != "Cancelled")
                .ToDictionary(tk => tk.SeatCode, tk => tk.Status);

            var seats = trip.Vehicle.VehicleType.SeatTemplates
                .OrderBy(s => s.Floor)
                .ThenBy(s => s.SeatCode)
                .Select(s =>
                {
                    bool isOccupied = activeTickets.TryGetValue(s.SeatCode, out string? ticketStatus);
                    return new
                    {
                        s.SeatCode,
                        s.Floor,
                        IsAvailable = !isOccupied,
                        Status = isOccupied ? (ticketStatus ?? "Booked") : "Available"
                    };
                })
                .ToList();

            return Ok(new
            {
                TripId = trip.Id,
                RouteName = trip.BusRoute.Name,
                VehiclePlate = trip.Vehicle.Plate,
                VehicleType = trip.Vehicle.VehicleType.Name,
                DepartAt = trip.DepartAt,
                Price = trip.Price,
                TotalSeats = seats.Count,
                AvailableSeats = seats.Count(s => s.IsAvailable),
                Seats = seats
            });
        }

        [HttpPost("generate-schedule")]
        public async Task<IActionResult> GenerateFixedSchedule([FromBody] GenerateScheduleDto dto)
        {
            var route = await _context.BusRoutes.FindAsync(dto.BusRouteId);
            if (route == null) return BadRequest(new { message = "Tuyến đường không tồn tại." });

            if (!TimeSpan.TryParse(dto.DepartureTime, out TimeSpan timeOfDay))
                return BadRequest(new { message = "Giờ xuất bến không đúng định dạng HH:mm." });

            var createdTrips = new List<Trip>();

            for (var date = dto.FromDate.Date; date <= dto.ToDate.Date; date = date.AddDays(1))
            {
                if (dto.DaysOfWeek.Count == 0 || dto.DaysOfWeek.Contains(date.DayOfWeek))
                {
                    var departAt = date.Add(timeOfDay);
                    var arriveAt = departAt.AddMinutes(route.DurationMin);

                    bool exists = await _context.Trips.AnyAsync(t =>
                        t.BusRouteId == dto.BusRouteId &&
                        t.VehicleId == dto.VehicleId &&
                        t.DepartAt == departAt &&
                        t.Status != "Cancelled");

                    if (!exists)
                    {
                        var trip = new Trip
                        {
                            BusRouteId = dto.BusRouteId,
                            VehicleId = dto.VehicleId,
                            DriverId = dto.DriverId,
                            DepartAt = departAt,
                            ArriveAt = arriveAt,
                            Price = dto.Price,
                            Status = "Open"
                        };
                        createdTrips.Add(trip);
                    }
                }
            }

            _context.Trips.AddRange(createdTrips);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Đã tạo thành công {createdTrips.Count} chuyến xe theo lịch cố định.",
                TotalCreated = createdTrips.Count,
                Trips = createdTrips.Select(t => new { t.Id, t.DepartAt, t.ArriveAt, t.Price, t.Status })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetTrips(
            [FromQuery] int? busRouteId,
            [FromQuery] DateTime? date,
            [FromQuery] string? status)
        {
            var query = _context.Trips
                .Include(t => t.BusRoute).ThenInclude(r => r.FromStation)
                .Include(t => t.BusRoute).ThenInclude(r => r.ToStation)
                .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                .Include(t => t.Driver)
                .Include(t => t.Tickets)
                .AsQueryable();

            if (busRouteId.HasValue)
                query = query.Where(t => t.BusRouteId == busRouteId.Value);

            if (date.HasValue)
                query = query.Where(t => t.DepartAt.Date == date.Value.Date);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);

            var list = await query
                .OrderBy(t => t.DepartAt)
                .Select(t => new
                {
                    t.Id,
                    t.BusRouteId,
                    RouteName = t.BusRoute.Name,
                    FromStation = t.BusRoute.FromStation.Name,
                    ToStation = t.BusRoute.ToStation.Name,
                    t.VehicleId,
                    VehiclePlate = t.Vehicle.Plate,
                    VehicleType = t.Vehicle.VehicleType.Name,
                    t.DriverId,
                    DriverName = t.Driver.FullName,
                    t.DepartAt,
                    t.ArriveAt,
                    t.Price,
                    t.Status,
                    TotalSeats = t.Vehicle.VehicleType.SeatCount,
                    BookedSeats = t.Tickets.Count(tk => tk.Status != "Cancelled")
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTrip([FromBody] CreateTripDto dto)
        {
            var route = await _context.BusRoutes.FindAsync(dto.BusRouteId);
            if (route == null) return BadRequest(new { message = "Tuyến đường không tồn tại." });

            var vehicle = await _context.Vehicles.FindAsync(dto.VehicleId);
            if (vehicle == null || vehicle.Status != "Active")
                return BadRequest(new { message = "Xe không tồn tại hoặc đang không ở trạng thái hoạt động (Active)." });

            var driver = await _context.Drivers.FindAsync(dto.DriverId);
            if (driver == null || driver.Status != "Active")
                return BadRequest(new { message = "Tài xế không tồn tại hoặc đang không sẵn sàng." });

            var arriveAt = dto.ArriveAt ?? dto.DepartAt.AddMinutes(route.DurationMin);

            bool vehicleConflict = await _context.Trips.AnyAsync(t =>
                t.VehicleId == dto.VehicleId &&
                t.Status != "Cancelled" &&
                dto.DepartAt < (t.ArriveAt ?? t.DepartAt.AddHours(2)) &&
                arriveAt > t.DepartAt);

            if (vehicleConflict)
                return BadRequest(new { message = "Xe đã được phân công cho chuyến khác bị trùng thời gian." });

            bool driverConflict = await _context.Trips.AnyAsync(t =>
                t.DriverId == dto.DriverId &&
                t.Status != "Cancelled" &&
                dto.DepartAt < (t.ArriveAt ?? t.DepartAt.AddHours(2)) &&
                arriveAt > t.DepartAt);

            if (driverConflict)
                return BadRequest(new { message = "Tài xế đã có lịch lái chuyến khác bị trùng thời gian." });

            var trip = new Trip
            {
                BusRouteId = dto.BusRouteId,
                VehicleId = dto.VehicleId,
                DriverId = dto.DriverId,
                DepartAt = dto.DepartAt,
                ArriveAt = arriveAt,
                Price = dto.Price,
                Status = "Open"
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            return Ok(trip);
        }

        [HttpPut("{id}/change-vehicle")]
        public async Task<IActionResult> ChangeVehicle(int id, [FromBody] ChangeVehicleDto dto)
        {
            var trip = await _context.Trips
                .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null) return NotFound(new { message = "Không tìm thấy chuyến xe." });

            var newVehicle = await _context.Vehicles
                .Include(v => v.VehicleType)
                .FirstOrDefaultAsync(v => v.Id == dto.NewVehicleId);

            if (newVehicle == null || newVehicle.Status != "Active")
                return BadRequest(new { message = "Xe mới không tồn tại hoặc không khả dụng." });

            int bookedCount = trip.Tickets.Count(tk => tk.Status != "Cancelled");
            if (newVehicle.VehicleType.SeatCount < bookedCount)
                return BadRequest(new { message = $"Xe mới chỉ có {newVehicle.VehicleType.SeatCount} ghế, nhỏ hơn số vé đã đặt ({bookedCount} vé)." });

            trip.VehicleId = dto.NewVehicleId;
            if (dto.NewDriverId.HasValue)
            {
                trip.DriverId = dto.NewDriverId.Value;
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Đổi phương tiện / phân công lại thành công.",
                TripId = trip.Id,
                NewVehiclePlate = newVehicle.Plate,
                DriverId = trip.DriverId
            });
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateTripStatus(int id, [FromBody] UpdateTripStatusDto dto)
        {
            var trip = await _context.Trips
                .Include(t => t.BusRoute)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null) return NotFound(new { message = "Không tìm thấy chuyến xe." });

            trip.Status = dto.Status;

            if (dto.Status == "Delayed" && dto.NewDepartAt.HasValue)
            {
                trip.DepartAt = dto.NewDepartAt.Value;
                trip.ArriveAt = dto.NewDepartAt.Value.AddMinutes(trip.BusRoute.DurationMin);
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = $"Đã cập nhật trạng thái chuyến xe sang '{dto.Status}'.",
                trip.Id,
                trip.Status,
                trip.DepartAt,
                trip.ArriveAt
            });
        }
    }
}