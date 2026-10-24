using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.VeKhach;

namespace HeThongVanTai.Web.Services;

/// <summary>Gọi API vé của khách: danh sách, chi tiết, hủy, thanh toán chuyển khoản, tra cứu, mã QR.</summary>
public class VeKhachApiService
{
    private readonly ApiClient _api;

    public VeKhachApiService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<MyBookingPage>> GetMineAsync(string? status, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(status)) query.Add("status=" + Uri.EscapeDataString(status));
        return _api.GetAsync<MyBookingPage>("api/bookings/mine?" + string.Join("&", query));
    }

    public Task<ApiResult<BookingDetail>> GetDetailAsync(string code) =>
        _api.GetAsync<BookingDetail>($"api/bookings/mine/{Uri.EscapeDataString(code)}");

    // Yêu cầu đổi ghế của chính khách (API chỉ cho vai trò Customer)
    public Task<ApiResult<List<HeThongVanTai.Web.Models.ViewModels.DatVe.SeatChangeItem>>> GetMySeatChangesAsync() =>
        _api.GetAsync<List<HeThongVanTai.Web.Models.ViewModels.DatVe.SeatChangeItem>>("api/seat-change-requests/mine");

    public Task<ApiResult<RefundPreview>> GetRefundPreviewAsync(int bookingId) =>
        _api.GetAsync<RefundPreview>($"api/bookings/{bookingId}/refund-preview");

    public Task<ApiResult<CancelResult>> CancelAsync(int bookingId) =>
        _api.PutAsync<CancelResult>($"api/bookings/{bookingId}/cancel");

    // Khách chỉ được chọn chuyển khoản; tiền mặt chỉ thu tại quầy (API từ chối)
    public Task<ApiResult<string>> PayByTransferAsync(int bookingId) =>
        _api.PostAsync<string>("api/Payments", new { bookingId, method = "BankTransfer" });

    public Task<ApiResult<BookingDetail>> LookupAsync(string code, string contact) =>
        _api.PostAsync<BookingDetail>("api/bookings/lookup", new { code, contact });

    public Task<ApiResult<byte[]>> GetQrAsync(string ticketCode) =>
        _api.GetBytesAsync($"api/Tickets/{Uri.EscapeDataString(ticketCode)}/qr");
}
