using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HeThongVanTai.Web.Models.ViewModels.VanChuyen;

// Đọc số thập phân theo culture invariant, tránh máy vi-VN hiểu "2.5" thành 25
public class InvariantDecimalBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext ctx)
    {
        var vr = ctx.ValueProvider.GetValue(ctx.ModelName);
        if (vr == ValueProviderResult.None || string.IsNullOrWhiteSpace(vr.FirstValue))
            return Task.CompletedTask;   // để mặc định 0, Range sẽ báo lỗi

        var raw = vr.FirstValue.Trim().Replace(',', '.');
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            ctx.Result = ModelBindingResult.Success(d);
        else
            ctx.ModelState.TryAddModelError(ctx.ModelName, "Giá trị số không hợp lệ.");
        return Task.CompletedTask;
    }
}

// ===== Form nhập =====
public class ParcelForm
{
    public string GoodsType { get; set; } = "Other";

    [Required(ErrorMessage = "Vui lòng nhập tên hàng hóa")]
    [MaxLength(200, ErrorMessage = "Tên hàng hóa tối đa 200 ký tự")]
    public string Name { get; set; } = "";

    [ModelBinder(typeof(InvariantDecimalBinder))]
    [Range(0.01, 1000, ErrorMessage = "Trọng lượng từ 0,01 đến 1000 kg")]
    public decimal Weight { get; set; }

    [Range(0, 300, ErrorMessage = "Chiều dài từ 0 đến 300 cm")] public int Length { get; set; }
    [Range(0, 300, ErrorMessage = "Chiều rộng từ 0 đến 300 cm")] public int Width { get; set; }
    [Range(0, 300, ErrorMessage = "Chiều cao từ 0 đến 300 cm")] public int Height { get; set; }
    [Range(1, 100, ErrorMessage = "Số lượng từ 1 đến 100")] public int Quantity { get; set; } = 1;
}

public class QuoteBody
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bến gửi")] public int FromStationId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bến nhận")] public int ToStationId { get; set; }
    public string ServiceType { get; set; } = "Standard";
    public string PickupMode { get; set; } = "Door";
    public string DeliveryMode { get; set; } = "Door";

    [ModelBinder(typeof(InvariantDecimalBinder))]
    [Range(0, 1_000_000_000, ErrorMessage = "Giá trị hàng hóa không hợp lệ")]
    public decimal DeclaredValue { get; set; }

    [MaxLength(30, ErrorMessage = "Mã khuyến mãi tối đa 30 ký tự")] public string? PromoCode { get; set; }

    [MinLength(1, ErrorMessage = "Cần ít nhất một kiện hàng")]
    public List<ParcelForm> Parcels { get; set; } = new() { new ParcelForm() };
}

public class ShipmentForm : QuoteBody
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên người gửi")]
    [MaxLength(100, ErrorMessage = "Tên người gửi tối đa 100 ký tự")]
    public string SenderName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại người gửi")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại người gửi phải gồm 10 chữ số")]
    public string SenderPhone { get; set; } = "";

    [MaxLength(300, ErrorMessage = "Địa chỉ người gửi tối đa 300 ký tự")] public string? SenderAddress { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận")]
    [MaxLength(100, ErrorMessage = "Tên người nhận tối đa 100 ký tự")]
    public string ReceiverName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại người nhận")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại người nhận phải gồm 10 chữ số")]
    public string ReceiverPhone { get; set; } = "";

    [MaxLength(300, ErrorMessage = "Địa chỉ người nhận tối đa 300 ký tự")] public string? ReceiverAddress { get; set; }

    public string FeePayer { get; set; } = "Sender";
    [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự")] public string? Note { get; set; }

    [ValidateNever] public List<StationItem> Stations { get; set; } = new();
}

// ===== Dữ liệu API trả về (khớp JSON của api/shipments) =====
public class QuoteResult
{
    public int DistanceKm { get; set; }
    public decimal ChargeWeightKg { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}

public class CreatedShipment
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class TrackLog
{
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public DateTime At { get; set; }
}

public class TrackResult
{
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public List<TrackLog> Logs { get; set; } = new();
}

public class ShipmentRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string FromStation { get; set; } = "";
    public string ToStation { get; set; } = "";
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public string FeePayer { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int ParcelCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PageResult<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<T> Items { get; set; } = new();
}

public class ShipmentItemInfo
{
    public string GoodsType { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Weight { get; set; }
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Quantity { get; set; }
    public decimal ChargeWeight { get; set; }
}

public class ShipmentLogInfo
{
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public DateTime At { get; set; }
    public string? ByEmail { get; set; }
}

public class TripInfo
{
    public int Id { get; set; }
    public string Route { get; set; } = "";
    public DateTime DepartAt { get; set; }
}

public class ShipmentDetail
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string FromStation { get; set; } = "";
    public string ToStation { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string SenderPhone { get; set; } = "";
    public string? SenderAddress { get; set; }
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public string? ReceiverAddress { get; set; }
    public string PickupMode { get; set; } = "";
    public string DeliveryMode { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string FeePayer { get; set; } = "";
    public decimal DeclaredValue { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public DateTime? PaidAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public TripInfo? Trip { get; set; }
    public List<ShipmentItemInfo> Items { get; set; } = new();
    public List<ShipmentLogInfo> Logs { get; set; } = new();
}

public class AvailableTrip
{
    public int Id { get; set; }
    public string Route { get; set; } = "";
    public DateTime DepartAt { get; set; }
    public string Plate { get; set; } = "";
}

// ===== Model cho từng trang =====
public class QuotePageModel
{
    public List<StationItem> Stations { get; set; } = new();
    public List<ParcelForm> Parcels { get; set; } = new() { new ParcelForm() };
    public string? Error { get; set; }
}

public class TrackPageModel
{
    public string? Code { get; set; }
    public TrackResult? Result { get; set; }
    public string? Error { get; set; }
}

public class ShipmentListViewModel
{
    public List<ShipmentRow> Items { get; set; } = new();
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int Total { get; set; }
    public string? Error { get; set; }
}

public class ShipmentDetailViewModel
{
    public ShipmentDetail? Shipment { get; set; }
    public List<AvailableTrip> Trips { get; set; } = new();
    public string? Error { get; set; }
}

// ===== Nhãn tiếng Việt =====
public static class VanChuyenHelpers
{
    public static readonly (string Value, string Text)[] GoodsTypes =
    {
        ("Document", "Tài liệu"), ("Clothes", "Quần áo"), ("Electronics", "Đồ điện tử"),
        ("Food", "Thực phẩm"), ("Fragile", "Hàng dễ vỡ"), ("Other", "Khác")
    };

    public static readonly (string Value, string Text)[] AllStatuses =
    {
        ("Pending", "Chờ nhận hàng"), ("PickedUp", "Đã nhận hàng"), ("InTransit", "Đang vận chuyển"),
        ("Arrived", "Đã đến bến nhận"), ("Delivered", "Đã giao"), ("Cancelled", "Đã hủy"), ("Returned", "Hoàn hàng")
    };

    public static string GoodsTypeText(string v) => GoodsTypes.FirstOrDefault(x => x.Value == v).Text ?? v;

    public static string StatusText(string s) => AllStatuses.FirstOrDefault(x => x.Value == s).Text ?? s;

    public static string StatusClass(string s) => s switch
    {
        "Delivered" => "pill-ok",
        "Cancelled" or "Returned" => "pill-bad",
        "Pending" => "pill-warn",
        _ => "pill-info"
    };

    public static string ServiceText(string s) => s == "Express" ? "Hỏa tốc" : "Tiêu chuẩn";
    public static string PickupText(string m) => m == "Door" ? "Nhận tận nơi" : "Gửi tại bến";
    public static string DeliveryText(string m) => m == "Door" ? "Giao tận nơi" : "Nhận tại bến";

    public static string FeePayerText(string p) => p switch
    {
        "Receiver" => "Người nhận trả",
        "Debt" => "Ghi nợ",
        _ => "Người gửi trả"
    };

    public static string PaymentText(string p) => p == "Paid" ? "Đã thu cước" : "Chưa thu cước";

    // Giống luồng trạng thái bên API (ShippingRules)
    public static string[] NextStatuses(string current) => current switch
    {
        "Pending" => new[] { "PickedUp", "Cancelled" },
        "PickedUp" => new[] { "InTransit", "Returned" },
        "InTransit" => new[] { "Arrived", "Returned" },
        "Arrived" => new[] { "Delivered", "Returned" },
        _ => Array.Empty<string>()
    };
}