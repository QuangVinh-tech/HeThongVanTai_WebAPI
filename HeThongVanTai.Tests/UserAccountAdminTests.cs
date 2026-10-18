using System.Security.Claims;
using HeThongVanTai.Controllers;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>
/// Việc 35 - Test khóa tài khoản và đổi vai trò (Admin quản lý người dùng) với Identity thật
/// trên CSDL tài khoản trong bộ nhớ; hồ sơ tài xế nằm ở CSDL nghiệp vụ (SQLite trong bộ nhớ của lớp nền).
/// </summary>
public class UserAccountAdminTests : BookingTestBase
{
    private readonly ServiceProvider _sp;
    private readonly VanTaiAuthDbContext _authDb;
    private readonly UserManager<IdentityUser> _users;
    private readonly RefreshTokenService _refresh;

    public UserAccountAdminTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<VanTaiAuthDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddIdentityCore<IdentityUser>(o => o.Lockout.AllowedForNewUsers = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<VanTaiAuthDbContext>();
        _sp = services.BuildServiceProvider();

        _authDb = _sp.GetRequiredService<VanTaiAuthDbContext>();
        _users = _sp.GetRequiredService<UserManager<IdentityUser>>();

        var roleMgr = _sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Operator, AppRoles.Seller, AppRoles.Accountant, AppRoles.Driver, AppRoles.Customer })
            roleMgr.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshTokenDays"] = "7" })
            .Build();
        _refresh = new RefreshTokenService(_authDb, config, NullLogger<RefreshTokenService>.Instance);
    }

    private async Task<IdentityUser> CreateUser(string email, params string[] roles)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        var created = await _users.CreateAsync(user, "Passw0rd!x");
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        if (roles.Length > 0) Assert.True((await _users.AddToRolesAsync(user, roles)).Succeeded);
        return user;
    }

    // Controller chạy dưới danh nghĩa một Admin đang đăng nhập
    private UsersController As(IdentityUser actor)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actor.Id),
            new Claim(ClaimTypes.Email, actor.Email ?? ""),
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        };
        return new UsersController(_users, Db, _refresh, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    private static UsersController.LockUserDto Lock(bool locked) => new() { Locked = locked };
    private static UsersController.UpdateRolesDto RolesDto(int? driverId, params string[] roles) =>
        new() { Roles = roles, DriverId = driverId };

    private async Task<IList<string>> RolesOf(IdentityUser u) =>
        await _users.GetRolesAsync((await _users.FindByIdAsync(u.Id))!);

    // ---------------- Khóa và mở khóa tài khoản ----------------

    [Fact]
    public async Task Khoa_tai_khoan_khoa_han_va_thu_hoi_refresh_token()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var customer = await CreateUser("khach@t.com", AppRoles.Customer);
        await _refresh.IssueAsync(customer.Id);

        var result = await As(admin).SetLock(customer.Id, Lock(true));

        Assert.IsType<OkObjectResult>(result);
        var fresh = (await _users.FindByIdAsync(customer.Id))!;
        Assert.True(await _users.IsLockedOutAsync(fresh));
        Assert.Equal(DateTimeOffset.MaxValue, fresh.LockoutEnd!.Value);
        Assert.True(await _authDb.RefreshTokens.Where(t => t.UserId == customer.Id).AllAsync(t => t.RevokedAt != null));
    }

    [Fact]
    public async Task Mo_khoa_tai_khoan_dang_nhap_lai_duoc_va_xoa_so_lan_sai()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var customer = await CreateUser("khach@t.com", AppRoles.Customer);
        await _users.AccessFailedAsync(customer);
        await _users.AccessFailedAsync(customer);
        await As(admin).SetLock(customer.Id, Lock(true));

        var result = await As(admin).SetLock(customer.Id, Lock(false));

        Assert.IsType<OkObjectResult>(result);
        var fresh = (await _users.FindByIdAsync(customer.Id))!;
        Assert.False(await _users.IsLockedOutAsync(fresh));
        Assert.Equal(0, await _users.GetAccessFailedCountAsync(fresh));
    }

    [Fact]
    public async Task Admin_khong_tu_khoa_chinh_minh()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        await CreateUser("admin2@t.com", AppRoles.Admin);

        var result = await As(admin).SetLock(admin.Id, Lock(true));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(await _users.IsLockedOutAsync((await _users.FindByIdAsync(admin.Id))!));
    }

    [Fact]
    public async Task Khong_khoa_quan_tri_vien_cuoi_cung()
    {
        var admin1 = await CreateUser("admin1@t.com", AppRoles.Admin);
        var admin2 = await CreateUser("admin2@t.com", AppRoles.Admin);
        var other = await CreateUser("admin3@t.com", AppRoles.Admin);

        // Khóa admin2 được vì còn admin1 chưa khóa
        Assert.IsType<OkObjectResult>(await As(other).SetLock(admin2.Id, Lock(true)));
        Assert.IsType<OkObjectResult>(await As(other).SetLock(admin1.Id, Lock(true)));

        // Còn mỗi "other" là admin đang hoạt động: khóa tiếp phải bị từ chối khi người gọi khác
        var caller = await CreateUser("seller-as-caller@t.com", AppRoles.Seller);
        var result = await As(caller).SetLock(other.Id, Lock(true));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(await _users.IsLockedOutAsync((await _users.FindByIdAsync(other.Id))!));
    }

    [Fact]
    public async Task Khoa_nguoi_dung_khong_ton_tai_bao_404()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        Assert.IsType<NotFoundObjectResult>(await As(admin).SetLock("khong-co", Lock(true)));
    }

    // ---------------- Đổi vai trò ----------------

    [Fact]
    public async Task Doi_vai_tro_thay_vai_trong_CSDL_nen_co_hieu_luc_o_lan_dang_nhap_sau()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var user = await CreateUser("nv@t.com", AppRoles.Customer);

        var result = await As(admin).UpdateRoles(user.Id, RolesDto(null, AppRoles.Seller));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(new[] { AppRoles.Seller }, (await RolesOf(user)).ToArray());
    }

    [Fact]
    public async Task Doi_vai_tro_cho_nhieu_vai_tro_cung_luc()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var user = await CreateUser("nv@t.com", AppRoles.Customer);

        Assert.IsType<OkObjectResult>(await As(admin).UpdateRoles(user.Id, RolesDto(null, AppRoles.Seller, AppRoles.Accountant)));

        Assert.Equal(new[] { AppRoles.Accountant, AppRoles.Seller }, (await RolesOf(user)).OrderBy(r => r).ToArray());
    }

    [Fact]
    public async Task Vai_tro_khong_hop_le_hoac_rong_bi_tu_choi_va_khong_doi_gi()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var user = await CreateUser("nv@t.com", AppRoles.Customer);

        Assert.IsType<BadRequestObjectResult>(await As(admin).UpdateRoles(user.Id, RolesDto(null, "SuperAdmin")));
        Assert.IsType<BadRequestObjectResult>(await As(admin).UpdateRoles(user.Id, RolesDto(null)));

        Assert.Equal(new[] { AppRoles.Customer }, (await RolesOf(user)).ToArray());
    }

    [Fact]
    public async Task Admin_khong_tu_bo_vai_tro_Admin_cua_minh()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        await CreateUser("admin2@t.com", AppRoles.Admin);

        var result = await As(admin).UpdateRoles(admin.Id, RolesDto(null, AppRoles.Seller));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(AppRoles.Admin, await RolesOf(admin));
    }

    [Fact]
    public async Task Khong_bo_vai_tro_Admin_cua_quan_tri_vien_cuoi_cung()
    {
        var onlyAdmin = await CreateUser("admin@t.com", AppRoles.Admin);
        var caller = await CreateUser("khac@t.com", AppRoles.Seller);

        var result = await As(caller).UpdateRoles(onlyAdmin.Id, RolesDto(null, AppRoles.Customer));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(AppRoles.Admin, await RolesOf(onlyAdmin));
    }

    // ---------------- Vai trò Tài xế gắn với hồ sơ tài xế ----------------

    [Fact]
    public async Task Gan_vai_tro_Tai_xe_phai_chon_ho_so_tai_xe()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var user = await CreateUser("tx@t.com", AppRoles.Customer);

        var result = await As(admin).UpdateRoles(user.Id, RolesDto(null, AppRoles.Driver));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(new[] { AppRoles.Customer }, (await RolesOf(user)).ToArray());
    }

    [Fact]
    public async Task Gan_vai_tro_Tai_xe_lien_ket_ho_so_va_go_lien_ket_khi_doi_vai_tro()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var user = await CreateUser("tx@t.com", AppRoles.Customer);
        var driver = new Driver { FullName = "Tài xế A" };
        Db.Drivers.Add(driver);
        await Db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await As(admin).UpdateRoles(user.Id, RolesDto(driver.Id, AppRoles.Driver)));
        await Db.Entry(driver).ReloadAsync();
        Assert.Equal(user.Id, driver.UserId);
        Assert.Equal(new[] { AppRoles.Driver }, (await RolesOf(user)).ToArray());

        Assert.IsType<OkObjectResult>(await As(admin).UpdateRoles(user.Id, RolesDto(null, AppRoles.Seller)));
        await Db.Entry(driver).ReloadAsync();
        Assert.Null(driver.UserId);
        Assert.Equal(new[] { AppRoles.Seller }, (await RolesOf(user)).ToArray());
    }

    [Fact]
    public async Task Ho_so_tai_xe_da_lien_ket_tai_khoan_khac_thi_bi_tu_choi()
    {
        var admin = await CreateUser("admin@t.com", AppRoles.Admin);
        var first = await CreateUser("tx1@t.com", AppRoles.Customer);
        var second = await CreateUser("tx2@t.com", AppRoles.Customer);
        var driver = new Driver { FullName = "Tài xế A" };
        Db.Drivers.Add(driver);
        await Db.SaveChangesAsync();
        await As(admin).UpdateRoles(first.Id, RolesDto(driver.Id, AppRoles.Driver));

        var result = await As(admin).UpdateRoles(second.Id, RolesDto(driver.Id, AppRoles.Driver));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain(AppRoles.Driver, await RolesOf(second));
    }
}
