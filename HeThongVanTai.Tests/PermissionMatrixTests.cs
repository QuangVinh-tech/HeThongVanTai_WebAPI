using System.Reflection;
using HeThongVanTai.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>
/// Việc 35 - Test phân quyền theo sheet "Phân quyền".
/// Với mỗi API (action) và mỗi vai trò, tính kết quả mà [Authorize] / [AllowAnonymous] sẽ cho:
/// 200 (được vào), 401 (chưa đăng nhập), 403 (đã đăng nhập nhưng sai vai trò), rồi so với bảng kỳ vọng.
/// Thêm API mới mà quên thêm dòng vào bảng kỳ vọng thì test báo lỗi (đúng quy tắc "thêm vào sheet trước").
/// Lưu ý: test kiểm tra khai báo phân quyền trên từng API; việc "của mình" (chỉ dữ liệu của chính mình)
/// do test của từng module kiểm tra.
/// </summary>
public class PermissionMatrixTests
{
    private static readonly string[] Roles = { "Admin", "Operator", "Seller", "Accountant", "Driver", "Customer" };

    // "*" = công khai (kể cả chưa đăng nhập) · "auth" = mọi tài khoản đã đăng nhập · còn lại = danh sách vai trò
    private static readonly Dictionary<string, string> Expected = new()
    {
        // Tài khoản
        ["AuthController.Register"] = "*",
        ["AuthController.VerifyEmail"] = "*",
        ["AuthController.ResendOtp"] = "*",
        ["AuthController.ForgotPassword"] = "*",
        ["AuthController.ResetPassword"] = "*",
        ["AuthController.Login"] = "*",
        ["AuthController.Refresh"] = "*",
        ["AuthController.Logout"] = "*",
        ["AuthController.Me"] = "auth",
        ["AuthController.ChangePassword"] = "auth",

        // Người dùng, cài đặt, nhật ký: chỉ Admin
        ["UsersController.GetRoles"] = "Admin",
        ["UsersController.GetDriverProfiles"] = "Admin",
        ["UsersController.List"] = "Admin",
        ["UsersController.GetById"] = "Admin",
        ["UsersController.UpdateRoles"] = "Admin",
        ["UsersController.SetLock"] = "Admin",
        ["SettingsController.GetPublic"] = "*",
        ["SettingsController.Get"] = "Admin",
        ["SettingsController.Save"] = "Admin",
        ["AuditLogsController.GetAll"] = "Admin",
        ["PromotionsController.GetAll"] = "Admin",
        ["PromotionsController.Create"] = "Admin",
        ["PromotionsController.Update"] = "Admin",
        ["PromotionsController.SetActive"] = "Admin",
        ["PromotionsController.Delete"] = "Admin",
        ["PromotionsController.ValidateCode"] = "Admin,Seller,Customer",

        // Danh mục và đội xe: Admin, Operator (xem bến xe, tuyến công khai)
        ["DanhMucController.GetStations"] = "*",
        ["DanhMucController.GetStationById"] = "*",
        ["DanhMucController.GetBusRoutes"] = "*",
        ["DanhMucController.GetBusRouteById"] = "*",
        ["DanhMucController.CreateStation"] = "Admin,Operator",
        ["DanhMucController.UpdateStation"] = "Admin,Operator",
        ["DanhMucController.DeleteStation"] = "Admin,Operator",
        ["DanhMucController.CreateBusRoute"] = "Admin,Operator",
        ["DanhMucController.UpdateBusRoute"] = "Admin,Operator",
        ["DanhMucController.DeleteBusRoute"] = "Admin,Operator",
        ["VehicleTypesController.GetAll"] = "Admin,Operator",
        ["VehicleTypesController.GetById"] = "Admin,Operator",
        ["VehicleTypesController.Create"] = "Admin,Operator",
        ["VehicleTypesController.UpdateSeatTemplates"] = "Admin,Operator",
        ["VehicleTypesController.Delete"] = "Admin,Operator",
        ["VehiclesController.GetAll"] = "Admin,Operator",
        ["VehiclesController.GetById"] = "Admin,Operator",
        ["VehiclesController.Create"] = "Admin,Operator",
        ["VehiclesController.Update"] = "Admin,Operator",
        ["VehiclesController.Delete"] = "Admin,Operator",
        ["VehiclesController.UpdateImage"] = "Admin,Operator",
        ["VehiclesController.GetVehicleAlerts"] = "Admin,Operator",
        ["DriversController.GetAll"] = "Admin,Operator",
        ["DriversController.GetById"] = "Admin,Operator",
        ["DriversController.Create"] = "Admin,Operator",
        ["DriversController.Update"] = "Admin,Operator",
        ["DriversController.Delete"] = "Admin,Operator",
        ["DriversController.UpdateAvatar"] = "Admin,Operator",
        ["ImagesController.GetAll"] = "Admin,Operator",
        ["ImagesController.Upload"] = "Admin,Operator",

        // Chuyến xe
        ["TripsController.GetTrips"] = "*",
        ["TripsController.GetTripSeats"] = "*",
        ["TripsController.CreateTrip"] = "Admin,Operator",
        ["TripsController.ChangeVehicle"] = "Admin,Operator",
        ["TripsController.PreviewSchedule"] = "Admin,Operator",
        ["TripsController.GenerateFixedSchedule"] = "Admin,Operator",
        ["TripsController.GetMine"] = "Driver",
        ["TripsController.GetMineDetail"] = "Driver",
        ["TripsController.GetPassengers"] = "Admin,Operator,Seller,Driver",
        ["TripsController.UpdateTripStatus"] = "Admin,Operator,Driver",

        // Đặt vé, thanh toán, vé
        ["BookingsController.Search"] = "*",
        ["BookingsController.GetByCode"] = "*",
        ["BookingsController.Lookup"] = "*",
        ["BookingsController.Create"] = "Admin,Seller,Customer",
        ["BookingsController.GetById"] = "Admin,Seller,Customer,Accountant",
        ["BookingsController.Mine"] = "Admin,Seller,Customer",
        ["BookingsController.MineDetail"] = "Admin,Seller,Customer",
        ["BookingsController.RefundPreview"] = "Admin,Seller,Customer",
        ["BookingsController.Cancel"] = "Admin,Seller,Customer",
        ["BookingsController.List"] = "Admin,Seller,Accountant",
        ["PaymentsController.Create"] = "Admin,Seller,Customer",
        ["PaymentsController.Confirm"] = "Admin,Seller,Accountant",
        ["PaymentsController.ByBooking"] = "Admin,Seller,Customer,Accountant",
        ["PaymentsController.SePayWebhook"] = "*",   // webhook của SePay, xác thực bằng khóa bí mật riêng
        ["TicketsController.Lookup"] = "*",
        ["TicketsController.Qr"] = "*",
        ["TicketsController.CheckIn"] = "Admin,Operator,Seller,Driver",
        ["TicketsController.Change"] = "Admin,Seller",   // đổi ngay: chỉ nhân viên; khách gửi yêu cầu bên dưới
        ["SeatChangeRequestsController.Create"] = "Customer",
        ["SeatChangeRequestsController.Mine"] = "Customer",
        ["SeatChangeRequestsController.Cancel"] = "Customer",
        ["SeatChangeRequestsController.List"] = "Admin",
        ["SeatChangeRequestsController.Approve"] = "Admin",
        ["SeatChangeRequestsController.Reject"] = "Admin",
        ["ReviewsController.ByTrip"] = "*",
        ["ReviewsController.Create"] = "Customer",
        ["ReviewsController.Mine"] = "Customer",

        // Khách hàng
        ["CustomersController.GetAll"] = "Admin,Seller",
        ["CustomersController.GetById"] = "Admin,Seller",
        ["CustomersController.Create"] = "Admin,Seller",
        ["CustomersController.Update"] = "Admin,Seller",
        ["CustomersController.Delete"] = "Admin,Seller",
        ["CustomersController.History"] = "Admin,Seller",

        // Vận chuyển hàng
        ["ShipmentsController.Quote"] = "*",
        ["ShipmentsController.Track"] = "*",
        ["ShipmentsController.Create"] = "Admin,Seller,Customer",
        ["ShipmentsController.Mine"] = "auth",
        ["ShipmentsController.Cancel"] = "auth",
        ["ShipmentsController.List"] = "Admin,Operator,Seller,Accountant",
        ["ShipmentsController.GetById"] = "Admin,Operator,Seller,Accountant",
        ["ShipmentsController.AvailableTrips"] = "Admin,Operator",
        ["ShipmentsController.ChangeStatus"] = "Admin,Operator",
        ["ShipmentsController.AssignTrip"] = "Admin,Operator",
        ["ShipmentsController.CollectFee"] = "Admin,Seller,Accountant",

        // Báo cáo và tổng quan
        ["DashboardController.GetSummary"] = "Admin,Operator",
        ["DashboardController.GetAlerts"] = "Admin,Operator",
        ["ReportsController.GetRevenueByRoute"] = "Admin,Accountant",
        ["ReportsController.RevenueBy"] = "Admin,Accountant",
        ["ReportsController.Debt"] = "Admin,Accountant",
        ["ReportsController.Reconciliation"] = "Admin,Accountant",
    };

    private sealed record Access(bool Anonymous, List<HashSet<string>?> Requirements);

    private static IEnumerable<(Type Controller, MethodInfo Action)> AllActions() =>
        typeof(BookingsController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(true).Any())
                .Select(m => (t, m)));

    private static string Key(Type c, MethodInfo m) => $"{c.Name}.{m.Name}";

    private static Access Resolve(Type c, MethodInfo m)
    {
        var anonymous = m.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
                        || c.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
        var reqs = c.GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(m.GetCustomAttributes<AuthorizeAttribute>(true))
            .Select(a => string.IsNullOrWhiteSpace(a.Roles)
                ? null
                : a.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet())
            .ToList();
        return new Access(anonymous, reqs);
    }

    // Kết quả HTTP mà bộ lọc phân quyền sẽ trả về. role = null nghĩa là chưa đăng nhập.
    private static int ActualStatus(Access a, string? role)
    {
        if (a.Anonymous || a.Requirements.Count == 0) return 200;
        if (role == null) return 401;
        return a.Requirements.Any(r => r != null && !r.Contains(role)) ? 403 : 200;
    }

    private static int ExpectedStatus(string spec, string? role) => spec switch
    {
        "*" => 200,
        "auth" => role == null ? 401 : 200,
        _ => role == null ? 401 : spec.Split(',').Contains(role) ? 200 : 403
    };

    private static List<string> Actual200(string? role) =>
        AllActions().Where(x => ActualStatus(Resolve(x.Controller, x.Action), role) == 200)
            .Select(x => Key(x.Controller, x.Action)).OrderBy(k => k).ToList();

    private static readonly string[] PublicApis =
    {
        "AuthController.Register", "AuthController.VerifyEmail", "AuthController.ResendOtp", "AuthController.ForgotPassword",
        "AuthController.ResetPassword", "AuthController.Login", "AuthController.Refresh", "AuthController.Logout",
        "BookingsController.Search", "BookingsController.GetByCode", "BookingsController.Lookup",
        "DanhMucController.GetStations", "DanhMucController.GetStationById",
        "DanhMucController.GetBusRoutes", "DanhMucController.GetBusRouteById",
        "PaymentsController.SePayWebhook", "ReviewsController.ByTrip", "SettingsController.GetPublic",
        "ShipmentsController.Quote", "ShipmentsController.Track",
        "TicketsController.Lookup", "TicketsController.Qr",
        "TripsController.GetTripSeats", "TripsController.GetTrips"
    };

    // ---------- Bảng kỳ vọng luôn khớp danh sách API thật ----------

    [Fact]
    public void Moi_API_deu_co_dong_trong_bang_phan_quyen()
    {
        var missing = AllActions().Select(x => Key(x.Controller, x.Action)).Where(k => !Expected.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0,
            "API chưa có trong bảng phân quyền (thêm vào sheet Phân quyền và vào bảng Expected): " + string.Join(", ", missing));
    }

    [Fact]
    public void Bang_phan_quyen_khong_con_API_da_xoa()
    {
        var real = AllActions().Select(x => Key(x.Controller, x.Action)).ToHashSet();
        var stale = Expected.Keys.Where(k => !real.Contains(k)).ToList();
        Assert.True(stale.Count == 0, "Bảng phân quyền còn dòng của API không tồn tại: " + string.Join(", ", stale));
    }

    // ---------- Mỗi vai trò gọi từng API: 200, 401, 403 ----------

    [Theory]
    [InlineData(null)]          // khách vãng lai (chưa đăng nhập)
    [InlineData("Admin")]
    [InlineData("Operator")]
    [InlineData("Seller")]
    [InlineData("Accountant")]
    [InlineData("Driver")]
    [InlineData("Customer")]
    public void Moi_vai_tro_goi_tung_API_dung_ma_trang_thai(string? role)
    {
        var wrong = new List<string>();
        foreach (var (c, m) in AllActions())
        {
            var key = Key(c, m);
            if (!Expected.TryGetValue(key, out var spec)) continue;   // đã có test riêng báo thiếu dòng
            var expected = ExpectedStatus(spec, role);
            var actual = ActualStatus(Resolve(c, m), role);
            if (expected != actual) wrong.Add($"{key}: cần {expected}, code cho {actual}");
        }
        Assert.True(wrong.Count == 0, $"Vai trò '{role ?? "khách vãng lai"}' sai quyền ở:\n" + string.Join("\n", wrong));
    }

    // ---------- Yêu cầu nghiệm thu của việc 35 (viết độc lập với bảng Expected) ----------

    [Fact]
    public void Khach_vang_lai_chi_vao_duoc_API_cong_khai_da_liet_ke()
    {
        var anonymous = Actual200(null);
        var unexpectedlyOpen = anonymous.Except(PublicApis).ToList();
        var closed = PublicApis.Except(anonymous).ToList();
        Assert.True(unexpectedlyOpen.Count == 0, "API đang mở cho khách vãng lai ngoài sheet: " + string.Join(", ", unexpectedlyOpen));
        Assert.True(closed.Count == 0, "API công khai bị đóng: " + string.Join(", ", closed));
    }

    private static readonly string[] AdminOnlyControllers =
    {
        nameof(UsersController), nameof(AuditLogsController), nameof(SettingsController),
        nameof(PromotionsController), nameof(VehiclesController), nameof(VehicleTypesController),
        nameof(DriversController), nameof(DashboardController), nameof(ReportsController),
        nameof(CustomersController), nameof(ImagesController)
    };

    private static IEnumerable<(Type Controller, MethodInfo Action)> ManagementActions() =>
        AllActions().Where(x => AdminOnlyControllers.Contains(x.Controller.Name)
            && Key(x.Controller, x.Action) != "SettingsController.GetPublic"
            && Key(x.Controller, x.Action) != "PromotionsController.ValidateCode");

    [Theory]
    [InlineData("Customer")]
    [InlineData("Driver")]
    public void Customer_va_Driver_khong_vao_duoc_API_quan_tri(string role)
    {
        var leaked = ManagementActions()
            .Where(x => ActualStatus(Resolve(x.Controller, x.Action), role) != 403)
            .Select(x => Key(x.Controller, x.Action)).ToList();
        Assert.True(leaked.Count == 0, $"{role} vào được API quản trị: " + string.Join(", ", leaked));
    }

    [Fact]
    public void Khach_vang_lai_bi_401_o_moi_API_quan_tri()
    {
        var leaked = ManagementActions()
            .Where(x => ActualStatus(Resolve(x.Controller, x.Action), null) != 401)
            .Select(x => Key(x.Controller, x.Action)).ToList();
        Assert.True(leaked.Count == 0, "Chưa đăng nhập vẫn vào được: " + string.Join(", ", leaked));
    }

    [Theory]
    [InlineData("Seller")]
    [InlineData("Operator")]
    [InlineData("Driver")]
    [InlineData("Customer")]
    public void Chi_Admin_va_Accountant_xem_duoc_bao_cao(string role)
    {
        var reports = AllActions().Where(x => x.Controller == typeof(ReportsController)).ToList();
        Assert.NotEmpty(reports);
        Assert.All(reports, x => Assert.Equal(403, ActualStatus(Resolve(x.Controller, x.Action), role)));
        Assert.All(reports, x => Assert.Equal(200, ActualStatus(Resolve(x.Controller, x.Action), "Accountant")));
        Assert.All(reports, x => Assert.Equal(200, ActualStatus(Resolve(x.Controller, x.Action), "Admin")));
    }

    [Fact]
    public void Operator_khong_xac_nhan_chuyen_khoan_va_khong_dat_ve()
    {
        var confirm = AllActions().Single(x => Key(x.Controller, x.Action) == "PaymentsController.Confirm");
        Assert.Equal(403, ActualStatus(Resolve(confirm.Controller, confirm.Action), "Operator"));
        var create = AllActions().Single(x => Key(x.Controller, x.Action) == "BookingsController.Create");
        Assert.Equal(403, ActualStatus(Resolve(create.Controller, create.Action), "Operator"));
        Assert.Equal(403, ActualStatus(Resolve(create.Controller, create.Action), "Driver"));
        Assert.Equal(403, ActualStatus(Resolve(create.Controller, create.Action), "Accountant"));
    }

    [Fact]
    public void Tai_xe_chi_goi_duoc_API_chuyen_cua_minh_va_API_chung()
    {
        var allowed = Actual200("Driver").Except(PublicApis).OrderBy(k => k).ToList();
        var expected = new[]
        {
            "AuthController.ChangePassword", "AuthController.Me",
            "ShipmentsController.Cancel", "ShipmentsController.Mine",
            "TicketsController.CheckIn",
            "TripsController.GetMine", "TripsController.GetMineDetail",
            "TripsController.GetPassengers", "TripsController.UpdateTripStatus"
        }.OrderBy(k => k).ToList();
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void Khach_hang_chi_goi_duoc_API_cua_khach()
    {
        var allowed = Actual200("Customer").Except(PublicApis).OrderBy(k => k).ToList();
        var expected = new[]
        {
            "AuthController.ChangePassword", "AuthController.Me",
            "BookingsController.Cancel", "BookingsController.Create", "BookingsController.GetById",
            "BookingsController.Mine", "BookingsController.MineDetail", "BookingsController.RefundPreview",
            "PaymentsController.ByBooking", "PaymentsController.Create",
            "PromotionsController.ValidateCode",
            "ReviewsController.Create", "ReviewsController.Mine",
            "SeatChangeRequestsController.Cancel", "SeatChangeRequestsController.Create", "SeatChangeRequestsController.Mine",
            "ShipmentsController.Cancel", "ShipmentsController.Create", "ShipmentsController.Mine"
        }.OrderBy(k => k).ToList();
        Assert.Equal(expected, allowed);
    }
}
