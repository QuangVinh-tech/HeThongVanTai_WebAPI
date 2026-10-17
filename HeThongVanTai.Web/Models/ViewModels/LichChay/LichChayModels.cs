using System.ComponentModel.DataAnnotations;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Models.ViewModels.XeTaiXe;

namespace HeThongVanTai.Web.Models.ViewModels.LichChay;

public class ScheduleForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tuyến đường")]
    [Display(Name = "Tuyến đường")]
    public int BusRouteId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn xe")]
    [Display(Name = "Xe")]
    public int VehicleId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tài xế")]
    [Display(Name = "Tài xế")]
    public int DriverId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Từ ngày")]
    public DateTime? FromDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Đến ngày")]
    public DateTime? ToDate { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giờ xuất bến")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Giờ xuất bến theo dạng HH:mm, ví dụ 08:30")]
    [Display(Name = "Giờ xuất bến")]
    public string DepartureTime { get; set; } = "08:00";

    // Bỏ trống = chạy mọi ngày trong khoảng
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    [Required(ErrorMessage = "Vui lòng nhập giá vé")]
    [Range(0, 100000000, ErrorMessage = "Giá vé từ 0 đến 100.000.000")]
    [Display(Name = "Giá vé (đ)")]
    public decimal? Price { get; set; }
}

public class PlanRow
{
    public DateTime DepartAt { get; set; }
    public DateTime ArriveAt { get; set; }
    public bool Ok { get; set; }
    public string? Problem { get; set; }
}

public class PlanResult
{
    public int TotalItems { get; set; }
    public int ConflictCount { get; set; }
    public List<PlanRow> Items { get; set; } = new();
}

public class ScheduleModel
{
    public ScheduleForm Form { get; set; } = new();
    public List<RouteItem> Routes { get; set; } = new();
    public List<VehicleItem> Vehicles { get; set; } = new();
    public List<DriverItem> Drivers { get; set; } = new();
    public PlanResult? Preview { get; set; }
    public string? LoadError { get; set; }
}

public static class ScheduleOptions
{
    public static readonly (DayOfWeek Day, string Label)[] Days =
    {
        (DayOfWeek.Monday, "Thứ 2"), (DayOfWeek.Tuesday, "Thứ 3"), (DayOfWeek.Wednesday, "Thứ 4"),
        (DayOfWeek.Thursday, "Thứ 5"), (DayOfWeek.Friday, "Thứ 6"), (DayOfWeek.Saturday, "Thứ 7"),
        (DayOfWeek.Sunday, "Chủ nhật")
    };

    public static string DayLabel(DayOfWeek d) => Days.First(x => x.Day == d).Label;
}
