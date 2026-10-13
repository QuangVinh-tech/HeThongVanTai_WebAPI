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
