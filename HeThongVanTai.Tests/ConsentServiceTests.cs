using HeThongVanTai.Data;
using HeThongVanTai.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>Việc 36 - ghi nhận ngày đồng ý Điều khoản và Chính sách bảo mật (CSDL tài khoản trong bộ nhớ).</summary>
public class ConsentServiceTests : IDisposable
{
    private readonly VanTaiAuthDbContext _db;
    private readonly ConsentService _svc;

    public ConsentServiceTests()
    {
        _db = new VanTaiAuthDbContext(new DbContextOptionsBuilder<VanTaiAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _svc = new ConsentService(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Ghi_nhan_luu_nguoi_dung_phien_ban_va_thoi_diem()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _svc.RecordAsync("user-1", "1.0");

        var row = Assert.Single(_db.UserConsents);
        Assert.Equal("user-1", row.UserId);
        Assert.Equal("1.0", row.TermsVersion);
        Assert.InRange(row.AcceptedAt, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Cung_phien_ban_chi_ghi_mot_lan_va_giu_ngay_dau()
    {
        await _svc.RecordAsync("user-1", "1.0");
        var first = _db.UserConsents.Single().AcceptedAt;

        await _svc.RecordAsync("user-1", "1.0");

        var row = Assert.Single(_db.UserConsents);
        Assert.Equal(first, row.AcceptedAt);
    }

    [Fact]
    public async Task Phien_ban_moi_them_dong_moi_va_Latest_tra_ve_ban_moi_nhat()
    {
        await _svc.RecordAsync("user-1", "1.0");
        await Task.Delay(20);
        await _svc.RecordAsync("user-1", "2.0");

        Assert.Equal(2, _db.UserConsents.Count());
        var latest = await _svc.LatestAsync("user-1");
        Assert.Equal("2.0", latest!.TermsVersion);
    }

    [Fact]
    public async Task Moi_nguoi_dung_co_ban_ghi_rieng()
    {
        await _svc.RecordAsync("user-1", "1.0");
        await _svc.RecordAsync("user-2", "1.0");

        Assert.Equal(2, _db.UserConsents.Count());
        Assert.Null(await _svc.LatestAsync("user-3"));
    }

    [Theory]
    [InlineData(null, "1.0")]
    [InlineData("", "1.0")]
    [InlineData("   ", "1.0")]
    [InlineData(" 2.1 ", "2.1")]
    public void Phien_ban_trong_dung_mac_dinh_va_cat_khoang_trang(string? input, string expected)
    {
        Assert.Equal(expected, ConsentService.NormalizeVersion(input));
    }

    [Fact]
    public async Task Phien_ban_qua_dai_bi_cat_cho_vua_cot()
    {
        await _svc.RecordAsync("user-1", new string('9', 50));

        Assert.Equal(ConsentService.MaxVersionLength, _db.UserConsents.Single().TermsVersion.Length);
    }
}
