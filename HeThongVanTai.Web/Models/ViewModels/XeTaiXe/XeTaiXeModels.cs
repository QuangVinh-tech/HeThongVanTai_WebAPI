using System.ComponentModel.DataAnnotations;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HeThongVanTai.Web.Models.ViewModels.XeTaiXe;

public class PagedResult<T>
{
    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new();
}

public class ImageUploadResult
{
    public string FilePath { get; set; } = "";
}

public static class XeTaiXeOptions
{
    public static readonly (string Value, string Label)[] Statuses =
    {
        ("Active", "Hoạt động"),
        ("Maintenance", "Bảo dưỡng"),
        ("Inactive", "Ngưng hoạt động")
    };

    public static readonly string[] LicenseClasses = { "B1", "B2", "C", "D", "E", "FB2", "FC", "FD", "FE" };

    public static string StatusText(string? status) => status switch
    {
        "Active" => "Hoạt động",
        "Maintenance" => "Bảo dưỡng",
        "Inactive" => "Ngưng hoạt động",
        _ => status ?? ""
    };

    public static string StatusCss(string? status) => status switch
    {
        "Active" => "pill-ok",
        "Maintenance" => "pill-warn",
        _ => "pill-info"
    };

    public static (string Css, string Text)? ExpiryWarning(DateTime? date)
    {
        if (!date.HasValue) return null;
        var days = (date.Value.Date - DateTime.Today).Days;
        if (days < 0) return ("pill-bad", "Quá hạn " + (-days) + " ngày");
        if (days <= 30) return ("pill-warn", "Còn " + days + " ngày");
        return null;
    }
}

public class VehicleItem
{
    public int Id { get; set; }
    public string Plate { get; set; } = "";
    public int VehicleTypeId { get; set; }
    public string? VehicleTypeName { get; set; }
    public int SeatCount { get; set; }
    public DateTime? RegistrationExpiry { get; set; }
    public DateTime? InsuranceExpiry { get; set; }
    public string Status { get; set; } = "Active";
    public string? ImageUrl { get; set; }
}

public class VehicleFilter
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public int? VehicleTypeId { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
}

public class VehicleListModel
{
    public VehicleFilter Filter { get; set; } = new();
    public List<VehicleItem> Items { get; set; } = new();
    public List<VehicleTypeItem> VehicleTypes { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public string? LoadError { get; set; }
}

public class VehicleForm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập biển số xe")]
    [MaxLength(20, ErrorMessage = "Biển số tối đa 20 ký tự")]
    [Display(Name = "Biển số xe")]
    public string Plate { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại xe")]
    [Display(Name = "Loại xe")]
    public int VehicleTypeId { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Hạn đăng kiểm")]
    public DateTime? RegistrationExpiry { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Hạn bảo hiểm")]
    public DateTime? InsuranceExpiry { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Active";

    public string? ImageUrl { get; set; }

    [Display(Name = "Ảnh phương tiện")]
    public IFormFile? ImageFile { get; set; }

    [ValidateNever]
    public List<VehicleTypeItem> VehicleTypes { get; set; } = new();
}

public class DriverItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? LicenseNo { get; set; }
    public string? LicenseClass { get; set; }
    public DateTime? LicenseExpiry { get; set; }
    public string Status { get; set; } = "Active";
    public string? AvatarUrl { get; set; }
}

public class DriverFilter
{
    public string? Keyword { get; set; }
    public string? FilterOn { get; set; }
    public string? Status { get; set; }
    public string? LicenseClass { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
}

public class DriverListModel
{
    public DriverFilter Filter { get; set; } = new();
    public List<DriverItem> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public string? LoadError { get; set; }
}

public class DriverForm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";

    [RegularExpression(@"^\+?[0-9\s.\-]{8,15}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [MaxLength(30, ErrorMessage = "Số GPLX tối đa 30 ký tự")]
    [Display(Name = "Số GPLX")]
    public string? LicenseNo { get; set; }

    [MaxLength(10, ErrorMessage = "Hạng GPLX tối đa 10 ký tự")]
    [Display(Name = "Hạng GPLX")]
    public string? LicenseClass { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Hạn GPLX")]
    public DateTime? LicenseExpiry { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Active";

    public string? AvatarUrl { get; set; }

    [Display(Name = "Ảnh đại diện")]
    public IFormFile? AvatarFile { get; set; }
}