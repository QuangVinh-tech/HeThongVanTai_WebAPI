using HeThongVanTai.Data;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

// ===================== Kết quả báo cáo =====================

/// <summary>Một khoản thu (Paid) hoặc hoàn (Refunded) đã gắn với tuyến, xe, tài xế, nhân viên bán.</summary>
public record RevenueFact(decimal Amount, string Status, DateTime PaidAt, int Tickets,
                          int? RouteId, int? VehicleId, int? DriverId, string? SoldBy);

public class BreakdownRow
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Sub { get; set; }
    public int Payments { get; set; }
    public int Tickets { get; set; }
    public decimal Revenue { get; set; }
}

public class BreakdownSummary
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TotalTickets { get; set; }
    public int TotalPayments { get; set; }
}

public class BreakdownResult
{
    public string By { get; set; } = "";
    public List<BreakdownRow> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
    public BreakdownSummary Summary { get; set; } = new();
}

public class DebtCustomerRow
{
    public string SenderName { get; set; } = "";
    public string SenderPhone { get; set; } = "";
    public int Shipments { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OldestAt { get; set; }
    public int OldestDays { get; set; }
    public decimal Within30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Over60 { get; set; }
}

public class DebtDetailRow
{
    public string Code { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string SenderName { get; set; } = "";
    public string SenderPhone { get; set; } = "";
    public string ReceiverName { get; set; } = "";
    public string FeePayer { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int AgeDays { get; set; }
}

public class DebtSummary
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public decimal TotalDebt { get; set; }
    public int Customers { get; set; }
    public int Shipments { get; set; }
    public decimal Within30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Over60 { get; set; }
}

public class DebtReport
{
    public List<DebtCustomerRow> Items { get; set; } = new();
    public List<DebtDetailRow>? Details { get; set; }   // chỉ có khi all = true (xuất Excel)
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
    public DebtSummary Summary { get; set; } = new();
}

public class ReconRow
{
    public int? PaymentId { get; set; }
    public DateTime? PaidAt { get; set; }
    public string BookingCode { get; set; } = "";
    public string BookingStatus { get; set; } = "";
    public decimal BookingTotal { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string? SePayTransactionId { get; set; }
    public string? BankReference { get; set; }
    public decimal RefundedAmount { get; set; }
    public List<string> Issues { get; set; } = new();
    public string IssueText { get; set; } = "";
}

public class ReconSummary
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public decimal BankReceived { get; set; }
    public decimal BankRefunded { get; set; }
    public decimal BankNet => BankReceived - BankRefunded;
    public int BankCount { get; set; }
    public decimal CashReceived { get; set; }
    public decimal CashRefunded { get; set; }
    public decimal CashNet => CashReceived - CashRefunded;
    public int IssueCount { get; set; }
}

public class ReconReport
{
    public List<ReconRow> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
    public ReconSummary Summary { get; set; } = new();
}

// ===================== Dịch vụ =====================

/// <summary>
/// Báo cáo cho Admin và Kế toán: doanh thu theo ngày, tháng, tuyến, xe, tài xế, nhân viên bán;
/// công nợ vận chuyển; đối soát chuyển khoản.
/// Mọi báo cáo doanh thu dùng chung một cách tính (LoadFactsAsync) nên tổng luôn khớp nhau và khớp báo cáo theo tuyến.
/// </summary>
public class ReportService
{
    public const int MaxRangeDays = 366;
    public const string UnknownSeller = "Khách tự đặt hoặc chưa ghi nhận";

    public static readonly string[] Dimensions = { "day", "month", "route", "vehicle", "driver", "seller" };

    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

    public static void CheckRange(DateTime start, DateTime end)
    {
        if (start.Date > end.Date)
            throw new ServiceException(400, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if ((end.Date - start.Date).TotalDays > MaxRangeDays)
            throw new ServiceException(400, $"Khoảng thời gian tối đa {MaxRangeDays} ngày.");
    }

    /// <summary>
    /// Doanh thu = tiền đã thu (Paid) trừ tiền đã hoàn (Refunded), tính theo ngày thu. Mỗi khoản gắn với chuyến của
    /// vé đầu tiên trong đặt vé (tuyến, xe, tài xế) và nhân viên bán của đặt vé đó.
    /// </summary>
    public async Task<List<RevenueFact>> LoadFactsAsync(DateTime start, DateTime end, int? routeId)
    {
        CheckRange(start, end);
        var rangeFrom = start.Date;
        var endExclusive = end.Date.AddDays(1);

        var query = _db.Payments.AsNoTracking()
            .Where(p => (p.Status == "Paid" || p.Status == "Refunded")
                        && p.PaidAt != null && p.PaidAt >= rangeFrom && p.PaidAt < endExclusive);
        if (routeId.HasValue)
        {
            var rid = routeId.Value;
            query = query.Where(p => p.Booking.Tickets.Any(t => t.Trip.BusRouteId == rid));
        }

        var rows = await query
            .Select(p => new
            {
                p.Amount,
                p.Status,
                PaidAt = p.PaidAt!.Value,
                Tickets = p.Booking.Tickets.Count(t => t.Status != "Cancelled"),
                RouteId = p.Booking.Tickets.OrderBy(t => t.Id).Select(t => (int?)t.Trip.BusRouteId).FirstOrDefault(),
                VehicleId = p.Booking.Tickets.OrderBy(t => t.Id).Select(t => (int?)t.Trip.VehicleId).FirstOrDefault(),
                DriverId = p.Booking.Tickets.OrderBy(t => t.Id).Select(t => (int?)t.Trip.DriverId).FirstOrDefault(),
                p.Booking.SoldBy
            })
            .ToListAsync();

        // Khoản không gắn được với chuyến nào thì bỏ, giống báo cáo theo tuyến, để mọi báo cáo cùng một tổng
        return rows
            .Where(r => r.RouteId != null)
            .Select(r => new RevenueFact(r.Amount, r.Status, r.PaidAt, r.Tickets, r.RouteId, r.VehicleId, r.DriverId, r.SoldBy))
            .ToList();
    }

    private static BreakdownRow ToRow(string key, string label, string? sub, IEnumerable<RevenueFact> g)
    {
        var list = g.ToList();
        var paid = list.Where(f => f.Status == "Paid").ToList();
        return new BreakdownRow
        {
            Key = key,
            Label = label,
            Sub = sub,
            Payments = paid.Count,
            Tickets = paid.Sum(f => f.Tickets),
            Revenue = list.Sum(f => f.Status == "Paid" ? f.Amount : -f.Amount)
        };
    }

    public async Task<BreakdownResult> RevenueByAsync(string by, DateTime start, DateTime end, int? routeId,
        string? sortBy, bool desc, int page, int pageSize, bool all)
    {
        by = (by ?? "").ToLowerInvariant();
        if (!Dimensions.Contains(by))
            throw new ServiceException(400, "Loại báo cáo không hợp lệ. Chọn một trong: " + string.Join(", ", Dimensions) + ".");

        var facts = await LoadFactsAsync(start, end, routeId);
        var rangeFrom = start.Date;
        var rangeTo = end.Date;
        var rows = new List<BreakdownRow>();

        switch (by)
        {
            case "day":
                var byDay = facts.GroupBy(f => f.PaidAt.Date).ToDictionary(g => g.Key, g => g.AsEnumerable());
                // Đủ mọi ngày trong khoảng (ngày không có doanh thu hiện 0) để biểu đồ liền mạch
                for (var d = rangeFrom; d <= rangeTo; d = d.AddDays(1))
                    rows.Add(ToRow(d.ToString("yyyy-MM-dd"), d.ToString("dd/MM/yyyy"), null,
                        byDay.TryGetValue(d, out var dayFacts) ? dayFacts : Enumerable.Empty<RevenueFact>()));
                break;

            case "month":
                var byMonth = facts.GroupBy(f => new DateTime(f.PaidAt.Year, f.PaidAt.Month, 1)).ToDictionary(g => g.Key, g => g.AsEnumerable());
                for (var m = new DateTime(rangeFrom.Year, rangeFrom.Month, 1); m <= new DateTime(rangeTo.Year, rangeTo.Month, 1); m = m.AddMonths(1))
                    rows.Add(ToRow(m.ToString("yyyy-MM"), m.ToString("MM/yyyy"), null,
                        byMonth.TryGetValue(m, out var monthFacts) ? monthFacts : Enumerable.Empty<RevenueFact>()));
                break;

            case "route":
                var routes = await _db.BusRoutes.AsNoTracking()
                    .Select(r => new { r.Id, r.Name, From = r.FromStation.Name, To = r.ToStation.Name })
                    .ToDictionaryAsync(r => r.Id);
                foreach (var g in facts.GroupBy(f => f.RouteId!.Value))
                {
                    routes.TryGetValue(g.Key, out var r);
                    rows.Add(ToRow(g.Key.ToString(), r?.Name ?? $"Tuyến #{g.Key}", r == null ? null : $"{r.From} → {r.To}", g));
                }
                break;

            case "vehicle":
                var vehicles = await _db.Vehicles.AsNoTracking()
                    .Select(v => new { v.Id, v.Plate, Type = v.VehicleType.Name })
                    .ToDictionaryAsync(v => v.Id);
                foreach (var g in facts.GroupBy(f => f.VehicleId ?? 0))
                {
                    vehicles.TryGetValue(g.Key, out var v);
                    rows.Add(ToRow(g.Key.ToString(), v?.Plate ?? $"Xe #{g.Key}", v?.Type, g));
                }
                break;

            case "driver":
                var drivers = await _db.Drivers.AsNoTracking()
                    .Select(d => new { d.Id, d.FullName, d.Phone })
                    .ToDictionaryAsync(d => d.Id);
                foreach (var g in facts.GroupBy(f => f.DriverId ?? 0))
                {
                    drivers.TryGetValue(g.Key, out var d);
                    rows.Add(ToRow(g.Key.ToString(), d?.FullName ?? $"Tài xế #{g.Key}", d?.Phone, g));
                }
                break;

            case "seller":
                foreach (var g in facts.GroupBy(f => string.IsNullOrWhiteSpace(f.SoldBy) ? "" : f.SoldBy!.Trim()))
                    rows.Add(ToRow(g.Key, g.Key.Length == 0 ? UnknownSeller : g.Key, null, g));
                break;
        }

        var summary = new BreakdownSummary
        {
            From = rangeFrom.ToString("yyyy-MM-dd"),
            To = rangeTo.ToString("yyyy-MM-dd"),
            TotalRevenue = rows.Sum(r => r.Revenue),
            TotalTickets = rows.Sum(r => r.Tickets),
            TotalPayments = rows.Sum(r => r.Payments)
        };

        // Theo ngày và tháng luôn xếp theo thời gian; các loại còn lại xếp theo cột chọn (mặc định doanh thu giảm dần)
        List<BreakdownRow> ordered;
        if (by is "day" or "month")
        {
            ordered = (desc ? rows.OrderByDescending(r => r.Key) : rows.OrderBy(r => r.Key)).ToList();
        }
        else
        {
            IOrderedEnumerable<BreakdownRow> sorted = (sortBy?.ToLowerInvariant()) switch
            {
                "name" or "label" or "route" => desc ? rows.OrderByDescending(r => r.Label) : rows.OrderBy(r => r.Label),
                "tickets" => desc ? rows.OrderByDescending(r => r.Tickets) : rows.OrderBy(r => r.Tickets),
                "payments" => desc ? rows.OrderByDescending(r => r.Payments) : rows.OrderBy(r => r.Payments),
                _ => desc ? rows.OrderByDescending(r => r.Revenue) : rows.OrderBy(r => r.Revenue)
            };
            ordered = sorted.ThenBy(r => r.Label).ToList();
        }

        var result = new BreakdownResult { By = by, Total = ordered.Count, Summary = summary };
        Page(ordered, page, pageSize, all, out var items, out var p, out var ps);
        result.Items = items;
        result.Page = p;
        result.PageSize = ps;
        return result;
    }

    // ===================== Công nợ vận chuyển =====================

    /// <summary>
    /// Công nợ: đơn vận chuyển chưa thu tiền (Unpaid, không tính đơn đã hủy), gom theo người gửi, chia theo tuổi nợ.
    /// rangeFrom, rangeTo lọc theo ngày tạo đơn (bỏ trống là không giới hạn).
    /// </summary>
    public async Task<DebtReport> DebtAsync(DateTime? rangeFrom, DateTime? rangeTo, string? feePayer,
        int page, int pageSize, bool all, DateTime? today = null)
    {
        if (rangeFrom.HasValue && rangeTo.HasValue) CheckRange(rangeFrom.Value, rangeTo.Value);
        var payer = string.IsNullOrWhiteSpace(feePayer) ? null : feePayer.Trim();
        if (payer != null && payer is not ("Sender" or "Receiver" or "Debt"))
            throw new ServiceException(400, "Người trả cước không hợp lệ (Sender, Receiver hoặc Debt).");

        var query = _db.Shipments.AsNoTracking()
            .Where(s => s.PaymentStatus == "Unpaid" && s.Status != "Cancelled");
        if (rangeFrom.HasValue) { var f = rangeFrom.Value.Date; query = query.Where(s => s.CreatedAt >= f); }
        if (rangeTo.HasValue) { var e = rangeTo.Value.Date.AddDays(1); query = query.Where(s => s.CreatedAt < e); }
        if (payer != null) query = query.Where(s => s.FeePayer == payer);

        var shipments = await query
            .Select(s => new { s.Code, s.CreatedAt, s.SenderName, s.SenderPhone, s.ReceiverName, s.FeePayer, s.Status, s.TotalAmount })
            .ToListAsync();

        var now = (today ?? DateTime.Now).Date;
        var details = shipments
            .Select(s => new DebtDetailRow
            {
                Code = s.Code, CreatedAt = s.CreatedAt, SenderName = s.SenderName, SenderPhone = s.SenderPhone,
                ReceiverName = s.ReceiverName, FeePayer = s.FeePayer, Status = s.Status, TotalAmount = s.TotalAmount,
                AgeDays = Math.Max(0, (now - s.CreatedAt.Date).Days)
            })
            .OrderByDescending(d => d.AgeDays).ThenBy(d => d.Code)
            .ToList();

        var customers = details
            .GroupBy(d => d.SenderPhone)
            .Select(g => new DebtCustomerRow
            {
                SenderPhone = g.Key,
                SenderName = g.OrderByDescending(d => d.CreatedAt).First().SenderName,   // tên theo đơn gần nhất
                Shipments = g.Count(),
                TotalAmount = g.Sum(d => d.TotalAmount),
                OldestAt = g.Min(d => d.CreatedAt),
                OldestDays = g.Max(d => d.AgeDays),
                Within30 = g.Where(d => d.AgeDays <= 30).Sum(d => d.TotalAmount),
                Days31To60 = g.Where(d => d.AgeDays is > 30 and <= 60).Sum(d => d.TotalAmount),
                Over60 = g.Where(d => d.AgeDays > 60).Sum(d => d.TotalAmount)
            })
            .OrderByDescending(c => c.TotalAmount).ThenBy(c => c.SenderName)
            .ToList();

        var report = new DebtReport
        {
            Total = customers.Count,
            Summary = new DebtSummary
            {
                From = rangeFrom?.ToString("yyyy-MM-dd") ?? "",
                To = rangeTo?.ToString("yyyy-MM-dd") ?? "",
                TotalDebt = customers.Sum(c => c.TotalAmount),
                Customers = customers.Count,
                Shipments = details.Count,
                Within30 = customers.Sum(c => c.Within30),
                Days31To60 = customers.Sum(c => c.Days31To60),
                Over60 = customers.Sum(c => c.Over60)
            }
        };
        Page(customers, page, pageSize, all, out var items, out var p, out var ps);
        report.Items = items;
        report.Page = p;
        report.PageSize = ps;
        if (all) report.Details = details;
        return report;
    }

    // ===================== Đối soát chuyển khoản =====================

    /// <summary>
    /// Đối soát: các giao dịch chuyển khoản đã thanh toán trong khoảng ngày, kèm cảnh báo bất thường
    /// (lệch số tiền, nhiều giao dịch cho một đặt vé, thiếu mã giao dịch ngân hàng, đặt vé hết hạn mà vẫn có tiền)
    /// và các đặt vé đã Paid nhưng không có giao dịch nào. Tổng hợp thêm tiền mặt để kế toán đối chiếu quỹ.
    /// Lưu ý: giao dịch ngân hàng SePay không tự khớp được (sai số tiền, đặt vé hết hạn) hiện chỉ ghi log,
    /// chưa lưu CSDL nên chưa xuất hiện ở báo cáo này.
    /// </summary>
    public async Task<ReconReport> ReconciliationAsync(DateTime start, DateTime end, bool onlyIssues,
        int page, int pageSize, bool all)
    {
        CheckRange(start, end);
        var rangeFrom = start.Date;
        var endExclusive = end.Date.AddDays(1);

        var paid = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == "Paid" && p.PaidAt != null && p.PaidAt >= rangeFrom && p.PaidAt < endExclusive)
            .Select(p => new
            {
                p.Id, p.Amount, p.Method, p.PaidAt, p.SePayTransactionId, p.BankReference,
                BookingCode = p.Booking.Code,
                BookingStatus = p.Booking.Status,
                BookingTotal = p.Booking.TotalAmount,
                p.BookingId,
                PaidCount = _db.Payments.Count(x => x.BookingId == p.BookingId && x.Status == "Paid")
            })
            .ToListAsync();

        // Tiền đã hoàn của từng đặt vé (mọi thời điểm), cộng trong bộ nhớ vì SQLite (dùng khi test) không cộng được cột decimal
        var bookingIds = paid.Select(x => x.BookingId).Distinct().ToList();
        var refundRows = await _db.Payments.AsNoTracking()
            .Where(r => r.Status == "Refunded" && bookingIds.Contains(r.BookingId))
            .Select(r => new { r.BookingId, r.Amount })
            .ToListAsync();
        var refundedBy = refundRows.GroupBy(r => r.BookingId).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

        var refundsInRange = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == "Refunded" && p.PaidAt != null && p.PaidAt >= rangeFrom && p.PaidAt < endExclusive)
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync();

        var rows = new List<ReconRow>();
        foreach (var p in paid.Where(x => x.Method == "BankTransfer"))
        {
            var issues = new List<string>();
            if (p.Amount != p.BookingTotal)
                issues.Add($"Lệch số tiền: nhận {p.Amount:N0}, đặt vé {p.BookingTotal:N0}");
            if (p.PaidCount > 1)
                issues.Add("Đặt vé có nhiều hơn một giao dịch đã thanh toán");
            if (string.IsNullOrWhiteSpace(p.SePayTransactionId))
                issues.Add("Chưa có mã giao dịch ngân hàng (có thể xác nhận thủ công)");
            if (p.BookingStatus is "Expired" or "Holding")
                issues.Add($"Đặt vé đang ở trạng thái {p.BookingStatus} nhưng đã có tiền");

            rows.Add(new ReconRow
            {
                PaymentId = p.Id, PaidAt = p.PaidAt, BookingCode = p.BookingCode, BookingStatus = p.BookingStatus,
                BookingTotal = p.BookingTotal, Amount = p.Amount, Method = p.Method,
                SePayTransactionId = p.SePayTransactionId, BankReference = p.BankReference,
                RefundedAmount = refundedBy.GetValueOrDefault(p.BookingId), Issues = issues, IssueText = string.Join("; ", issues)
            });
        }

        // Đặt vé đã Paid mà không có giao dịch Paid nào
        var orphans = await _db.Bookings.AsNoTracking()
            .Where(b => b.Status == "Paid" && b.CreatedAt >= rangeFrom && b.CreatedAt < endExclusive
                        && !b.Payments.Any(x => x.Status == "Paid"))
            .Select(b => new { b.Code, b.Status, b.TotalAmount, b.CreatedAt })
            .ToListAsync();
        foreach (var b in orphans)
        {
            const string text = "Đặt vé đã thanh toán nhưng không có giao dịch nào";
            rows.Add(new ReconRow
            {
                PaymentId = null, PaidAt = null, BookingCode = b.Code, BookingStatus = b.Status,
                BookingTotal = b.TotalAmount, Amount = 0, Method = "-", Issues = new() { text }, IssueText = text
            });
        }

        var summary = new ReconSummary
        {
            From = rangeFrom.ToString("yyyy-MM-dd"),
            To = end.Date.ToString("yyyy-MM-dd"),
            BankReceived = paid.Where(p => p.Method == "BankTransfer").Sum(p => p.Amount),
            BankCount = paid.Count(p => p.Method == "BankTransfer"),
            BankRefunded = refundsInRange.Where(r => r.Method == "BankTransfer").Sum(r => r.Amount),
            CashReceived = paid.Where(p => p.Method == "Cash").Sum(p => p.Amount),
            CashRefunded = refundsInRange.Where(r => r.Method == "Cash").Sum(r => r.Amount),
            IssueCount = rows.Count(r => r.Issues.Count > 0)
        };

        var shown = (onlyIssues ? rows.Where(r => r.Issues.Count > 0) : rows)
            .OrderByDescending(r => r.PaidAt ?? DateTime.MinValue).ThenBy(r => r.BookingCode)
            .ToList();

        var report = new ReconReport { Total = shown.Count, Summary = summary };
        Page(shown, page, pageSize, all, out var items, out var pg, out var ps);
        report.Items = items;
        report.Page = pg;
        report.PageSize = ps;
        return report;
    }

    // ===================== Phân trang =====================
    private static void Page<T>(List<T> all, int page, int pageSize, bool takeAll,
        out List<T> items, out int pageOut, out int pageSizeOut)
    {
        if (takeAll)
        {
            items = all;
            pageOut = 1;
            pageSizeOut = Math.Max(all.Count, 1);
            return;
        }
        pageOut = Math.Max(page, 1);
        pageSizeOut = Math.Clamp(pageSize, 1, 100);
        items = all.Skip((pageOut - 1) * pageSizeOut).Take(pageSizeOut).ToList();
    }
}
