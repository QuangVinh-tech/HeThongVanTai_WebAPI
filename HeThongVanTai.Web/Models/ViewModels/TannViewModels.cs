using System;
using System.Collections.Generic;

namespace HeThongVanTai.Web.Models.ViewModels
{
    public class TripSearchInputModel
    {
        public string? DepartureStationId { get; set; }
        public string? ArrivalStationId { get; set; }
        public DateTime DepartureDate { get; set; } = DateTime.Today;
        public int SeatQuantity { get; set; } = 1;
    }

    public class TripItemViewModel
    {
        public int TripId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public string DepartureStation { get; set; } = string.Empty;
        public string ArrivalStation { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime EstimatedArrivalTime { get; set; }
        public decimal Price { get; set; }
        public int AvailableSeats { get; set; }
        public string VehicleTypeName { get; set; } = string.Empty;
    }

    public class SeatItemViewModel
    {
        public string SeatCode { get; set; } = string.Empty;
        public int Floor { get; set; }
        public bool IsAvailable { get; set; }
        public string Status { get; set; } = "Available";
    }

    public class TripSeatsViewModel
    {
        public int TripId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public string VehiclePlate { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public DateTime DepartAt { get; set; }
        public decimal Price { get; set; }
        public List<SeatItemViewModel> Seats { get; set; } = new();
    }

    public class BookingCreateViewModel
    {
        public int TripId { get; set; }
        public string PassengerName { get; set; } = string.Empty;
        public string PassengerPhone { get; set; } = string.Empty;
        public string SelectedSeats { get; set; } = string.Empty;
        public string? PromotionCode { get; set; }
        public decimal TotalAmount { get; set; }
        public int HoldMinutes { get; set; } = 10;
    }

    public class TicketDetailViewModel
    {
        public string BookingCode { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string PassengerPhone { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public DateTime DepartAt { get; set; }
        public string Seats { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Paid";
        public string QrCodeUrl { get; set; } = string.Empty;
    }
}