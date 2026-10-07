using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HeThongVanTai.Models.DTO
{
    public class StationDto
    {
        [Required(ErrorMessage = "Tên bến xe/điểm đón trả không được để trống")]
        [MaxLength(100)]
        public string Name { get; set; } = "";
        [MaxLength(100)]
        public string? City { get; set; }
        [MaxLength(255)]
        public string? Address { get; set; }
    }

    public class BusRouteDto
    {
        [Required(ErrorMessage = "Tên tuyến đường không được để trống")]
        public string Name { get; set; } = "";
        public int FromStationId { get; set; }
        public int ToStationId { get; set; }
        public int DistanceKm { get; set; }
        public int DurationMin { get; set; }
    }

    public class SeatTemplateItemDto
    {
        [Required]
        public string SeatCode { get; set; } = "";
        public int Floor { get; set; } = 1;
    }

    public class CreateVehicleTypeDto
    {
        [Required]
        public string Name { get; set; } = "";
        public int SeatCount { get; set; }
        public int Floors { get; set; } = 1;
        public List<SeatTemplateItemDto>? CustomSeats { get; set; }
    }

    public class VehicleDto
    {
        [Required(ErrorMessage = "Biển số xe không được để trống")]
        public string Plate { get; set; } = "";
        public int VehicleTypeId { get; set; }
        public DateTime? RegistrationExpiry { get; set; }
        public DateTime? InsuranceExpiry { get; set; }
        public string Status { get; set; } = "Active";
        public string? ImageUrl { get; set; }
    }

    public class DriverDto
    {
        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? LicenseNo { get; set; }
        public string? LicenseClass { get; set; }
        public DateTime? LicenseExpiry { get; set; }
        public string Status { get; set; } = "Active";
        public string? AvatarUrl { get; set; }
    }

    public class CreateTripDto
    {
        public int BusRouteId { get; set; }
        public int VehicleId { get; set; }
        public int DriverId { get; set; }
        public DateTime DepartAt { get; set; }
        public DateTime? ArriveAt { get; set; }
        public decimal Price { get; set; }
    }

    public class GenerateScheduleDto
    {
        public int BusRouteId { get; set; }
        public int VehicleId { get; set; }
        public int DriverId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string DepartureTime { get; set; } = "08:00";
        public List<DayOfWeek> DaysOfWeek { get; set; } = new();
        public decimal Price { get; set; }
    }

    public class ChangeVehicleDto
    {
        public int NewVehicleId { get; set; }
        public int? NewDriverId { get; set; }
    }

    public class UpdateTripStatusDto
    {
        [Required]
        public string Status { get; set; } = "Open";
        public DateTime? NewDepartAt { get; set; }
    }

    public class ImageUploadRequestDto
    {
        [Required(ErrorMessage = "Vui lòng chọn file ảnh")]
        public IFormFile File { get; set; } = null!;
        public string? FileName { get; set; }
        public string? FileDescription { get; set; }
    }

    public class UpdateVehicleImageDto
    {
        [Required(ErrorMessage = "Đường dẫn ảnh không được để trống")]
        public string ImageUrl { get; set; } = "";
    }

    public class UpdateDriverAvatarDto
    {
        [Required(ErrorMessage = "Đường dẫn ảnh không được để trống")]
        public string AvatarUrl { get; set; } = "";
    }

    public class PagedResultDto<T>
    {
        public int TotalItems { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
        public List<T> Items { get; set; } = new();
    }
}