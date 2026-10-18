using ClosedXML.Excel;

namespace HeThongVanTai.Web.Infrastructure;

/// <summary>Tạo file Excel cho các báo cáo: tiêu đề, dòng thông tin lọc, bảng dữ liệu, dòng tổng.</summary>
public static class ReportExcel
{
    public const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Chuỗi bắt đầu bằng = + - @ có thể bị Excel hiểu là công thức; thêm dấu nháy để luôn là chữ
    public static string Text(string? value)
    {
        var s = value ?? "";
        return s.Length > 0 && "=+-@".Contains(s[0]) ? "'" + s : s;
    }

    private static void SetCell(IXLCell cell, object? v)
    {
        switch (v)
        {
            case null: break;
            case string s: cell.Value = Text(s); break;
            case int i: cell.Value = i; break;
            case long l: cell.Value = l; break;
            case decimal d: cell.Value = d; break;
            case DateTime dt: cell.Value = dt; break;
            default: cell.Value = Text(v.ToString()); break;
        }
    }

    /// <param name="numberCols">Cột (tính từ 1) hiển thị số có dấu phân cách hàng nghìn.</param>
    /// <param name="dateTimeCols">Cột (tính từ 1) hiển thị ngày giờ dd/MM/yyyy HH:mm.</param>
    public static void AddSheet(XLWorkbook workbook, string sheetName, string title, IEnumerable<string> info,
        string[] headers, IEnumerable<object?[]> rows, object?[]? totalRow, int[]? numberCols = null, int[]? dateTimeCols = null)
    {
        var ws = workbook.Worksheets.Add(sheetName);

        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        var r = 2;
        foreach (var line in info) ws.Cell(r++, 1).Value = Text(line);

        var headerRow = r + 1;
        for (var c = 0; c < headers.Length; c++) ws.Cell(headerRow, c + 1).Value = headers[c];
        var head = ws.Range(headerRow, 1, headerRow, headers.Length);
        head.Style.Font.Bold = true;
        head.Style.Font.FontColor = XLColor.White;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#B03060");

        var row = headerRow + 1;
        foreach (var values in rows)
        {
            for (var c = 0; c < values.Length; c++) SetCell(ws.Cell(row, c + 1), values[c]);
            row++;
        }

        var lastDataRow = row - 1;
        if (totalRow != null)
        {
            for (var c = 0; c < totalRow.Length; c++) SetCell(ws.Cell(row, c + 1), totalRow[c]);
            var total = ws.Range(row, 1, row, headers.Length);
            total.Style.Font.Bold = true;
            total.Style.Fill.BackgroundColor = XLColor.FromHtml("#FDE7EC");
            lastDataRow = row;
        }

        if (lastDataRow > headerRow)
        {
            foreach (var c in numberCols ?? Array.Empty<int>())
                ws.Range(headerRow + 1, c, lastDataRow, c).Style.NumberFormat.Format = "#,##0";
            foreach (var c in dateTimeCols ?? Array.Empty<int>())
                ws.Range(headerRow + 1, c, lastDataRow, c).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        }
        ws.Columns().AdjustToContents();
    }

    public static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
