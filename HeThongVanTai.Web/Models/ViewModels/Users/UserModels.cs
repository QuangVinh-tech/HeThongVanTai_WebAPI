using HeThongVanTai.Web.Infrastructure;

namespace HeThongVanTai.Web.Models.ViewModels.Users;

public class UserItem
{
    public string Id { get; set; } = "";
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsLocked { get; set; }

    // Hồ sơ tài xế được liên kết (chỉ có khi tài khoản có vai trò Driver)
    public int? DriverId { get; set; }
    public string? DriverName { get; set; }
}

// Hồ sơ tài xế để chọn khi gán vai trò Driver
public class DriverProfileItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? LicenseNo { get; set; }
    public string? LinkedUserId { get; set; }
}

public class UserPage
{
    public List<UserItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(PageSize, 1)));
}

public class UserRolesForm
{
    public string Id { get; set; } = "";
    public string? Email { get; set; }
    public bool IsSelf { get; set; }

    // Các vai trò được chọn (checkbox)
    public List<string> SelectedRoles { get; set; } = new();

    // Hồ sơ tài xế liên kết khi chọn vai trò Driver
    public int? DriverId { get; set; }
    public List<DriverProfileItem> DriverProfiles { get; set; } = new();
}

// Tên vai trò hiển thị cho người dùng (tên trong hệ thống giữ nguyên tiếng Anh)
public static class RoleLabels
{
    public static readonly string[] All =
        { AppRoles.Admin, AppRoles.Operator, AppRoles.Seller, AppRoles.Accountant, AppRoles.Driver, AppRoles.Customer };

    public static string Of(string role) => role switch
    {
        AppRoles.Admin => "Quản trị viên",
        AppRoles.Operator => "Điều hành",
        AppRoles.Seller => "Bán vé",
        AppRoles.Accountant => "Kế toán",
        AppRoles.Driver => "Tài xế",
        AppRoles.Customer => "Khách hàng",
        _ => role
    };
}
