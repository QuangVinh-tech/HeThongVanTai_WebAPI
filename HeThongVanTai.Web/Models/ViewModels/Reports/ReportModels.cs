using HeThongVanTai.Web.Models.ViewModels.DanhMuc;

namespace HeThongVanTai.Web.Models.ViewModels.Reports;

public class RevenueRowItem
{
    public int RouteId { get; set; }
    public string RouteName { get; set; } = "";
    public string? FromStation { get; set; }
    public string? ToStation { get; set; }
    public int Payments { get; set; }
    public int Tickets { get; set; }
    public decimal Revenue { get; set; }
}

public class RevenueSummary
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TotalTickets { get; set; }
    public int TotalPayments { get; set; }
}

public class RevenuePage
{
    public List<RevenueRowItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public RevenueSummary Summary { get; set; } = new();

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(PageSize, 1)));
}

// Điều kiện lọc, sắp xếp, phân trang của trang báo cáo
public class ReportFilter
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? RouteId { get; set; }
    public string Sort { get; set; } = "revenue";
    public bool Desc { get; set; } = true;
    public int Page { get; set; } = 1;
}

public class ReportViewModel
{
    public ReportFilter Filter { get; set; } = new();
    public RevenuePage? Data { get; set; }
    public List<RouteItem> Routes { get; set; } = new();
    public string? Error { get; set; }
}

// ===== Báo cáo mở rộng (việc 34) =====

public class BreakdownRowItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Sub { get; set; }
    public int Payments { get; set; }
    public int Tickets { get; set; }
    public decimal Revenue { get; set; }
}

public class BreakdownPage
{
    public string By { get; set; } = "";
    public List<BreakdownRowItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public RevenueSummary Summary { get; set; } = new();

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(PageSize, 1)));
}

public class BreakdownViewModel
{
    public string By { get; set; } = "day";
    public string Title { get; set; } = "";
    public string NameColumn { get; set; } = "";
    public bool TimeSeries { get; set; }
    public ReportFilter Filter { get; set; } = new();
    public BreakdownPage? Data { get; set; }
    public List<RouteItem> Routes { get; set; } = new();
    public string? Error { get; set; }
}

public class DebtCustomerItem
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

public class DebtDetailItem
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

public class DebtSummaryItem
{
    public decimal TotalDebt { get; set; }
    public int Customers { get; set; }
    public int Shipments { get; set; }
    public decimal Within30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Over60 { get; set; }
}

public class DebtPage
{
    public List<DebtCustomerItem> Items { get; set; } = new();
    public List<DebtDetailItem>? Details { get; set; }
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public DebtSummaryItem Summary { get; set; } = new();

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(PageSize, 1)));
}

public class DebtViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? FeePayer { get; set; }
    public int Page { get; set; } = 1;
    public DebtPage? Data { get; set; }
    public string? Error { get; set; }
}

public class ReconItem
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

public class ReconSummaryItem
{
    public decimal BankReceived { get; set; }
    public decimal BankRefunded { get; set; }
    public int BankCount { get; set; }
    public decimal CashReceived { get; set; }
    public decimal CashRefunded { get; set; }
    public int IssueCount { get; set; }

    public decimal BankNet => BankReceived - BankRefunded;
    public decimal CashNet => CashReceived - CashRefunded;
}

public class ReconPage
{
    public List<ReconItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public ReconSummaryItem Summary { get; set; } = new();

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(PageSize, 1)));
}

public class ReconViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool OnlyIssues { get; set; }
    public int Page { get; set; } = 1;
    public ReconPage? Data { get; set; }
    public string? Error { get; set; }
}

public static class ReportLabels
{
    public static string FeePayer(string v) => v switch
    {
        "Sender" => "Người gửi trả",
        "Receiver" => "Người nhận trả",
        "Debt" => "Ghi nợ",
        _ => v
    };
}
