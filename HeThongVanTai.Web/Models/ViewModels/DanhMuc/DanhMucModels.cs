using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HeThongVanTai.Web.Models.ViewModels.DanhMuc;

// ===== Bến xe =====
public class StationItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? City { get; set; }
    public string? Address { get; set; }
}

public class StationForm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên bến xe")]
    [MaxLength(100, ErrorMessage = "Tên bến xe tối đa 100 ký tự")]
    [Display(Name = "Tên bến xe")]
    public string Name { get; set; } = "";

    [MaxLength(100, ErrorMessage = "Thành phố tối đa 100 ký tự")]
    [Display(Name = "Thành phố")]
    public string? City { get; set; }

    [MaxLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }
}

// ===== Tuyến đường =====
public class RouteItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int FromStationId { get; set; }
    public string? FromStationName { get; set; }
    public string? FromCity { get; set; }
    public int ToStationId { get; set; }
    public string? ToStationName { get; set; }
    public string? ToCity { get; set; }
    public int DistanceKm { get; set; }
    public int DurationMin { get; set; }
}

public class RouteForm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên tuyến")]
    [MaxLength(200, ErrorMessage = "Tên tuyến tối đa 200 ký tự")]
    [Display(Name = "Tên tuyến")]
    public string Name { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bến đi")]
    [Display(Name = "Bến đi")]
    public int FromStationId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bến đến")]
    [Display(Name = "Bến đến")]
    public int ToStationId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập quãng đường")]
    [Range(1, 10000, ErrorMessage = "Quãng đường từ 1 đến 10.000 km")]
    [Display(Name = "Quãng đường (km)")]
    public int? DistanceKm { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập thời gian di chuyển")]
    [Range(1, 10080, ErrorMessage = "Thời gian từ 1 đến 10.080 phút")]
    [Display(Name = "Thời gian (phút)")]
    public int? DurationMin { get; set; }

    // Danh sách bến để chọn trong form, không nhận từ client
    [ValidateNever]
    public List<StationItem> Stations { get; set; } = new();
}

// ===== Loại xe và sơ đồ ghế =====
public class SeatItem
{
    public int Id { get; set; }
    public string SeatCode { get; set; } = "";
    public int Floor { get; set; } = 1;
}

public class VehicleTypeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SeatCount { get; set; }
    public List<SeatItem> Seats { get; set; } = new();

    public int Floors => Seats.Count == 0 ? 1 : Seats.Max(s => s.Floor);
}

public class VehicleTypeForm
{
    [Required(ErrorMessage = "Vui lòng nhập tên loại xe")]
    [MaxLength(100, ErrorMessage = "Tên loại xe tối đa 100 ký tự")]
    [Display(Name = "Tên loại xe")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số ghế")]
    [Range(1, 100, ErrorMessage = "Số ghế từ 1 đến 100")]
    [Display(Name = "Số ghế")]
    public int? SeatCount { get; set; }

    [Range(1, 2, ErrorMessage = "Số tầng là 1 hoặc 2")]
    [Display(Name = "Số tầng")]
    public int Floors { get; set; } = 1;
}

public class SeatMapForm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Danh sách ghế dạng JSON do trang gửi lên: [{"code":"A01","floor":1}, ...]
    public string SeatsJson { get; set; } = "[]";

    [ValidateNever]
    public List<SeatItem> Seats { get; set; } = new();
}
