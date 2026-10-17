using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongVanTai.Services;

public class ShipmentService
{
    private readonly AppDbContext _db;
    private readonly ShippingOptions _opt;
    private readonly ILogger<ShipmentService> _logger;

    public ShipmentService(AppDbContext db, IOptions<ShippingOptions> opt, ILogger<ShipmentService> logger)
    { _db = db; _opt = opt.Value; _logger = logger; }

    private static readonly string[] Services = { "Standard", "Express" };
    private static readonly string[] Modes = { "Door", "Station" };

    // Tính cước (chưa áp khuyến mãi). Dùng chung cho Quote và Create.
    private async Task<QuoteResultDto> PriceAsync(QuoteRequestDto dto)
    {
        if (dto.FromStationId == dto.ToStationId)
            throw new ServiceException(400, "Bến gửi và bến nhận phải khác nhau.");
        if (!Services.Contains(dto.ServiceType) || !Modes.Contains(dto.PickupMode) || !Modes.Contains(dto.DeliveryMode))
            throw new ServiceException(400, "Loại dịch vụ hoặc hình thức giao nhận không hợp lệ.");
        if (dto.Parcels.Count == 0)
            throw new ServiceException(400, "Cần ít nhất một kiện hàng.");

        // Khoảng cách lấy từ tuyến xe khách (không phân biệt chiều đi/về)
        var route = await _db.BusRoutes.AsNoTracking().FirstOrDefaultAsync(r =>
            (r.FromStationId == dto.FromStationId && r.ToStationId == dto.ToStationId) ||
            (r.FromStationId == dto.ToStationId && r.ToStationId == dto.FromStationId));
        if (route is null)
            throw new ServiceException(400, "Chưa có tuyến xe giữa hai bến này nên chưa nhận hàng.");

        decimal chargeKg = 0; var fragile = false;
        foreach (var p in dto.Parcels)
        {
            chargeKg += ShippingRules.ChargeWeight(p.Weight, p.Length, p.Width, p.Height) * p.Quantity;
            if (p.GoodsType == "Fragile") fragile = true;
        }

        var fee = ShippingRules.CalcFee(_opt, dto.ServiceType, route.DistanceKm, chargeKg,
            dto.DeclaredValue, fragile, dto.PickupMode == "Door", dto.DeliveryMode == "Door");

        return new QuoteResultDto
        {
            DistanceKm = route.DistanceKm,
            ChargeWeightKg = chargeKg,
            ShippingFee = fee,
            Total = fee
        };
    }

    public async Task<QuoteResultDto> QuoteAsync(QuoteRequestDto dto)
    {
        var q = await PriceAsync(dto);
        if (!string.IsNullOrWhiteSpace(dto.PromoCode))
        {
            var code = dto.PromoCode.Trim().ToUpperInvariant();
            var promo = await _db.Promotions.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code)
                ?? throw new ServiceException(404, "Mã giảm giá không tồn tại.");
            q.Discount = Rules.Discount(promo, q.ShippingFee, DateTime.Now, out var err);
            if (err != null) throw new ServiceException(400, err);
            q.Total = q.ShippingFee - q.Discount;
        }
        return q;
    }

    public async Task<Shipment> CreateAsync(CreateShipmentDto dto, string? userId, string? email, bool canDebt)
    {
        if (dto.PickupMode == "Door" && string.IsNullOrWhiteSpace(dto.SenderAddress))
            throw new ServiceException(400, "Nhận tận nơi thì phải nhập địa chỉ người gửi.");
        if (dto.DeliveryMode == "Door" && string.IsNullOrWhiteSpace(dto.ReceiverAddress))
            throw new ServiceException(400, "Giao tận nơi thì phải nhập địa chỉ người nhận.");
        if (dto.FeePayer is not ("Sender" or "Receiver" or "Debt"))
            throw new ServiceException(400, "Người trả cước không hợp lệ.");
        if (dto.FeePayer == "Debt" && !canDebt)
            throw new ServiceException(403, "Chỉ nhân viên bán vé hoặc Admin mới được ghi nợ.");

        var q = await PriceAsync(dto);

        Promotion? promo = null; decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(dto.PromoCode))
        {
            var code = dto.PromoCode.Trim().ToUpperInvariant();
            promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Code == code)
                ?? throw new ServiceException(404, "Mã giảm giá không tồn tại.");
            discount = Rules.Discount(promo, q.ShippingFee, DateTime.Now, out var err);
            if (err != null) throw new ServiceException(400, err);
            promo.UsedCount++;   // TODO: xem BookingService xử lý lượt dùng khi nhiều người đặt cùng lúc rồi làm giống
        }

        string newCode;
        do { newCode = ShippingRules.NewCode(); }
        while (await _db.Shipments.AnyAsync(s => s.Code == newCode));

        var s = new Shipment
        {
            Code = newCode,
            FromStationId = dto.FromStationId,
            ToStationId = dto.ToStationId,
            SenderName = dto.SenderName.Trim(),
            SenderPhone = dto.SenderPhone,
            SenderAddress = dto.SenderAddress?.Trim(),
            ReceiverName = dto.ReceiverName.Trim(),
            ReceiverPhone = dto.ReceiverPhone,
            ReceiverAddress = dto.ReceiverAddress?.Trim(),
            PickupMode = dto.PickupMode,
            DeliveryMode = dto.DeliveryMode,
            ServiceType = dto.ServiceType,
            FeePayer = dto.FeePayer,
            DeclaredValue = dto.DeclaredValue,
            ShippingFee = q.ShippingFee,
            DiscountAmount = discount,
            TotalAmount = q.ShippingFee - discount,
            PromotionId = promo?.Id,
            Note = dto.Note,
            CreatedByUserId = userId,
            Items = dto.Parcels.Select(p => new ShipmentItem
            {
                GoodsType = p.GoodsType,
                Name = p.Name.Trim(),
                Weight = p.Weight,
                Length = p.Length,
                Width = p.Width,
                Height = p.Height,
                Quantity = p.Quantity,
                ChargeWeight = ShippingRules.ChargeWeight(p.Weight, p.Length, p.Width, p.Height)
            }).ToList(),
            Logs = { new ShipmentLog { Status = "Pending", Note = "Tạo đơn", ByEmail = email } }
        };
        _db.Shipments.Add(s);
        await _db.SaveChangesAsync();

        // Chỉ ghi mã và email, không ghi tên, SĐT, địa chỉ (đúng quy ước log trong README)
        _logger.LogInformation("Tạo đơn hàng {Code} bởi {Email}", s.Code, email);
        return s;
    }

    public async Task ChangeStatusAsync(int id, string status, string? note, string? email)
    {
        var s = await _db.Shipments.FindAsync(id)
            ?? throw new ServiceException(404, "Không tìm thấy đơn hàng.");
        if (!ShippingRules.CanMove(s.Status, status))
            throw new ServiceException(400, $"Không thể chuyển trạng thái từ {s.Status} sang {status}.");

        s.Status = status;
        _db.ShipmentLogs.Add(new ShipmentLog { ShipmentId = s.Id, Status = status, Note = note, ByEmail = email });
        await _db.SaveChangesAsync();
        _logger.LogInformation("Đơn hàng {Code} chuyển sang {Status} bởi {Email}", s.Code, status, email);
    }

    public async Task AssignTripAsync(int id, int tripId, string? email)
    {
        var s = await _db.Shipments.FindAsync(id) ?? throw new ServiceException(404, "Không tìm thấy đơn hàng.");
        var trip = await _db.Trips.Include(t => t.BusRoute).FirstOrDefaultAsync(t => t.Id == tripId)
            ?? throw new ServiceException(404, "Không tìm thấy chuyến xe.");
        if (s.Status is "Delivered" or "Cancelled" or "Returned")
            throw new ServiceException(400, "Đơn đã kết thúc, không gắn chuyến được.");

        // Chuyến phải đi đúng tuyến của đơn (cùng cặp bến, hai chiều đều được)
        var r = trip.BusRoute;
        var ok = (r.FromStationId == s.FromStationId && r.ToStationId == s.ToStationId) ||
                 (r.FromStationId == s.ToStationId && r.ToStationId == s.FromStationId);
        if (!ok) throw new ServiceException(400, "Chuyến xe không đi đúng tuyến của đơn hàng.");

        s.TripId = tripId;
        await _db.SaveChangesAsync();
    }
    public async Task CancelAsync(int id, string userId, string? email)
    {
        var s = await _db.Shipments.FirstOrDefaultAsync(x => x.Id == id && x.CreatedByUserId == userId)
            ?? throw new ServiceException(404, "Không tìm thấy đơn hàng.");
        if (!ShippingRules.CanMove(s.Status, "Cancelled"))
            throw new ServiceException(400, "Chỉ hủy được đơn đang chờ xử lý.");

        s.Status = "Cancelled";
        _db.ShipmentLogs.Add(new ShipmentLog
        {
            ShipmentId = s.Id,
            Status = "Cancelled",
            Note = "Khách hủy đơn",
            ByEmail = email
        });
        await _db.SaveChangesAsync();
        _logger.LogInformation("Đơn hàng {Code} bị hủy bởi {Email}", s.Code, email);
    }
    public async Task CollectFeeAsync(int id, string? email)
    {
        var s = await _db.Shipments.FindAsync(id) ?? throw new ServiceException(404, "Không tìm thấy đơn hàng.");
        if (s.PaymentStatus == "Paid") throw new ServiceException(409, "Đơn đã thu cước rồi.");
        if (s.Status == "Cancelled") throw new ServiceException(400, "Đơn đã hủy.");
        s.PaymentStatus = "Paid"; s.PaidAt = DateTime.Now;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Thu cước đơn {Code} bởi {Email}", s.Code, email);
    }
}