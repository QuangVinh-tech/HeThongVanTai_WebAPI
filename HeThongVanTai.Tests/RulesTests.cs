using HeThongVanTai.Models.Domain;
using HeThongVanTai.Services;

namespace HeThongVanTai.Tests;

public class RulesTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 8, 0, 0);

    private static Promotion Promo(string type = "Percent", decimal value = 10) => new()
    {
        Code = "TEST",
        DiscountType = type,
        Value = value,
        StartAt = Now.AddDays(-1),
        EndAt = Now.AddDays(1),
        IsActive = true
    };

    // ---------- Hoàn tiền theo số giờ còn lại ----------
    [Theory]
    [InlineData(48, 100)]
    [InlineData(24, 100)]
    [InlineData(18, 70)]
    [InlineData(12, 70)]
    [InlineData(5, 50)]
    [InlineData(2, 50)]
    [InlineData(1, 0)]
    public void HoanTien_TheoGioConLai(int hours, int expectedPercent)
    {
        var rate = Rules.RefundRate(Now.AddHours(hours), Now);
        Assert.Equal(expectedPercent, (int)(rate * 100));
    }

    // ---------- Mã giảm giá ----------
    [Fact]
    public void Giam10Phan_Tram_DungSoTien()
    {
        var d = Rules.Discount(Promo(), 200_000m, Now, out var err);
        Assert.Null(err);
        Assert.Equal(20_000m, d);
    }

    [Fact]
    public void GiamPhanTram_BiChanBoiMaxDiscount()
    {
        var p = Promo(value: 50);
        p.MaxDiscount = 30_000m;
        Assert.Equal(30_000m, Rules.Discount(p, 200_000m, Now, out _));
    }

    [Fact]
    public void GiamSoTien_KhongVuotQuaTongTien()
    {
        Assert.Equal(200_000m, Rules.Discount(Promo("Amount", 500_000m), 200_000m, Now, out _));
    }

    [Fact]
    public void MaHetHan_BaoLoi()
    {
        var p = Promo();
        p.EndAt = Now.AddMinutes(-1);
        Rules.Discount(p, 200_000m, Now, out var err);
        Assert.NotNull(err);
    }

    [Fact]
    public void ChuaDuDonToiThieu_BaoLoi()
    {
        var p = Promo();
        p.MinOrder = 300_000m;
        Rules.Discount(p, 200_000m, Now, out var err);
        Assert.NotNull(err);
    }

    [Fact]
    public void HetLuotDung_BaoLoi()
    {
        var p = Promo();
        p.UsageLimit = 5;
        p.UsedCount = 5;
        Rules.Discount(p, 200_000m, Now, out var err);
        Assert.NotNull(err);
    }

    [Fact]
    public void MaBiTat_BaoLoi()
    {
        var p = Promo();
        p.IsActive = false;
        Rules.Discount(p, 200_000m, Now, out var err);
        Assert.NotNull(err);
    }

    // ---------- Sinh mã vé ----------
    [Fact]
    public void MaVe_DungDinhDang_VaKhongTrung()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => Rules.NewTicketCode()).ToList();
        Assert.All(codes, c => Assert.Matches("^VT[A-Z2-9]{8}$", c));
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }
}