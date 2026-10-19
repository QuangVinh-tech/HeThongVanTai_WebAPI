using System.Text.RegularExpressions;

namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

/// <summary>
/// Bố cục một tầng: số cột mỗi hàng, lối đi sau cột nào, và BackRow = số giường ở hàng cuối nằm ngang toàn thân xe (0 = không có).
/// </summary>
public sealed record LayoutPlan(int Columns, int[] AisleAfter, int BackRow = 0);

/// <summary>Một hàng ghế. FullWidth = hàng cuối nằm ngang, không có lối đi.</summary>
public sealed record SeatRow(SeatInfo?[] Cells, bool FullWidth);

public class BusSeatMapModel
{
    public List<SeatInfo> Seats { get; set; } = new();
    /// <summary>Tên loại xe (ví dụ "Limousine 24 chỗ"), dùng để chọn bố cục.</summary>
    public string? VehicleType { get; set; }
    /// <summary>true: bấm chọn được (trang chọn ghế, đổi ghế). false: chỉ xem (cửa sổ xem sơ đồ).</summary>
    public bool Interactive { get; set; } = true;
    /// <summary>Ghế hiện tại khi đổi ghế (hiện nét đứt, không chọn được).</summary>
    public string? CurrentSeat { get; set; }
}

/// <summary>
/// Xếp ghế của từng tầng thành hàng giống thân xe thật. Sơ đồ ghế trong CSDL chỉ có mã ghế và số tầng,
/// nên bố cục được chọn theo tên loại xe, số tầng và số ghế mỗi tầng:
///
///  2 tầng
///   - tên có "limousine": 2 dãy, 1 lối đi (limousine giường, phòng nằm; 24 chỗ = 2 tầng x 12 giường, 6 hàng)
///   - giường nằm thường, mỗi tầng 14 giường trở lên và (số giường - 5) chia hết cho 3 (20, 17...): 3 dãy, 2 lối đi,
///     hàng cuối 5 giường nằm ngang (xe 40 chỗ: 5 hàng x 3 + 5 mỗi tầng)
///   - giường nằm thường khác, từ 12 giường mỗi tầng: 3 dãy, 2 lối đi
///   - ít hơn 12 giường mỗi tầng: 2 dãy, 1 lối đi
///  1 tầng
///   - tối đa 8 ghế: 2 cột (1-1)
///   - limousine ghế ngồi hoặc 9 đến 24 ghế (xe 16 chỗ): 3 cột (1-2)
///   - trên 24 ghế (xe khách ghế ngồi): 4 cột (2-2)
/// </summary>
public static class SeatLayout
{
    private static readonly Regex CodeRx = new(@"^(\D*)(\d+)", RegexOptions.Compiled);

    public static bool IsLimousine(string? vehicleType) =>
        !string.IsNullOrWhiteSpace(vehicleType) && vehicleType.Contains("limo", StringComparison.OrdinalIgnoreCase);

    public static LayoutPlan Plan(int floors, int seatsOnFloor, string? vehicleType = null)
    {
        var limo = IsLimousine(vehicleType);

        if (floors >= 2)
        {
            if (limo) return new LayoutPlan(2, new[] { 1 });
            if (seatsOnFloor >= 14 && (seatsOnFloor - 5) % 3 == 0) return new LayoutPlan(3, new[] { 1, 2 }, 5);
            return seatsOnFloor >= 12 ? new LayoutPlan(3, new[] { 1, 2 }) : new LayoutPlan(2, new[] { 1 });
        }

        if (seatsOnFloor <= 8) return new LayoutPlan(2, new[] { 1 });
        if (limo || seatsOnFloor <= 24) return new LayoutPlan(3, new[] { 1 });
        return new LayoutPlan(4, new[] { 2 });
    }

    /// <summary>Chia ghế của một tầng thành các hàng; ô thiếu ở hàng cuối là null. Sắp theo mã tự nhiên (A2 trước A10).</summary>
    public static List<SeatRow> Rows(IEnumerable<SeatInfo> seats, LayoutPlan plan)
    {
        var ordered = seats
            .OrderBy(s => Prefix(s.SeatCode), StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => Number(s.SeatCode))
            .ThenBy(s => s.SeatCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<SeatRow>();
        var body = plan.BackRow > 0 && ordered.Count >= plan.BackRow ? ordered.Count - plan.BackRow : ordered.Count;

        for (var i = 0; i < body; i += plan.Columns)
        {
            var cells = new SeatInfo?[plan.Columns];
            for (var c = 0; c < plan.Columns && i + c < body; c++) cells[c] = ordered[i + c];
            rows.Add(new SeatRow(cells, false));
        }

        if (body < ordered.Count)
            rows.Add(new SeatRow(ordered.Skip(body).Select(s => (SeatInfo?)s).ToArray(), true));

        return rows;
    }

    private static string Prefix(string code)
    {
        var m = CodeRx.Match(code);
        return m.Success ? m.Groups[1].Value : code;
    }

    private static int Number(string code)
    {
        var m = CodeRx.Match(code);
        return m.Success && int.TryParse(m.Groups[2].Value, out var n) ? n : int.MaxValue;
    }
}
