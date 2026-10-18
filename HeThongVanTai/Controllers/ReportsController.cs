using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Services;
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
        private readonly ReportService _reports;

        public ReportsController(AppDbContext db, ReportService reports)
        {
            _db = db;
            _reports = reports;
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

            // Cùng một cách tính với các báo cáo theo ngày, tháng, xe, tài xế, nhân viên (ReportService.LoadFactsAsync)
            var payments = await _reports.LoadFactsAsync(start, end, routeId);

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

        // GET: /api/Reports/revenue-by?by=day|month|route|vehicle|driver|seller&from=&to=&routeId=
        //        &sortBy=revenue|tickets|payments|name&desc=true&page=1&pageSize=10&all=false
        // Doanh thu theo ngày, tháng, tuyến, xe, tài xế hoặc nhân viên bán; tổng luôn bằng báo cáo theo tuyến.
        [HttpGet("revenue-by")]
        public async Task<IActionResult> RevenueBy(
            [FromQuery] string by = "day",
            [FromQuery(Name = "from")] DateTime? fromDate = null,
            [FromQuery(Name = "to")] DateTime? toDate = null,
            [FromQuery] int? routeId = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool desc = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool all = false)
        {
            var today = DateTime.Now.Date;
            // Theo ngày và tháng xếp theo thời gian (desc=false là cũ đến mới); các loại khác xếp theo sortBy
            return Ok(await _reports.RevenueByAsync(by, fromDate ?? today.AddDays(-29), toDate ?? today,
                routeId, sortBy, desc, page, pageSize, all));
        }

        // GET: /api/Reports/debt?from=&to=&feePayer=Sender|Receiver|Debt&page=1&pageSize=10&all=false
        // Công nợ vận chuyển: đơn chưa thu tiền theo người gửi, chia tuổi nợ 0-30, 31-60, trên 60 ngày.
        [HttpGet("debt")]
        public async Task<IActionResult> Debt(
            [FromQuery(Name = "from")] DateTime? fromDate,
            [FromQuery(Name = "to")] DateTime? toDate,
            [FromQuery] string? feePayer,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool all = false) =>
            Ok(await _reports.DebtAsync(fromDate, toDate, feePayer, page, pageSize, all));

        // GET: /api/Reports/reconciliation?from=&to=&onlyIssues=false&page=1&pageSize=10&all=false
        // Đối soát chuyển khoản: giao dịch ngân hàng đã thanh toán, kèm cảnh báo bất thường.
        [HttpGet("reconciliation")]
        public async Task<IActionResult> Reconciliation(
            [FromQuery(Name = "from")] DateTime? fromDate,
            [FromQuery(Name = "to")] DateTime? toDate,
            [FromQuery] bool onlyIssues = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool all = false)
        {
            var today = DateTime.Now.Date;
            return Ok(await _reports.ReconciliationAsync(fromDate ?? today.AddDays(-29), toDate ?? today,
                onlyIssues, page, pageSize, all));
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
