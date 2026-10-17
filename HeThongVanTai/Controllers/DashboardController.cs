using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers
{
    // Số liệu cho trang Tổng quan của quản trị
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoles.AdminOperator)]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /api/Dashboard/get-summary?days=7
        // Doanh thu = tiền đã thu (Paid) trừ tiền đã hoàn (Refunded), tính theo ngày thu.
        [HttpGet("get-summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int days = 7)
        {
            days = Math.Clamp(days, 1, 31);
            var now = DateTime.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var firstDay = today.AddDays(-(days - 1));

            // Doanh thu từng ngày (ngày không có giao dịch vẫn trả về 0 để biểu đồ liên tục)
            var payments = await _db.Payments.AsNoTracking()
                .Where(p => (p.Status == "Paid" || p.Status == "Refunded")
                            && p.PaidAt != null && p.PaidAt >= firstDay && p.PaidAt < tomorrow)
                .Select(p => new { PaidAt = p.PaidAt!.Value, p.Amount, p.Status })
                .ToListAsync();

            var revenueByDay = Enumerable.Range(0, days)
                .Select(i => firstDay.AddDays(i))
                .Select(d => new
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Revenue = payments.Where(p => p.PaidAt.Date == d)
                        .Sum(p => p.Status == "Paid" ? p.Amount : -p.Amount)
                })
                .ToList();

            var revenueToday = revenueByDay.Last().Revenue;
            var revenueTotal = revenueByDay.Sum(x => x.Revenue);

            // Vé bán hôm nay: vé còn hiệu lực của các booking đã thanh toán, đặt trong ngày
            var ticketsSoldToday = await _db.Tickets.AsNoTracking()
                .CountAsync(t => t.Status != "Cancelled" && t.Booking.Status == "Paid"
                                 && t.Booking.CreatedAt >= today && t.Booking.CreatedAt < tomorrow);

            // Chuyến đang chạy: tài xế đã bấm bắt đầu (Running), hoặc đã quá giờ khởi hành mà chưa đến nơi, chưa hủy/hoàn thành
            var activeTrips = await _db.Trips.AsNoTracking()
                .Where(t => t.Status == "Running"
                            || (t.Status != "Cancelled" && t.Status != "Completed" && t.DepartAt <= now))
                .Select(t => new { t.Status, t.DepartAt, t.ArriveAt, t.BusRoute.DurationMin })
                .ToListAsync();
            var runningTrips = activeTrips.Count(t => t.Status == "Running"
                || (t.ArriveAt ?? t.DepartAt.AddMinutes(t.DurationMin)) >= now);

            // Tỉ lệ lấp đầy: ghế đã đặt / tổng ghế của các chuyến khởi hành trong hôm nay
            var todayTrips = await _db.Trips.AsNoTracking()
                .Where(t => t.Status != "Cancelled" && t.DepartAt >= today && t.DepartAt < tomorrow)
                .Select(t => new
                {
                    Seats = t.Vehicle.VehicleType.SeatCount,
                    Booked = t.Tickets.Count(k => k.Status != "Cancelled")
                })
                .ToListAsync();
            var totalSeats = todayTrips.Sum(t => t.Seats);
            double? occupancy = totalSeats > 0
                ? Math.Round(todayTrips.Sum(t => Math.Min(t.Booked, t.Seats)) * 100.0 / totalSeats, 1)
                : null;

            return Ok(new
            {
                RevenueToday = revenueToday,
                RevenuePeriod = revenueTotal,
                Days = days,
                TicketsSoldToday = ticketsSoldToday,
                RunningTrips = runningTrips,
                TripsToday = todayTrips.Count,
                OccupancyPercent = occupancy,
                RevenueByDay = revenueByDay
            });
        }

        // GET: /api/Dashboard/get-alerts?days=30
        // Giấy tờ hết hạn hoặc sắp hết hạn trong N ngày, và các chuyến bị hoãn.
        [HttpGet("get-alerts")]
        public async Task<IActionResult> GetAlerts([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 1, 365);
            var now = DateTime.Now;
            var today = now.Date;
            var limit = today.AddDays(days);
            var alerts = new List<AlertItem>();

            var vehicles = await _db.Vehicles.AsNoTracking()
                .Where(v => v.Status == "Active"
                            && ((v.RegistrationExpiry != null && v.RegistrationExpiry < limit)
                                || (v.InsuranceExpiry != null && v.InsuranceExpiry < limit)))
                .Select(v => new { v.Id, v.Plate, v.RegistrationExpiry, v.InsuranceExpiry })
                .ToListAsync();
            foreach (var v in vehicles)
            {
                if (v.RegistrationExpiry is DateTime reg && reg < limit)
                    alerts.Add(Expiry("VehicleRegistration", v.Id, $"Xe {v.Plate}", "đăng kiểm", reg, today));
                if (v.InsuranceExpiry is DateTime ins && ins < limit)
                    alerts.Add(Expiry("VehicleInsurance", v.Id, $"Xe {v.Plate}", "bảo hiểm", ins, today));
            }

            var drivers = await _db.Drivers.AsNoTracking()
                .Where(d => d.Status == "Active" && d.LicenseExpiry != null && d.LicenseExpiry < limit)
                .Select(d => new { d.Id, d.FullName, d.LicenseExpiry })
                .ToListAsync();
            foreach (var d in drivers)
                alerts.Add(Expiry("DriverLicense", d.Id, $"Tài xế {d.FullName}", "GPLX", d.LicenseExpiry!.Value, today));

            // Chuyến bị hoãn (trễ) chưa qua quá 1 ngày
            var delayed = await _db.Trips.AsNoTracking()
                .Where(t => t.Status == "Delayed" && t.DepartAt >= today.AddDays(-1))
                .OrderBy(t => t.DepartAt)
                .Select(t => new { t.Id, Route = t.BusRoute.Name, t.DepartAt })
                .Take(20)
                .ToListAsync();
            foreach (var t in delayed)
            {
                alerts.Add(new AlertItem
                {
                    Type = "TripDelayed",
                    Severity = "bad",
                    RefId = t.Id,
                    Message = $"Chuyến {t.Route} bị hoãn, giờ khởi hành mới {t.DepartAt:HH:mm dd/MM/yyyy}",
                    Date = t.DepartAt
                });
            }

            // Nghiêm trọng (đã hết hạn, chuyến trễ) lên trước, rồi theo ngày gần nhất
            var result = alerts
                .OrderBy(a => a.Severity == "bad" ? 0 : 1)
                .ThenBy(a => a.Date)
                .Take(50)
                .ToList();
            return Ok(result);
        }

        private static AlertItem Expiry(string type, int id, string subject, string what, DateTime expiry, DateTime today)
        {
            var left = (expiry.Date - today).Days;
            return new AlertItem
            {
                Type = type,
                Severity = left < 0 ? "bad" : "warn",
                RefId = id,
                Message = left < 0
                    ? $"{subject}: {what} đã hết hạn {-left} ngày ({expiry:dd/MM/yyyy})"
                    : left == 0
                        ? $"{subject}: {what} hết hạn hôm nay ({expiry:dd/MM/yyyy})"
                        : $"{subject}: {what} còn {left} ngày ({expiry:dd/MM/yyyy})",
                Date = expiry
            };
        }

        private class AlertItem
        {
            public string Type { get; set; } = "";
            public string Severity { get; set; } = "warn";   // bad = nghiêm trọng, warn = sắp đến hạn
            public int RefId { get; set; }
            public string Message { get; set; } = "";
            public DateTime Date { get; set; }
        }
    }
}
