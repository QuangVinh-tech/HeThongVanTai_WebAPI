namespace HeThongVanTai.Web.Models.ViewModels.NhatKy;

public class AuditLogItem
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
}

public class AuditFilter
{
    public string? Keyword { get; set; }
    public string? EntityType { get; set; }
    public string? Operation { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
}

public class AuditListModel
{
    public AuditFilter Filter { get; set; } = new();
    public List<AuditLogItem> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public string? LoadError { get; set; }
}

public static class AuditOptions
{
    public static readonly (string Value, string Label)[] EntityTypes =
    {
        ("Vehicle", "Xe"), ("Driver", "Tài xế"), ("Trip", "Chuyến xe"), ("BusRoute", "Tuyến đường"),
        ("Promotion", "Khuyến mãi"), ("UserRole", "Tài khoản và vai trò"), ("Booking", "Đặt vé")
    };

    public static readonly (string Value, string Label)[] Actions =
    {
        ("Create", "Thêm"), ("Update", "Sửa"), ("Delete", "Xóa"), ("Status", "Đổi trạng thái"),
        ("GenerateSchedule", "Tạo lịch chạy"), ("ChangeRole", "Đổi vai trò"), ("Lock", "Khóa tài khoản"),
        ("Unlock", "Mở khóa"), ("Cancel", "Hủy vé"), ("Refund", "Hủy và hoàn tiền")
    };

    public static string EntityText(string v) => EntityTypes.FirstOrDefault(x => x.Value == v).Label ?? v;
    public static string ActionText(string v) => Actions.FirstOrDefault(x => x.Value == v).Label ?? v;

    public static string ActionCss(string v) => v switch
    {
        "Create" => "pill-ok",
        "Delete" or "Cancel" or "Refund" or "Lock" => "pill-bad",
        "Status" or "ChangeRole" or "Unlock" => "pill-warn",
        _ => "pill-info"
    };
}
