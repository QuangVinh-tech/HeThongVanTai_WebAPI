using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers
{
    // Báo cáo cho Admin và Kế toán
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoles.AdminAccountant)]
    public class ReportsController : ControllerBase
    {
        private const int MaxRangeDays = 366;
        private readonly AppDbContext _db;

        public ReportsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /api/Reports/get-revenue-by-route?from=2026-10-01&to=2026-10-31&routeId=3
        //        &sortBy=revenue|tickets|payments|route&desc=true&page=1&pageSize=10&all=false
        // Doanh thu = tiền đã thu (Paid) trừ tiền đã hoàn (Refunded), tính theo ngày thu, gom theo tuyến
        // của chuyến trong booking. all=true trả toàn bộ dòng (dùng để xuất Excel).
        [HttpGet("get-revenue-by-route")]
        public async Task<IActionResult> GetRevenueByRoute(
            [FromQuery(Name = "from")] DateTime? fromDate,
            [FromQuery(Name = "to")] DateTime? toDate,
            [FromQuery] int? routeId,
            [FromQuery] string? sortBy,
            [FromQuery] bool desc = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool all = false)
        {
            var today = DateTime.Now.Date;
            var start = (fromDate ?? today.AddDays(-29)).Date;
            var end = (toDate ?? today).Date;
            if (start > end)
                return BadRequest(new { message = "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc." });
            if ((end - start).TotalDays > MaxRangeDays)
                return BadRequest(new { message = $"Khoảng thời gian tối đa {MaxRangeDays} ngày." });
            var endExclusive = end.AddDays(1);

            var query = _db.Payments.AsNoTracking()
                .Where(p => (p.Status == "Paid" || p.Status == "Refunded")
                            && p.PaidAt != null && p.PaidAt >= start && p.PaidAt < endExclusive);
            if (routeId.HasValue)
            {
                var rid = routeId.Value;
                query = query.Where(p => p.Booking.Tickets.Any(t => t.Trip.BusRouteId == rid));
            }

            var payments = await query
                .Select(p => new
                {
                    p.Amount,
                    p.Status,
                    RouteId = p.Booking.Tickets.Select(t => (int?)t.Trip.BusRouteId).FirstOrDefault(),
                    Tickets = p.Booking.Tickets.Count(t => t.Status != "Cancelled")
                })
                .ToListAsync();

            var routes = await _db.BusRoutes.AsNoTracking()
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    From = r.FromStation.Name,
                    To = r.ToStation.Name
                })
                .ToDictionaryAsync(r => r.Id);

            var rows = payments
                .Where(p => p.RouteId != null)
                .GroupBy(p => p.RouteId!.Value)
                .Select(g =>
                {
                    routes.TryGetValue(g.Key, out var r);
                    return new RevenueRow
                    {
                        RouteId = g.Key,
                        RouteName = r?.Name ?? $"Tuyến #{g.Key}",
                        FromStation = r?.From,
                        ToStation = r?.To,
                        Payments = g.Count(p => p.Status == "Paid"),
                        Tickets = g.Where(p => p.Status == "Paid").Sum(p => p.Tickets),
                        Revenue = g.Sum(p => p.Status == "Paid" ? p.Amount : -p.Amount)
                    };
                })
                .ToList();

            // Tổng tính trên toàn bộ kết quả lọc, không chỉ trang hiện tại
            var summary = new
            {
                From = start.ToString("yyyy-MM-dd"),
                To = end.ToString("yyyy-MM-dd"),
                TotalRevenue = rows.Sum(r => r.Revenue),
                TotalTickets = rows.Sum(r => r.Tickets),
                TotalPayments = rows.Sum(r => r.Payments)
            };

            IOrderedEnumerable<RevenueRow> sorted = (sortBy?.ToLowerInvariant()) switch
            {
                "route" => desc ? rows.OrderByDescending(r => r.RouteName) : rows.OrderBy(r => r.RouteName),
                "tickets" => desc ? rows.OrderByDescending(r => r.Tickets) : rows.OrderBy(r => r.Tickets),
                "payments" => desc ? rows.OrderByDescending(r => r.Payments) : rows.OrderBy(r => r.Payments),
                _ => desc ? rows.OrderByDescending(r => r.Revenue) : rows.OrderBy(r => r.Revenue)
            };
            var ordered = sorted.ThenBy(r => r.RouteName).ToList();

            if (all)
            {
                return Ok(new { Items = ordered, Total = ordered.Count, Page = 1, PageSize = ordered.Count, Summary = summary });
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Ok(new { Items = items, Total = ordered.Count, Page = page, PageSize = pageSize, Summary = summary });
        }

        public class RevenueRow
        {
            public int RouteId { get; set; }
            public string RouteName { get; set; } = "";
            public string? FromStation { get; set; }
            public string? ToStation { get; set; }
            public int Payments { get; set; }
            public int Tickets { get; set; }
            public decimal Revenue { get; set; }
        }
    }
}
