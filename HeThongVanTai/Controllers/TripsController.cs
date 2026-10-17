using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using HeThongVanTai.Services.Audit;

namespace HeThongVanTai.Controllers
{
    [Route("api/trips")]
    [ApiController]
    [Authorize]   // phải đăng nhập; quyền cụ thể ghi trên từng action (Admin/Operator, Driver...)
    public class TripsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuditService? _audit;
        private readonly IConfiguration? _cfg;

        public TripsController(AppDbContext context, IAuditService? audit = null, IConfiguration? cfg = null)
        {
            _context = context;
            _audit = audit;
            _cfg = cfg;
        }

        // Tài xế chỉ được bấm "Bắt đầu" trước giờ khởi hành tối đa bấy nhiêu phút (Trips:StartEarlyMinutes, mặc định 120)
        private int StartEarlyMinutes => _cfg?.GetValue<int?>("Trips:StartEarlyMinutes") ?? 120;

        private bool IsStaff => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

        private async Task<Driver?> CurrentDriverAsync()
        {
            var uid = UserClaims.UserId(User);
            if (uid == null) return null;
            return await _context.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == uid);
        }

        private IActionResult NotOwner() =>
            StatusCode(403, new { message = "Bạn chỉ được xem và thao tác chuyến được phân công cho mình." });

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

        // ===== Lịch chạy cố định (việc 39) =====
        private sealed class PlanItem
        {
            public DateTime DepartAt { get; set; }
            public DateTime ArriveAt { get; set; }
            public string? Problem { get; set; }
        }

        private async Task<(string? Error, List<PlanItem> Plan)> BuildPlanAsync(GenerateScheduleDto dto)
        {
            var plan = new List<PlanItem>();

            var route = await _context.BusRoutes.FindAsync(dto.BusRouteId);
            if (route == null) return ("Tuyến đường không tồn tại.", plan);

            var vehicle = await _context.Vehicles.FindAsync(dto.VehicleId);
            if (vehicle == null || vehicle.Status != "Active")
                return ("Xe không tồn tại hoặc đang không ở trạng thái hoạt động (Active).", plan);

            var driver = await _context.Drivers.FindAsync(dto.DriverId);
            if (driver == null || driver.Status != "Active")
                return ("Tài xế không tồn tại hoặc không sẵn sàng.", plan);

            if (!TimeSpan.TryParse(dto.DepartureTime, out TimeSpan timeOfDay) || timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
                return ("Giờ xuất bến không đúng định dạng HH:mm.", plan);

            if (dto.ToDate.Date < dto.FromDate.Date)
                return ("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.", plan);
            if ((dto.ToDate.Date - dto.FromDate.Date).TotalDays > 92)
                return ("Khoảng ngày tối đa là 93 ngày (khoảng 3 tháng).", plan);
            if (dto.Price < 0)
                return ("Giá vé không được âm.", plan);

            var days = dto.DaysOfWeek ?? new List<DayOfWeek>();
            for (var date = dto.FromDate.Date; date <= dto.ToDate.Date; date = date.AddDays(1))
            {
                if (days.Count != 0 && !days.Contains(date.DayOfWeek)) continue;
                var departAt = date.Add(timeOfDay);
                plan.Add(new PlanItem { DepartAt = departAt, ArriveAt = departAt.AddMinutes(route.DurationMin) });
            }
            if (plan.Count == 0)
                return ("Không có ngày nào trong khoảng đã chọn khớp với các thứ đã chọn.", plan);

            // Một lần đọc các chuyến có thể trùng; so sánh trong bộ nhớ
            var from = plan.Min(p => p.DepartAt).AddDays(-2);
            var to = plan.Max(p => p.ArriveAt).AddDays(2);
            var existing = (await _context.Trips.AsNoTracking()
                    .Where(t => t.Status != TripStatus.Cancelled
                                && (t.VehicleId == vehicle.Id || t.DriverId == driver.Id)
                                && t.DepartAt > from && t.DepartAt < to)
                    .Select(t => new { t.VehicleId, t.DriverId, t.DepartAt, t.ArriveAt })
                    .ToListAsync())
                .Select(t => new { t.VehicleId, t.DriverId, t.DepartAt, End = t.ArriveAt ?? t.DepartAt.AddHours(2) })
                .ToList();

            var now = DateTime.Now;
            PlanItem? previous = null;
            foreach (var p in plan)
            {
                var problems = new List<string>();
                if (p.DepartAt <= now) problems.Add("thời điểm đã qua");
                if (vehicle.RegistrationExpiry.HasValue && vehicle.RegistrationExpiry.Value.Date < p.DepartAt.Date)
                    problems.Add("xe đã hết hạn đăng kiểm");
                if (!driver.LicenseExpiry.HasValue || driver.LicenseExpiry.Value.Date < p.DepartAt.Date)
                    problems.Add("GPLX của tài xế đã hết hạn hoặc chưa cập nhật");
                if (existing.Any(e => e.VehicleId == vehicle.Id && p.DepartAt < e.End && p.ArriveAt > e.DepartAt))
                    problems.Add("xe đã có chuyến khác trùng giờ");
                if (existing.Any(e => e.DriverId == driver.Id && p.DepartAt < e.End && p.ArriveAt > e.DepartAt))
                    problems.Add("tài xế đã có chuyến khác trùng giờ");
                if (previous != null && p.DepartAt < previous.ArriveAt)
                    problems.Add("trùng với chuyến cùng lịch của ngày trước (tuyến chạy quá 24 giờ)");

                if (problems.Count > 0)
                {
                    var text = string.Join("; ", problems);
                    p.Problem = char.ToUpper(text[0]) + text[1..];
                }
                previous = p;
            }
            return (null, plan);
        }

        private static object ShapePlan(List<PlanItem> plan) => new
        {
            TotalItems = plan.Count,
            ConflictCount = plan.Count(p => p.Problem != null),
            Items = plan.Select(p => new { p.DepartAt, p.ArriveAt, Ok = p.Problem == null, p.Problem }).ToList()
        };

        // Xem trước kết quả, không ghi gì vào CSDL; báo ngày nào bị trùng giờ hoặc vướng giấy tờ
        [Authorize(Roles = AppRoles.AdminOperator)]
        [HttpPost("generate-schedule/preview")]
        public async Task<IActionResult> PreviewSchedule([FromBody] GenerateScheduleDto dto)
        {
            var (error, plan) = await BuildPlanAsync(dto);
            if (error != null) return BadRequest(new { message = error });
            return Ok(ShapePlan(plan));
        }

        // Tạo lịch: nếu bất kỳ ngày nào bị trùng giờ hoặc vướng giấy tờ thì từ chối cả lịch (không tạo dở dang)
        [Authorize(Roles = AppRoles.AdminOperator)]
        [HttpPost("generate-schedule")]
        public async Task<IActionResult> GenerateFixedSchedule([FromBody] GenerateScheduleDto dto)
        {
            var (error, plan) = await BuildPlanAsync(dto);
            if (error != null) return BadRequest(new { message = error });

            var bad = plan.Where(p => p.Problem != null).ToList();
            if (bad.Count > 0)
            {
                var sample = string.Join(" | ", bad.Take(5).Select(p => $"{p.DepartAt:dd/MM} lúc {p.DepartAt:HH:mm}: {p.Problem}"));
                var more = bad.Count > 5 ? $" ... và {bad.Count - 5} ngày khác" : "";
                return BadRequest(new
                {
                    message = $"Không tạo lịch vì có {bad.Count} chuyến bị từ chối: {sample}{more}. Hãy bấm Xem trước để chỉnh lại.",
                    conflicts = bad.Select(p => new { p.DepartAt, p.Problem })
                });
            }

            var createdTrips = plan.Select(p => new Trip
            {
                BusRouteId = dto.BusRouteId,
                VehicleId = dto.VehicleId,
                DriverId = dto.DriverId,
                DepartAt = p.DepartAt,
                ArriveAt = p.ArriveAt,
                Price = dto.Price,
                Status = TripStatus.Open
            }).ToList();

            _context.Trips.AddRange(createdTrips);
            await _context.SaveChangesAsync();
            await _audit.AuditAsync("GenerateSchedule", "Trip", null,
                $"Tạo lịch chạy {createdTrips.Count} chuyến, tuyến #{dto.BusRouteId}, xe #{dto.VehicleId}, tài xế #{dto.DriverId}, từ {dto.FromDate:dd/MM/yyyy} đến {dto.ToDate:dd/MM/yyyy} lúc {dto.DepartureTime}");

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

        [Authorize(Roles = AppRoles.AdminOperator)]
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
            await _audit.AuditAsync("Create", "Trip", trip.Id, $"Tạo chuyến #{trip.Id}: {route.Name} lúc {trip.DepartAt:dd/MM/yyyy HH:mm}, xe {vehicle.Plate}, tài xế {driver.FullName}");

            return Ok(trip);
        }

        [Authorize(Roles = AppRoles.AdminOperator)]
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
            await _audit.AuditAsync("Update", "Trip", trip.Id, $"Chuyến #{trip.Id}: đổi sang xe {newVehicle.Plate}" + (dto.NewDriverId.HasValue ? $", tài xế #{trip.DriverId}" : ""));

            return Ok(new
            {
                message = "Đổi phương tiện / phân công lại thành công.",
                TripId = trip.Id,
                NewVehiclePlate = newVehicle.Plate,
                DriverId = trip.DriverId
            });
        }

        // ===== Tài xế: chuyến của tôi (việc 40) =====
        // Không trả giá vé hay thanh toán: tài xế không được xem tiền.
        [Authorize(Roles = AppRoles.Driver)]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine([FromQuery] bool includePast = false)
        {
            var driver = await CurrentDriverAsync();
            if (driver == null)
                return NotFound(new { message = "Tài khoản chưa được liên kết với hồ sơ tài xế. Hãy nhờ quản trị viên liên kết." });

            var since = DateTime.Today.AddDays(-1);
            var q = _context.Trips.AsNoTracking().Where(t => t.DriverId == driver.Id);
            if (!includePast)
                q = q.Where(t => t.DepartAt >= since || t.Status == TripStatus.Running);

            var items = await q.OrderBy(t => t.DepartAt).Take(200)
                .Select(t => new
                {
                    t.Id,
                    RouteName = t.BusRoute.Name,
                    FromStation = t.BusRoute.FromStation.Name,
                    ToStation = t.BusRoute.ToStation.Name,
                    VehiclePlate = t.Vehicle.Plate,
                    t.DepartAt,
                    t.ArriveAt,
                    t.Status,
                    TotalSeats = t.Vehicle.VehicleType.SeatCount,
                    BookedSeats = t.Tickets.Count(tk => tk.Status != "Cancelled")
                })
                .ToListAsync();

            return Ok(new { DriverName = driver.FullName, Items = items });
        }

        [Authorize(Roles = AppRoles.Driver)]
        [HttpGet("mine/{id:int}")]
        public async Task<IActionResult> GetMineDetail(int id)
        {
            var driver = await CurrentDriverAsync();
            if (driver == null)
                return NotFound(new { message = "Tài khoản chưa được liên kết với hồ sơ tài xế." });

            var trip = await _context.Trips.AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new
                {
                    t.Id,
                    t.DriverId,
                    RouteName = t.BusRoute.Name,
                    FromStation = t.BusRoute.FromStation.Name,
                    ToStation = t.BusRoute.ToStation.Name,
                    VehiclePlate = t.Vehicle.Plate,
                    t.DepartAt,
                    t.ArriveAt,
                    t.Status,
                    TotalSeats = t.Vehicle.VehicleType.SeatCount
                })
                .FirstOrDefaultAsync();

            if (trip == null) return NotFound(new { message = "Không tìm thấy chuyến xe." });
            if (trip.DriverId != driver.Id) return NotOwner();

            var passengers = await LoadPassengersAsync(id);
            return Ok(new
            {
                trip.Id,
                trip.RouteName,
                trip.FromStation,
                trip.ToStation,
                trip.VehiclePlate,
                trip.DepartAt,
                trip.ArriveAt,
                trip.Status,
                trip.TotalSeats,
                Passengers = passengers
            });
        }

        private async Task<List<PassengerRow>> LoadPassengersAsync(int tripId) =>
            await _context.Tickets.AsNoTracking()
                .Where(k => k.TripId == tripId && k.Status != "Cancelled")
                .OrderBy(k => k.SeatCode)
                .Select(k => new PassengerRow { SeatCode = k.SeatCode, PassengerName = k.PassengerName, TicketCode = k.TicketCode, Status = k.Status })
                .ToListAsync();

        public class PassengerRow
        {
            public string SeatCode { get; set; } = "";
            public string? PassengerName { get; set; }
            public string TicketCode { get; set; } = "";
            public string Status { get; set; } = "";
        }

        // Danh sách hành khách: chỉ tên, ghế, mã vé (không giá vé, không thanh toán).
        // Admin, Operator, Seller xem mọi chuyến; Tài xế chỉ chuyến của mình.
        [Authorize(Roles = AppRoles.AdminOperatorSellerDriver)]
        [HttpGet("{id:int}/passengers")]
        public async Task<IActionResult> GetPassengers(int id)
        {
            var trip = await _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null) return NotFound(new { message = "Không tìm thấy chuyến xe." });

            var seesAll = IsStaff || User.IsInRole(AppRoles.Seller);
            if (!seesAll)
            {
                var driver = await CurrentDriverAsync();
                if (driver == null || driver.Id != trip.DriverId) return NotOwner();
            }

            return Ok(new { TripId = id, Passengers = await LoadPassengersAsync(id) });
        }

        // Quyền đổi trạng thái: Admin, Operator (mọi chuyến) và Tài xế (chỉ chuyến của mình: bắt đầu, báo trễ, hoàn thành)
        [Authorize(Roles = AppRoles.AdminOperatorDriver)]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateTripStatus(int id, [FromBody] UpdateTripStatusDto dto)
        {
            if (!TripStatus.All.Contains(dto.Status))
                return BadRequest(new { message = "Trạng thái chuyến không hợp lệ." });

            var trip = await _context.Trips
                .Include(t => t.BusRoute)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null) return NotFound(new { message = "Không tìm thấy chuyến xe." });

            var now = DateTime.Now;
            if (!IsStaff)
            {
                var driver = await CurrentDriverAsync();
                if (driver == null || driver.Id != trip.DriverId) return NotOwner();

                var error = DriverTransitionError(trip, dto, now);
                if (error != null) return BadRequest(new { message = error });
            }
            else if (trip.Status == TripStatus.Completed && dto.Status != TripStatus.Completed)
            {
                return BadRequest(new { message = "Chuyến đã hoàn thành, không thể đổi trạng thái." });
            }

            var oldStatus = trip.Status;
            trip.Status = dto.Status;

            if (dto.Status == TripStatus.Delayed && dto.NewDepartAt.HasValue)
            {
                trip.DepartAt = dto.NewDepartAt.Value;
                trip.ArriveAt = dto.NewDepartAt.Value.AddMinutes(trip.BusRoute.DurationMin);
            }

            await _context.SaveChangesAsync();
            await _audit.AuditAsync("Status", "Trip", trip.Id,
                $"Chuyến #{trip.Id}: {oldStatus} → {trip.Status}" + (dto.Status == TripStatus.Delayed && dto.NewDepartAt.HasValue ? $", giờ đi mới {trip.DepartAt:dd/MM/yyyy HH:mm}" : ""));
            return Ok(new
            {
                message = $"Đã cập nhật trạng thái chuyến xe sang '{dto.Status}'.",
                trip.Id,
                trip.Status,
                trip.DepartAt,
                trip.ArriveAt
            });
        }

        // Tài xế chỉ được: Bắt đầu (Open/Delayed → Running), Báo trễ (Open/Delayed → Delayed, kèm giờ mới), Hoàn thành (Running → Completed)
        private string? DriverTransitionError(Trip trip, UpdateTripStatusDto dto, DateTime now)
        {
            switch (dto.Status)
            {
                case TripStatus.Running:
                    if (trip.Status is not (TripStatus.Open or TripStatus.Delayed))
                        return "Chỉ bắt đầu được chuyến đang mở bán hoặc bị hoãn.";
                    if (now < trip.DepartAt.AddMinutes(-StartEarlyMinutes))
                        return $"Chưa đến giờ bắt đầu. Chỉ được bắt đầu trước giờ khởi hành tối đa {StartEarlyMinutes} phút.";
                    return null;

                case TripStatus.Delayed:
                    if (trip.Status is not (TripStatus.Open or TripStatus.Delayed))
                        return "Chỉ báo trễ được chuyến chưa chạy.";
                    if (!dto.NewDepartAt.HasValue || dto.NewDepartAt.Value <= trip.DepartAt)
                        return "Hãy nhập giờ khởi hành mới, muộn hơn giờ khởi hành hiện tại.";
                    return null;

                case TripStatus.Completed:
                    return trip.Status == TripStatus.Running ? null : "Chỉ hoàn thành được chuyến đang chạy.";

                default:
                    return "Tài xế chỉ được bắt đầu, báo trễ hoặc hoàn thành chuyến.";
            }
        }
    }
}