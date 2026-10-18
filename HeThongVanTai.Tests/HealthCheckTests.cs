using HeThongVanTai.Data;
using HeThongVanTai.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>Kiểm tra endpoint /health: báo Healthy khi CSDL kết nối được, Unhealthy khi không.</summary>
public class HealthCheckTests : BookingTestBase
{
    [Fact]
    public async Task CsdlKetNoiDuoc_BaoHealthy()
    {
        var result = await new DbHealthCheck<AppDbContext>(Db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CsdlKhongKetNoiDuoc_BaoUnhealthy_KhongLoChiTietLoi()
    {
        // Máy chủ không tồn tại, hết thời gian chờ sau 1 giây
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=Khong;User Id=sa;Password=x;Connect Timeout=1;TrustServerCertificate=True;")
            .Options;
        using var broken = new AppDbContext(options);

        var result = await new DbHealthCheck<AppDbContext>(broken).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("127.0.0.1", result.Description ?? "");   // thông báo không chứa địa chỉ máy chủ
    }
}
