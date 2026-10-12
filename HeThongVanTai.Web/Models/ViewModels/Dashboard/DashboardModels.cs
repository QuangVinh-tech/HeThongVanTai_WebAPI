namespace HeThongVanTai.Web.Models.ViewModels.Dashboard;

public class RevenuePoint
{
    public string Date { get; set; } = "";
    public decimal Revenue { get; set; }
}

public class DashboardSummary
{
    public decimal RevenueToday { get; set; }
    public decimal RevenuePeriod { get; set; }
    public int Days { get; set; }
    public int TicketsSoldToday { get; set; }
    public int RunningTrips { get; set; }
    public int TripsToday { get; set; }
    public double? OccupancyPercent { get; set; }
    public List<RevenuePoint> RevenueByDay { get; set; } = new();
}

public class DashboardAlert
{
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "warn";   // bad hoặc warn
    public int RefId { get; set; }
    public string Message { get; set; } = "";
    public DateTime Date { get; set; }
}

public class DashboardViewModel
{
    public DashboardSummary? Summary { get; set; }
    public string? SummaryError { get; set; }
    public List<DashboardAlert> Alerts { get; set; } = new();
    public string? AlertsError { get; set; }

    public bool HasRevenue => Summary != null && Summary.RevenueByDay.Any(p => p.Revenue != 0);
}
