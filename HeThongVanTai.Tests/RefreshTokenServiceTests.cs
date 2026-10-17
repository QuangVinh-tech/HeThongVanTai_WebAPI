using Xunit;
using HeThongVanTai.Data;
using HeThongVanTai.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HeThongVanTai.Tests;

/// <summary>Kiểm tra cấp, xoay vòng, thu hồi refresh token (CSDL trong bộ nhớ).</summary>
public class RefreshTokenServiceTests : IDisposable
{
    private readonly VanTaiAuthDbContext _db;
    private readonly RefreshTokenService _svc;

    public RefreshTokenServiceTests()
    {
        _db = new VanTaiAuthDbContext(new DbContextOptionsBuilder<VanTaiAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshTokenDays"] = "7" })
            .Build();
        _svc = new RefreshTokenService(_db, config, NullLogger<RefreshTokenService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Cap_token_chi_luu_ban_bam()
    {
        var issued = await _svc.IssueAsync("user-1");

        var row = _db.RefreshTokens.Single();
        Assert.NotEqual(issued.Token, row.TokenHash);
        Assert.Equal(RefreshTokenService.Hash(issued.Token), row.TokenHash);
        Assert.True(issued.ExpiresAt > DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task Lam_moi_thu_hoi_token_cu_va_cap_token_moi()
    {
        var first = await _svc.IssueAsync("user-1");

        var rotated = await _svc.RotateAsync(first.Token);

        Assert.NotNull(rotated);
        Assert.Equal("user-1", rotated!.Value.UserId);
        Assert.NotEqual(first.Token, rotated.Value.Token.Token);
        var old = _db.RefreshTokens.Single(t => t.TokenHash == RefreshTokenService.Hash(first.Token));
        Assert.NotNull(old.RevokedAt);
        // Token mới dùng tiếp được
        Assert.NotNull(await _svc.RotateAsync(rotated.Value.Token.Token));
    }

    [Fact]
    public async Task Token_khong_ton_tai_hoac_rong_bi_tu_choi()
    {
        Assert.Null(await _svc.RotateAsync("khong-co-token-nay"));
        Assert.Null(await _svc.RotateAsync(""));
    }

    [Fact]
    public async Task Token_het_han_bi_tu_choi()
    {
        var issued = await _svc.IssueAsync("user-1");
        var row = _db.RefreshTokens.Single();
        row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        Assert.Null(await _svc.RotateAsync(issued.Token));
    }

    [Fact]
    public async Task Dung_lai_token_cu_sau_khi_xoay_vong_thu_hoi_moi_phien()
    {
        var first = await _svc.IssueAsync("user-1");
        var second = (await _svc.RotateAsync(first.Token))!.Value.Token;

        // Giả lập token cũ đã bị thu hồi từ lâu (quá khoảng ân hạn) rồi bị đưa ra lần nữa
        var old = _db.RefreshTokens.Single(t => t.TokenHash == RefreshTokenService.Hash(first.Token));
        old.RevokedAt = DateTime.UtcNow.AddMinutes(-5);
        await _db.SaveChangesAsync();

        Assert.Null(await _svc.RotateAsync(first.Token));
        // Token mới cũng bị thu hồi theo
        Assert.Null(await _svc.RotateAsync(second.Token));
    }

    [Fact]
    public async Task Hai_request_song_song_cung_token_cu_khong_bi_coi_la_lo()
    {
        var first = await _svc.IssueAsync("user-1");
        var second = (await _svc.RotateAsync(first.Token))!.Value.Token;

        // Dùng lại ngay trong khoảng ân hạn: bị từ chối nhưng không thu hồi token mới
        Assert.Null(await _svc.RotateAsync(first.Token));
        Assert.NotNull(await _svc.RotateAsync(second.Token));
    }

    [Fact]
    public async Task Thu_hoi_het_nhung_giu_lai_phien_dang_dung()
    {
        var a = await _svc.IssueAsync("user-1");
        var b = await _svc.IssueAsync("user-1");
        var other = await _svc.IssueAsync("user-2");

        var count = await _svc.RevokeAllAsync("user-1", exceptToken: a.Token);

        Assert.Equal(1, count);
        Assert.Null(await _svc.RotateAsync(b.Token));
        Assert.NotNull(await _svc.RotateAsync(a.Token));
        Assert.NotNull(await _svc.RotateAsync(other.Token));   // người khác không bị ảnh hưởng
    }

    [Fact]
    public async Task Dang_xuat_thu_hoi_token()
    {
        var issued = await _svc.IssueAsync("user-1");

        await _svc.RevokeAsync(issued.Token);

        Assert.Null(await _svc.RotateAsync(issued.Token));
    }
}
