using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

/// <summary>
/// Chạy lúc khởi động khi triển khai (Docker, máy chủ):
/// 1) Database:AutoMigrate = true thì áp migration cho cả hai CSDL (thử lại nếu SQL Server chưa sẵn sàng);
/// 2) có Seed:AdminEmail và Seed:AdminPassword thì tạo tài khoản Admin đầu tiên nếu chưa có.
/// Không bao giờ ghi mật khẩu vào log.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        if (config.GetValue<bool>("Database:AutoMigrate"))
            await MigrateAsync(services, logger);

        await SeedAdminAsync(services, config, logger);
    }

    private static async Task MigrateAsync(IServiceProvider services, ILogger logger)
    {
        const int attempts = 10;
        for (var i = 1; ; i++)
        {
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<VanTaiAuthDbContext>().Database.MigrateAsync();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
                logger.LogInformation("Đã áp dụng migration cho cả hai CSDL");
                return;
            }
            catch (Exception ex) when (i < attempts)
            {
                logger.LogWarning(ex, "Chưa kết nối hoặc áp migration được (lần {Attempt}/{Max}), thử lại sau 5 giây", i, attempts);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var email = config["Seed:AdminEmail"]?.Trim();
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return;

        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var existing = await users.FindByEmailAsync(email);
        if (existing != null)
        {
            // Tài khoản đã có: không đổi mật khẩu, chỉ bảo đảm có quyền Admin và đã xác thực email
            if (!existing.EmailConfirmed)
            {
                existing.EmailConfirmed = true;
                await users.UpdateAsync(existing);
            }
            if (!await users.IsInRoleAsync(existing, AppRoles.Admin))
                await users.AddToRoleAsync(existing, AppRoles.Admin);
            logger.LogInformation("Tài khoản Admin {Email} đã tồn tại, giữ nguyên mật khẩu", email);
            return;
        }

        var admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            logger.LogError("Không tạo được Admin {Email}: {Errors}", email, string.Join("; ", created.Errors.Select(e => e.Code)));
            return;
        }
        await users.AddToRoleAsync(admin, AppRoles.Admin);
        logger.LogInformation("Đã tạo tài khoản Admin đầu tiên {Email}. Hãy đăng nhập và đổi mật khẩu ngay", email);
    }
}
