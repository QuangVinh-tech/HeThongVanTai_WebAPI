using System.Globalization;
using ClosedXML.Excel;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using HeThongVanTai.Web.Models.ViewModels.Reports;
using HeThongVanTai.Web.Models.ViewModels.VanChuyen;
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

    // ===================== Báo cáo mở rộng (việc 34) =====================

    private static readonly string[] Dimensions = { "day", "month", "route", "vehicle", "driver", "seller" };

    private static string DimTitle(string by) => by switch
    {
        "day" => "Doanh thu theo ngày",
        "month" => "Doanh thu theo tháng",
        "route" => "Doanh thu theo tuyến",
        "vehicle" => "Doanh thu theo xe",
        "driver" => "Doanh thu theo tài xế",
        "seller" => "Doanh thu theo nhân viên bán",
        _ => "Doanh thu"
    };

    private static string DimNameColumn(string by) => by switch
    {
        "day" => "Ngày",
        "month" => "Tháng",
        "route" => "Tuyến",
        "vehicle" => "Biển số xe",
        "driver" => "Tài xế",
        "seller" => "Nhân viên bán",
        _ => "Tên"
    };

    private static string NormalizeBy(string? by)
    {
        var key = (by ?? "").ToLowerInvariant();
        return Dimensions.Contains(key) ? key : "day";
    }

    // Theo ngày và tháng xếp theo thời gian (cũ đến mới) và hiện hết một lần; các loại còn lại xếp theo doanh thu, có phân trang
    private static ReportFilter BuildRevenueFilter(string by, DateTime? fromDate, DateTime? toDate, int? routeId,
                                                   string? sort, bool? desc, int page)
    {
        var timeSeries = by is "day" or "month";
        var filter = BuildFilter(fromDate, toDate, routeId, sort, desc ?? !timeSeries, page);
        if (by == "month" && fromDate == null)
            filter.From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);   // mặc định 12 tháng gần nhất
        return filter;
    }

    [HttpGet("Revenue")]
    public async Task<IActionResult> Revenue(
        string? by,
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        int? routeId, string? sort, bool? desc, int page = 1)
    {
        by = NormalizeBy(by);
        var filter = BuildRevenueFilter(by, fromDate, toDate, routeId, sort, desc, page);
        var model = new BreakdownViewModel
        {
            By = by, Title = DimTitle(by), NameColumn = DimNameColumn(by),
            TimeSeries = by is "day" or "month", Filter = filter
        };
        var routes = await _danhMuc.GetRoutesAsync();
        model.Routes = routes.Data ?? new();

        var invalid = Validate(filter);
        if (invalid != null)
        {
            model.Error = invalid;
            return View(model);
        }

        var result = await _svc.GetBreakdownAsync(by, filter, PageSize, all: model.TimeSeries);
        if (result.Ok) model.Data = result.Data;
        else model.Error = result.Error ?? "Không tải được báo cáo. Vui lòng thử lại.";
        return View(model);
    }

    [HttpGet("Revenue/Export")]
    public async Task<IActionResult> RevenueExport(
        string? by,
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        int? routeId, string? sort, bool? desc)
    {
        by = NormalizeBy(by);
        var filter = BuildRevenueFilter(by, fromDate, toDate, routeId, sort, desc, 1);
        var back = new RouteValueDictionary
        {
            ["by"] = by,
            ["from"] = filter.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = filter.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["routeId"] = filter.RouteId, ["sort"] = filter.Sort, ["desc"] = filter.Desc
        };

        var invalid = Validate(filter);
        if (invalid != null)
        {
            TempData["Error"] = invalid;
            return RedirectToAction(nameof(Revenue), back);
        }

        var result = await _svc.GetBreakdownAsync(by, filter, 100, all: true);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không xuất được báo cáo. Vui lòng thử lại.";
            return RedirectToAction(nameof(Revenue), back);
        }

        var routeLabel = "Tất cả tuyến";
        if (filter.RouteId.HasValue)
        {
            var routes = await _danhMuc.GetRoutesAsync();
            routeLabel = routes.Data?.FirstOrDefault(r => r.Id == filter.RouteId.Value)?.Name ?? $"Tuyến #{filter.RouteId.Value}";
        }

        var data = result.Data;
        var hasSub = by is "route" or "vehicle" or "driver";
        var headers = hasSub
            ? new[] { "STT", DimNameColumn(by), by == "route" ? "Bến đi - bến đến" : by == "vehicle" ? "Loại xe" : "Số điện thoại", "Số giao dịch", "Số vé", "Doanh thu (đ)" }
            : new[] { "STT", DimNameColumn(by), "Số giao dịch", "Số vé", "Doanh thu (đ)" };
        var index = 1;
        var rows = data.Items.Select(i => hasSub
            ? new object?[] { index++, i.Label, i.Sub ?? "", i.Payments, i.Tickets, i.Revenue }
            : new object?[] { index++, i.Label, i.Payments, i.Tickets, i.Revenue }).ToList();
        var total = hasSub
            ? new object?[] { null, "Tổng cộng", null, data.Summary.TotalPayments, data.Summary.TotalTickets, data.Summary.TotalRevenue }
            : new object?[] { null, "Tổng cộng", data.Summary.TotalPayments, data.Summary.TotalTickets, data.Summary.TotalRevenue };

        using var workbook = new XLWorkbook();
        ReportExcel.AddSheet(workbook, DimTitle(by).Length > 31 ? DimTitle(by)[..31] : DimTitle(by),
            DimTitle(by).ToUpperInvariant(),
            new[] { $"Từ ngày {filter.From:dd/MM/yyyy} đến ngày {filter.To:dd/MM/yyyy}", "Tuyến: " + routeLabel },
            headers, rows, total,
            numberCols: hasSub ? new[] { 4, 5, 6 } : new[] { 3, 4, 5 });

        _logger.LogInformation("{User} xuất báo cáo {By} {From:yyyy-MM-dd} đến {To:yyyy-MM-dd}: {Rows} dòng",
            User.Identity?.Name, by, filter.From, filter.To, data.Items.Count);
        return File(ReportExcel.ToBytes(workbook), ReportExcel.XlsxType,
            $"bao-cao-doanh-thu-theo-{by}_{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.xlsx");
    }

    // ----- Công nợ vận chuyển -----
    [HttpGet("Debt")]
    public async Task<IActionResult> Debt(
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        string? feePayer, int page = 1)
    {
        var model = new DebtViewModel
        {
            From = fromDate?.Date, To = toDate?.Date,
            FeePayer = feePayer is "Sender" or "Receiver" or "Debt" ? feePayer : null,
            Page = Math.Max(page, 1)
        };
        if (model.From > model.To)
        {
            model.Error = "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.";
            return View(model);
        }

        var result = await _svc.GetDebtAsync(model.From, model.To, model.FeePayer, model.Page, PageSize);
        if (result.Ok) model.Data = result.Data;
        else model.Error = result.Error ?? "Không tải được báo cáo. Vui lòng thử lại.";
        return View(model);
    }

    [HttpGet("Debt/Export")]
    public async Task<IActionResult> DebtExport(
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        string? feePayer)
    {
        var payer = feePayer is "Sender" or "Receiver" or "Debt" ? feePayer : null;
        var back = new RouteValueDictionary
        {
            ["from"] = fromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = toDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["feePayer"] = payer
        };

        var result = await _svc.GetDebtAsync(fromDate?.Date, toDate?.Date, payer, 1, 100, all: true);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không xuất được báo cáo. Vui lòng thử lại.";
            return RedirectToAction(nameof(Debt), back);
        }

        var data = result.Data;
        var info = new List<string>
        {
            "Ngày xuất: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            "Ngày tạo đơn: " + (fromDate == null && toDate == null ? "tất cả"
                : $"{fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}".Replace("  ", " ").Trim()),
            "Người trả cước: " + (payer == null ? "tất cả" : ReportLabels.FeePayer(payer))
        };

        using var workbook = new XLWorkbook();
        var i1 = 1;
        ReportExcel.AddSheet(workbook, "Công nợ theo khách", "BÁO CÁO CÔNG NỢ VẬN CHUYỂN", info,
            new[] { "STT", "Người gửi", "Số điện thoại", "Số đơn", "Tổng nợ (đ)", "Đơn cũ nhất", "Số ngày nợ lâu nhất", "Nợ 0-30 ngày (đ)", "Nợ 31-60 ngày (đ)", "Nợ trên 60 ngày (đ)" },
            data.Items.Select(c => new object?[] { i1++, c.SenderName, c.SenderPhone, c.Shipments, c.TotalAmount, c.OldestAt, c.OldestDays, c.Within30, c.Days31To60, c.Over60 }).ToList(),
            new object?[] { null, "Tổng cộng", null, data.Summary.Shipments, data.Summary.TotalDebt, null, null, data.Summary.Within30, data.Summary.Days31To60, data.Summary.Over60 },
            numberCols: new[] { 4, 5, 7, 8, 9, 10 }, dateTimeCols: new[] { 6 });

        var i2 = 1;
        ReportExcel.AddSheet(workbook, "Chi tiết đơn", "CHI TIẾT ĐƠN CHƯA THU TIỀN", info,
            new[] { "STT", "Mã vận đơn", "Ngày tạo", "Người gửi", "Số điện thoại", "Người nhận", "Người trả cước", "Trạng thái", "Số ngày", "Số tiền (đ)" },
            (data.Details ?? new()).Select(d => new object?[] { i2++, d.Code, d.CreatedAt, d.SenderName, d.SenderPhone, d.ReceiverName,
                ReportLabels.FeePayer(d.FeePayer), VanChuyenHelpers.StatusText(d.Status), d.AgeDays, d.TotalAmount }).ToList(),
            null, numberCols: new[] { 9, 10 }, dateTimeCols: new[] { 3 });

        _logger.LogInformation("{User} xuất báo cáo công nợ: {Rows} khách", User.Identity?.Name, data.Items.Count);
        return File(ReportExcel.ToBytes(workbook), ReportExcel.XlsxType, $"bao-cao-cong-no_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // ----- Đối soát chuyển khoản -----
    [HttpGet("Reconciliation")]
    public async Task<IActionResult> Reconciliation(
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        bool onlyIssues = false, int page = 1)
    {
        var today = DateTime.Today;
        var model = new ReconViewModel
        {
            From = (fromDate ?? today.AddDays(-29)).Date, To = (toDate ?? today).Date,
            OnlyIssues = onlyIssues, Page = Math.Max(page, 1)
        };
        var invalid = Validate(new ReportFilter { From = model.From, To = model.To });
        if (invalid != null)
        {
            model.Error = invalid;
            return View(model);
        }

        var result = await _svc.GetReconciliationAsync(model.From, model.To, model.OnlyIssues, model.Page, PageSize);
        if (result.Ok) model.Data = result.Data;
        else model.Error = result.Error ?? "Không tải được báo cáo. Vui lòng thử lại.";
        return View(model);
    }

    [HttpGet("Reconciliation/Export")]
    public async Task<IActionResult> ReconciliationExport(
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        bool onlyIssues = false)
    {
        var today = DateTime.Today;
        var from = (fromDate ?? today.AddDays(-29)).Date;
        var to = (toDate ?? today).Date;
        var back = new RouteValueDictionary
        {
            ["from"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["onlyIssues"] = onlyIssues
        };

        var invalid = Validate(new ReportFilter { From = from, To = to });
        if (invalid != null)
        {
            TempData["Error"] = invalid;
            return RedirectToAction(nameof(Reconciliation), back);
        }

        var result = await _svc.GetReconciliationAsync(from, to, onlyIssues, 1, 100, all: true);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không xuất được báo cáo. Vui lòng thử lại.";
            return RedirectToAction(nameof(Reconciliation), back);
        }

        var data = result.Data;
        var s = data.Summary;
        var info = new[]
        {
            $"Từ ngày {from:dd/MM/yyyy} đến ngày {to:dd/MM/yyyy}" + (onlyIssues ? " (chỉ giao dịch bất thường)" : ""),
            $"Chuyển khoản: nhận {s.BankReceived:N0} đ, hoàn {s.BankRefunded:N0} đ, còn lại {s.BankNet:N0} đ ({s.BankCount} giao dịch)",
            $"Tiền mặt: nhận {s.CashReceived:N0} đ, hoàn {s.CashRefunded:N0} đ, còn lại {s.CashNet:N0} đ",
            $"Số dòng bất thường: {s.IssueCount}"
        };

        var idx = 1;
        using var workbook = new XLWorkbook();
        ReportExcel.AddSheet(workbook, "Đối soát chuyển khoản", "BÁO CÁO ĐỐI SOÁT CHUYỂN KHOẢN", info,
            new[] { "STT", "Thời gian thanh toán", "Mã đặt vé", "Trạng thái đặt vé", "Số tiền đặt vé (đ)", "Số tiền nhận (đ)", "Đã hoàn (đ)", "Mã giao dịch SePay", "Mã tham chiếu ngân hàng", "Cảnh báo" },
            data.Items.Select(i => new object?[] { idx++, i.PaidAt, i.BookingCode, DatVeHelpers.BookingStatusText(i.BookingStatus),
                i.BookingTotal, i.Amount, i.RefundedAmount, i.SePayTransactionId ?? "", i.BankReference ?? "", i.IssueText }).ToList(),
            new object?[] { null, null, "Tổng cộng", null, null, data.Items.Sum(i => i.Amount), data.Items.Sum(i => i.RefundedAmount), null, null, null },
            numberCols: new[] { 5, 6, 7 }, dateTimeCols: new[] { 2 });

        _logger.LogInformation("{User} xuất báo cáo đối soát {From:yyyy-MM-dd} đến {To:yyyy-MM-dd}: {Rows} dòng",
            User.Identity?.Name, from, to, data.Items.Count);
        return File(ReportExcel.ToBytes(workbook), ReportExcel.XlsxType,
            $"bao-cao-doi-soat_{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
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
