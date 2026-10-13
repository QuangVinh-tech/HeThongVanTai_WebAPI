using System.Globalization;
using ClosedXML.Excel;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.Reports;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HeThongVanTai.Web.Controllers;

// Báo cáo doanh thu: Admin và Kế toán
[Authorize(Roles = AppRoles.AdminAccountant)]
[Route("Admin/Reports")]
public class ReportsController : Controller
{
    private const int PageSize = 10;
    private const int MaxRangeDays = 366;
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly string[] SortKeys = { "route", "payments", "tickets", "revenue" };

    private readonly ReportsApiService _svc;
    private readonly DanhMucApiService _danhMuc;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(ReportsApiService svc, DanhMucApiService danhMuc, ILogger<ReportsController> logger)
    {
        _svc = svc;
        _danhMuc = danhMuc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        int? routeId, string? sort, bool desc = true, int page = 1)
    {
        var filter = BuildFilter(fromDate, toDate, routeId, sort, desc, page);
        var model = new ReportViewModel { Filter = filter };

        var routes = await _danhMuc.GetRoutesAsync();
        model.Routes = routes.Data ?? new();

        var invalid = Validate(filter);
        if (invalid != null)
        {
            model.Error = invalid;
            return View(model);
        }

        var result = await _svc.GetRevenueByRouteAsync(filter, PageSize);
        if (result.Ok) model.Data = result.Data;
        else model.Error = result.Error ?? "Không tải được báo cáo. Vui lòng thử lại.";
        return View(model);
    }

    // Xuất toàn bộ kết quả lọc (không phân trang) ra file Excel
    [HttpGet("Export")]
    public async Task<IActionResult> Export(
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        int? routeId, string? sort, bool desc = true)
    {
        var filter = BuildFilter(fromDate, toDate, routeId, sort, desc, 1);
        var back = new RouteValueDictionary
        {
            ["from"] = filter.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = filter.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["routeId"] = filter.RouteId,
            ["sort"] = filter.Sort,
            ["desc"] = desc
        };

        var invalid = Validate(filter);
        if (invalid != null)
        {
            TempData["Error"] = invalid;
            return RedirectToAction(nameof(Index), back);
        }

        var result = await _svc.GetRevenueByRouteAsync(filter, 100, all: true);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không xuất được báo cáo. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index), back);
        }

        var routeLabel = "Tất cả tuyến";
        if (filter.RouteId.HasValue)
        {
            var routes = await _danhMuc.GetRoutesAsync();
            routeLabel = routes.Data?.FirstOrDefault(r => r.Id == filter.RouteId.Value)?.Name
                         ?? $"Tuyến #{filter.RouteId.Value}";
        }

        var bytes = BuildWorkbook(result.Data, filter, routeLabel);
        _logger.LogInformation("{User} xuất báo cáo doanh thu {From:yyyy-MM-dd} đến {To:yyyy-MM-dd}: {Rows} dòng",
            User.Identity?.Name, filter.From, filter.To, result.Data.Items.Count);

        var fileName = $"bao-cao-doanh-thu_{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.xlsx";
        return File(bytes, XlsxType, fileName);
    }

    private static ReportFilter BuildFilter(DateTime? fromDate, DateTime? toDate, int? routeId,
                                            string? sort, bool desc, int page)
    {
        var today = DateTime.Today;
        var key = (sort ?? "").ToLowerInvariant();
        return new ReportFilter
        {
            From = (fromDate ?? today.AddDays(-29)).Date,
            To = (toDate ?? today).Date,
            RouteId = routeId is > 0 ? routeId : null,
            Sort = SortKeys.Contains(key) ? key : "revenue",
            Desc = desc,
            Page = Math.Max(page, 1)
        };
    }

    private static string? Validate(ReportFilter f)
    {
        if (f.From > f.To) return "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.";
        if ((f.To - f.From).TotalDays > MaxRangeDays) return $"Khoảng thời gian tối đa {MaxRangeDays} ngày.";
        return null;
    }

    // Chuỗi bắt đầu bằng = + - @ có thể bị Excel hiểu là công thức; thêm dấu nháy để luôn là chữ
    private static string Text(string? value)
    {
        var s = value ?? "";
        return s.Length > 0 && "=+-@".Contains(s[0]) ? "'" + s : s;
    }

    private static byte[] BuildWorkbook(RevenuePage page, ReportFilter filter, string routeLabel)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Doanh thu theo tuyến");

        ws.Cell(1, 1).Value = "BÁO CÁO DOANH THU THEO TUYẾN";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Từ ngày {filter.From:dd/MM/yyyy} đến ngày {filter.To:dd/MM/yyyy}";
        ws.Cell(3, 1).Value = "Tuyến: " + Text(routeLabel);

        const int headerRow = 5;
        var headers = new[] { "STT", "Tuyến", "Bến đi", "Bến đến", "Số giao dịch", "Số vé", "Doanh thu (đ)" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(headerRow, c + 1).Value = headers[c];
        var head = ws.Range(headerRow, 1, headerRow, headers.Length);
        head.Style.Font.Bold = true;
        head.Style.Font.FontColor = XLColor.White;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#B03060");

        var row = headerRow + 1;
        var index = 1;
        foreach (var item in page.Items)
        {
            ws.Cell(row, 1).Value = index++;
            ws.Cell(row, 2).Value = Text(item.RouteName);
            ws.Cell(row, 3).Value = Text(item.FromStation);
            ws.Cell(row, 4).Value = Text(item.ToStation);
            ws.Cell(row, 5).Value = item.Payments;
            ws.Cell(row, 6).Value = item.Tickets;
            ws.Cell(row, 7).Value = item.Revenue;
            row++;
        }

        ws.Cell(row, 2).Value = "Tổng cộng";
        ws.Cell(row, 5).Value = page.Summary.TotalPayments;
        ws.Cell(row, 6).Value = page.Summary.TotalTickets;
        ws.Cell(row, 7).Value = page.Summary.TotalRevenue;
        ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDE7EC");

        ws.Range(headerRow + 1, 5, row, 7).Style.NumberFormat.Format = "#,##0";
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
