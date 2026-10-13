using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HeThongVanTai.Web.Models.ViewModels;

namespace HeThongVanTai.Web.Services
{
    public class TannApiService
    {
        private readonly HttpClient _client;

        public TannApiService(HttpClient client)
        {
            _client = client;
        }

        public async Task<List<TripItemViewModel>> SearchTripsAsync(TripSearchInputModel input)
        {
            var url = $"api/trips/search?DepartureStationId={input.DepartureStationId}&ArrivalStationId={input.ArrivalStationId}&DepartureDate={input.DepartureDate:yyyy-MM-dd}&SeatQuantity={input.SeatQuantity}";
            var res = await _client.GetAsync(url);
            if (!res.IsSuccessStatusCode) return new List<TripItemViewModel>();
            return await res.Content.ReadFromJsonAsync<List<TripItemViewModel>>() ?? new List<TripItemViewModel>();
        }

        public async Task<TripSeatsViewModel?> GetTripSeatsAsync(int tripId)
        {
            var res = await _client.GetAsync($"api/trips/{tripId}/seats");
            if (!res.IsSuccessStatusCode) return null;
            return await res.Content.ReadFromJsonAsync<TripSeatsViewModel>();
        }

        public async Task<bool> CreateBookingAsync(BookingCreateViewModel model)
        {
            var res = await _client.PostAsJsonAsync("api/bookings", model);
            return res.IsSuccessStatusCode;
        }

        public async Task<TicketDetailViewModel?> GetBookingByCodeAsync(string code)
        {
            var res = await _client.GetAsync($"api/bookings/{code}");
            if (!res.IsSuccessStatusCode) return null;
            return await res.Content.ReadFromJsonAsync<TicketDetailViewModel>();
        }
    }
}