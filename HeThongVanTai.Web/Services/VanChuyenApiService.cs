using System.Text.Json;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.VanChuyen;

namespace HeThongVanTai.Web.Services;

/// <summary>Gọi API Vận chuyển hàng hóa (api/shipments).</summary>
public class VanChuyenApiService
{
    private readonly ApiClient _api;

    public VanChuyenApiService(ApiClient api) => _api = api;

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static object Parcels(QuoteBody b) => b.Parcels.Select(p => new
    {
        goodsType = p.GoodsType,
        name = p.Name.Trim(),
        weight = p.Weight,
        length = p.Length,
        width = p.Width,
        height = p.Height,
        quantity = p.Quantity
    }).ToList();

    public Task<ApiResult<QuoteResult>> QuoteAsync(QuoteBody b) =>
        _api.PostAsync<QuoteResult>("api/shipments/quote", new
        {
            fromStationId = b.FromStationId,
            toStationId = b.ToStationId,
            serviceType = b.ServiceType,
            pickupMode = b.PickupMode,
            deliveryMode = b.DeliveryMode,
            declaredValue = b.DeclaredValue,
            promoCode = Clean(b.PromoCode),
            parcels = Parcels(b)
        });

    public Task<ApiResult<CreatedShipment>> CreateAsync(ShipmentForm f) =>
        _api.PostAsync<CreatedShipment>("api/shipments", new
        {
            fromStationId = f.FromStationId,
            toStationId = f.ToStationId,
            serviceType = f.ServiceType,
            pickupMode = f.PickupMode,
            deliveryMode = f.DeliveryMode,
            declaredValue = f.DeclaredValue,
            promoCode = Clean(f.PromoCode),
            parcels = Parcels(f),
            senderName = f.SenderName.Trim(),
            senderPhone = f.SenderPhone.Trim(),
            senderAddress = Clean(f.SenderAddress),
            receiverName = f.ReceiverName.Trim(),
            receiverPhone = f.ReceiverPhone.Trim(),
            receiverAddress = Clean(f.ReceiverAddress),
            feePayer = f.FeePayer,
            note = Clean(f.Note)
        });

    public Task<ApiResult<TrackResult>> TrackAsync(string code) =>
        _api.GetAsync<TrackResult>("api/shipments/track/" + Uri.EscapeDataString(code.Trim()));

    private static string Query(string? status, string? keyword, int page, int pageSize)
    {
        var q = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(status)) q.Add("status=" + Uri.EscapeDataString(status));
        if (!string.IsNullOrWhiteSpace(keyword)) q.Add("keyword=" + Uri.EscapeDataString(keyword.Trim()));
        return "?" + string.Join("&", q);
    }

    public Task<ApiResult<PageResult<ShipmentRow>>> MineAsync(string? status, int page, int pageSize) =>
        _api.GetAsync<PageResult<ShipmentRow>>("api/shipments/mine" + Query(status, null, page, pageSize));

    public Task<ApiResult<PageResult<ShipmentRow>>> ListAsync(string? status, string? keyword, int page, int pageSize) =>
        _api.GetAsync<PageResult<ShipmentRow>>("api/shipments" + Query(status, keyword, page, pageSize));

    public Task<ApiResult<ShipmentDetail>> DetailAsync(int id) =>
        _api.GetAsync<ShipmentDetail>($"api/shipments/{id}");

    public Task<ApiResult<List<AvailableTrip>>> AvailableTripsAsync(int id) =>
        _api.GetAsync<List<AvailableTrip>>($"api/shipments/{id}/available-trips");

    public Task<ApiResult<JsonElement>> CancelAsync(int id) =>
        _api.PutAsync<JsonElement>($"api/shipments/{id}/cancel");

    public Task<ApiResult<JsonElement>> ChangeStatusAsync(int id, string status, string? note) =>
        _api.PatchAsync<JsonElement>($"api/shipments/{id}/status", new { status, note = Clean(note) });

    public Task<ApiResult<JsonElement>> AssignTripAsync(int id, int tripId) =>
        _api.PutAsync<JsonElement>($"api/shipments/{id}/assign-trip", new { tripId });

    public Task<ApiResult<JsonElement>> CollectFeeAsync(int id) =>
        _api.PatchAsync<JsonElement>($"api/shipments/{id}/collect-fee");
}