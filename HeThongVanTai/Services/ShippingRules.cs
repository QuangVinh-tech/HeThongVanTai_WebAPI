using System.Security.Cryptography;

namespace HeThongVanTai.Services;

public class ShippingOptions
{
    public decimal PerKm { get; set; } = 150;              // đ/km
    public decimal PerKgStandard { get; set; } = 6000;     // đ/kg
    public decimal PerKgExpress { get; set; } = 10000;
    public decimal MinFee { get; set; } = 20000;
    public decimal DoorPickupFee { get; set; } = 10000;    // nhận tận nơi
    public decimal DoorDeliveryFee { get; set; } = 15000;  // giao tận nơi
    public decimal FragileRate { get; set; } = 0.2m;       // phụ phí hàng dễ vỡ +20%
    public decimal InsuranceFreeUpTo { get; set; } = 1_000_000; // giá trị khai báo trên mức này thì tính bảo hiểm
    public decimal InsuranceRate { get; set; } = 0.005m;   // 0,5% giá trị
}

public static class ShippingRules
{
    public const decimal VolumetricDivisor = 5000m;

    // Khối lượng quy đổi (kg) = Dài x Rộng x Cao (cm) / 5000
    public static decimal VolumetricWeight(int l, int w, int h) => l * w * h / VolumetricDivisor;

    // Tính cước theo số lớn hơn giữa cân thực và cân quy đổi
    public static decimal ChargeWeight(decimal weight, int l, int w, int h)
        => Math.Round(Math.Max(weight, VolumetricWeight(l, w, h)), 2);

    public static decimal CalcFee(ShippingOptions o, string service, int km, decimal chargeKg,
        decimal declaredValue, bool fragile, bool doorPickup, bool doorDelivery)
    {
        var perKg = service == "Express" ? o.PerKgExpress : o.PerKgStandard;
        var fee = km * o.PerKm + chargeKg * perKg;
        if (fragile) fee *= 1 + o.FragileRate;
        if (declaredValue > o.InsuranceFreeUpTo) fee += declaredValue * o.InsuranceRate;
        if (doorPickup) fee += o.DoorPickupFee;
        if (doorDelivery) fee += o.DoorDeliveryFee;
        fee = Math.Max(fee, o.MinFee);
        return Math.Ceiling(fee / 1000m) * 1000m;   // làm tròn lên nghìn đồng
    }

    // Luồng trạng thái hợp lệ
    private static readonly Dictionary<string, string[]> Next = new()
    {
        ["Pending"] = new[] { "PickedUp", "Cancelled" },
        ["PickedUp"] = new[] { "InTransit", "Returned" },
        ["InTransit"] = new[] { "Arrived", "Returned" },
        ["Arrived"] = new[] { "Delivered", "Returned" },
    };
    public static bool CanMove(string from, string to)
        => Next.TryGetValue(from, out var list) && list.Contains(to);

    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public static string NewCode() => "VC" + DateTime.Now.ToString("yyMMdd") + RandomNumberGenerator.GetString(Chars, 5);

    public static string MaskPhone(string p) => p.Length < 7 ? "****" : p[..3] + "****" + p[^3..];
}