using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HeThongVanTai.Tests;

public class SettingsServiceTests : BookingTestBase
{
    private static IConfiguration Cfg(string? hold = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(hold == null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["Booking:HoldMinutes"] = hold })
            .Build();

    private SettingsService Service(string? hold = null) => new(Db, Cfg(hold));

    private static SettingsDto Valid() => new()
    {
        HoldMinutes = 20,
        RefundTiers = new()
        {
            new() { HoursBefore = 48, RatePercent = 100 },
            new() { HoursBefore = 6, RatePercent = 40 }
        },
        CompanyName = "Công ty Thử",
        LogoUrl = "/images/logo.png",
        Hotline = "1900 1234",
        Email = "lienhe@example.com"
    };

    [Fact]
    public async Task ChuaLuu_DungMacDinh()
    {
        var s = await Service().GetAsync();
        Assert.Equal(15, s.HoldMinutes);
        Assert.Equal(3, s.RefundTiers.Count);
        Assert.Equal(24, s.RefundTiers[0].HoursBefore);
    }

    [Fact]
    public async Task ChuaLuu_GiuGheLayTuAppsettings()
    {
        var s = await Service("25").GetAsync();
        Assert.Equal(25, s.HoldMinutes);
    }

    [Fact]
    public async Task LuuXong_DocLaiDuocVaGhiNguoiSua()
    {
        var svc = Service();
        Assert.Null(await svc.SaveAsync(Valid(), "admin@test.com"));

        var s = await svc.GetAsync();
        Assert.Equal(20, s.HoldMinutes);
        Assert.Equal(2, s.RefundTiers.Count);
        Assert.Equal(48, s.RefundTiers[0].HoursBefore);
        Assert.Equal("Công ty Thử", s.CompanyName);
        Assert.Equal("1900 1234", s.Hotline);
        Assert.All(Db.SystemSettings, x => Assert.Equal("admin@test.com", x.UpdatedBy));
    }

    [Fact]
    public async Task LuuHaiLan_KhongTaoDong_Trung()
    {
        var svc = Service();
        await svc.SaveAsync(Valid(), "a");
        var dto = Valid();
        dto.HoldMinutes = 30;
        Assert.Null(await svc.SaveAsync(dto, "b"));
        Assert.Equal(6, Db.SystemSettings.Count());
        Assert.Equal(30, (await svc.GetAsync()).HoldMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void GiuGhe_NgoaiKhoang_BiTuChoi(int minutes)
    {
        var dto = Valid();
        dto.HoldMinutes = minutes;
        Assert.NotNull(SettingsService.Validate(dto));
    }

    [Fact]
    public void Moc_TrungGio_BiTuChoi()
    {
        var dto = Valid();
        dto.RefundTiers.Add(new() { HoursBefore = 6, RatePercent = 10 });
        Assert.NotNull(SettingsService.Validate(dto));
    }

    [Fact]
    public void Huy_SatGio_TiLeCaoHon_BiTuChoi()
    {
        var dto = Valid();
        dto.RefundTiers = new()
        {
            new() { HoursBefore = 24, RatePercent = 50 },
            new() { HoursBefore = 2, RatePercent = 80 }
        };
        Assert.NotNull(SettingsService.Validate(dto));
    }

    [Fact]
    public void SoMoc_NgoaiKhoang_BiTuChoi()
    {
        var dto = Valid();
        dto.RefundTiers = new();
        Assert.NotNull(SettingsService.Validate(dto));

        dto.RefundTiers = Enumerable.Range(0, 11).Select(i => new RefundTierDto { HoursBefore = i, RatePercent = 0 }).ToList();
        Assert.NotNull(SettingsService.Validate(dto));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.com/x.png")]
    public void Logo_SaiDinhDang_BiTuChoi(string logo)
    {
        var dto = Valid();
        dto.LogoUrl = logo;
        Assert.NotNull(SettingsService.Validate(dto));
    }

    [Fact]
    public void Email_VaHotline_Sai_BiTuChoi()
    {
        var dto = Valid();
        dto.Email = "khong-phai-email";
        Assert.NotNull(SettingsService.Validate(dto));

        dto = Valid();
        dto.Hotline = "abc";
        Assert.NotNull(SettingsService.Validate(dto));
    }

    [Fact]
    public void TenCongTy_Trong_BiTuChoi()
    {
        var dto = Valid();
        dto.CompanyName = "  ";
        Assert.NotNull(SettingsService.Validate(dto));
    }

    [Fact]
    public async Task SaiDuLieu_KhongLuuGiDuoc()
    {
        var dto = Valid();
        dto.HoldMinutes = 0;
        Assert.NotNull(await Service().SaveAsync(dto, "a"));
        Assert.Empty(Db.SystemSettings);
    }

    // ---------- Hoàn tiền theo mốc tùy chỉnh ----------
    private static readonly DateTime Now = new(2026, 10, 10, 8, 0, 0);

    [Theory]
    [InlineData(60, 100)]
    [InlineData(48, 100)]
    [InlineData(10, 40)]
    [InlineData(6, 40)]
    [InlineData(5, 0)]
    [InlineData(-1, 0)]
    public void HoanTien_TheoMocTuyChinh(int hours, int expectedPercent)
    {
        var rate = Rules.RefundRate(Now.AddHours(hours), Now, Valid().RefundTiers);
        Assert.Equal(expectedPercent, (int)(rate * 100));
    }

    [Fact]
    public void HoanTien_MocMacDinh_VanDung()
    {
        Assert.Equal(1m, Rules.RefundRate(Now.AddHours(30), Now));
        Assert.Equal(0.7m, Rules.RefundRate(Now.AddHours(13), Now, Rules.DefaultRefundTiers));
        Assert.Equal(0m, Rules.RefundRate(Now.AddHours(1), Now));
    }
}
