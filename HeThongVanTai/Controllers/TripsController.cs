using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;

namespace HeThongVanTai.Controllers
{
    [Route("api/trips")]
    [ApiController]
    [Authorize(Roles = AppRoles.AdminOperator)]
    public class TripsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TripsController(AppDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
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

            var driver = await _context.Drivers.FindAsync(dto.DriverId);
            if (driver == null || driver.Status != "Active")
                return BadRequest(new { message = "Tài xế không tồn tại hoặc không sẵn sàng." });

            if (driver.LicenseExpiry.HasValue && driver.LicenseExpiry.Value.Date < dto.ToDate.Date)
                return BadRequest(new { message = "Giấy phép lái xe (GPLX) của tài xế đã hết hạn hoặc sẽ hết hạn trong khoảng lịch chạy này." });

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
                        t.VehicleId == dto.VehicleId &&
                        t.Status != "Cancelled" &&
                        departAt < (t.ArriveAt ?? t.DepartAt.AddHours(2)) &&
                        arriveAt > t.DepartAt);

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

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetTrips(
            [FromQuery] int? busRouteId,
            [FromQuery] DateTime? date,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] bool isAscending = true,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
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

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                if (sortBy.Equals("Price", StringComparison.OrdinalIgnoreCase))
                    query = isAscending ? query.OrderBy(t => t.Price) : query.OrderByDescending(t => t.Price);
                else if (sortBy.Equals("DepartAt", StringComparison.OrdinalIgnoreCase))
                    query = isAscending ? query.OrderBy(t => t.DepartAt) : query.OrderByDescending(t => t.DepartAt);
            }
            else
            {
                query = query.OrderBy(t => t.DepartAt);
            }

            int totalItems = await query.CountAsync();
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var list = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
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

            return Ok(new
            {
                TotalItems = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                Items = list
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTrip([FromBody] CreateTripDto dto)
        {
            var route = await _context.BusRoutes.FindAsync(dto.BusRouteId);
            if (route == null) return BadRequest(new { message = "Tuyến đường không tồn tại." });

            var vehicle = await _context.Vehicles.FindAsync(dto.VehicleId);
            if (vehicle == null || vehicle.Status != "Active")
                return BadRequest(new { message = "Xe không tồn tại hoặc đang không ở trạng thái hoạt động (Active)." });

            if (vehicle.RegistrationExpiry.HasValue && vehicle.RegistrationExpiry.Value.Date < dto.DepartAt.Date)
                return BadRequest(new { message = "Xe đã hết hạn đăng kiểm vào ngày khởi hành, không thể phân công." });

            var driver = await _context.Drivers.FindAsync(dto.DriverId);
            if (driver == null || driver.Status != "Active")
                return BadRequest(new { message = "Tài xế không tồn tại hoặc đang không sẵn sàng." });

            if (!driver.LicenseExpiry.HasValue || driver.LicenseExpiry.Value.Date < dto.DepartAt.Date)
                return BadRequest(new { message = "Giấy phép lái xe (GPLX) của tài xế đã hết hạn hoặc chưa cập nhật hạn bằng." });

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

            var arriveAt = trip.ArriveAt ?? trip.DepartAt.AddHours(2);
            bool vehicleConflict = await _context.Trips.AnyAsync(t =>
                t.Id != id &&
                t.VehicleId == dto.NewVehicleId &&
                t.Status != "Cancelled" &&
                trip.DepartAt < (t.ArriveAt ?? t.DepartAt.AddHours(2)) &&
                arriveAt > t.DepartAt);

            if (vehicleConflict)
                return BadRequest(new { message = "Xe mới đang bị trùng lịch chạy với một chuyến khác." });

            int bookedCount = trip.Tickets.Count(tk => tk.Status != "Cancelled");
            if (newVehicle.VehicleType.SeatCount < bookedCount)
                return BadRequest(new { message = $"Xe mới chỉ có {newVehicle.VehicleType.SeatCount} ghế, nhỏ hơn số vé đã đặt ({bookedCount} vé)." });

            if (dto.NewDriverId.HasValue)
            {
                var newDriver = await _context.Drivers.FindAsync(dto.NewDriverId.Value);
                if (newDriver == null || newDriver.Status != "Active")
                    return BadRequest(new { message = "Tài xế mới không tồn tại hoặc không sẵn sàng." });

                if (!newDriver.LicenseExpiry.HasValue || newDriver.LicenseExpiry.Value.Date < trip.DepartAt.Date)
                    return BadRequest(new { message = "GPLX của tài xế mới đã hết hạn." });

                trip.DriverId = dto.NewDriverId.Value;
            }

            trip.VehicleId = dto.NewVehicleId;
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