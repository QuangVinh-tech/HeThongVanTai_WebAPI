using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

public class BookingService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public BookingService(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    private int HoldMinutes => _cfg.GetValue<int?>("Booking:HoldMinutes") ?? 15;

    private static object Shape(Booking b) => new
    {
        b.Id,
        b.Code,
        b.Status,
        b.TotalAmount,
        b.DiscountAmount,
        b.HoldExpiresAt,
        Tickets = b.Tickets.Select(t => new { t.Id, t.TicketCode, t.SeatCode, t.PassengerName, t.Price, t.Status })
    };

    // ===================== #22 Tìm chuyến =====================
    public async Task<object> SearchAsync(int fromId, int toId, DateTime date)
    {
        await ReleaseExpiredAsync();
        var now = DateTime.Now;
        var start = date.Date;
        var end = start.AddDays(1);

        return await _db.Trips.AsNoTracking()
            .Where(t => t.Status == "Open" && t.DepartAt > now
                     && t.BusRoute.FromStationId == fromId && t.BusRoute.ToStationId == toId
                     && t.DepartAt >= start && t.DepartAt < end)
            .OrderBy(t => t.DepartAt)
            .Select(t => new
            {
                TripId = t.Id,
                Route = t.BusRoute.Name,
                t.DepartAt,
                t.ArriveAt,
                t.Price,
                Plate = t.Vehicle.Plate,
                TotalSeats = t.Vehicle.VehicleType.SeatCount,
                AvailableSeats = t.Vehicle.VehicleType.SeatCount - t.Tickets.Count(k => k.Status != "Cancelled")
            })
            .ToListAsync();
    }

    // ============== #22 + #23 + #26 Đặt vé, giữ ghế, áp mã ==============
    public async Task<(int Id, object Body)> CreateAsync(CreateBookingDto dto)
    {
        var now = DateTime.Now;

        var trip = await _db.Trips.Include(t => t.Vehicle).FirstOrDefaultAsync(t => t.Id == dto.TripId)
                   ?? throw new ServiceException(404, "Chuyến không tồn tại.");
        if (trip.Status != "Open" || trip.DepartAt <= now)
            throw new ServiceException(400, "Chuyến đã đóng hoặc đã khởi hành.");
        if (!await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId))
            throw new ServiceException(400, "Khách hàng không tồn tại.");

        var seats = dto.Seats
            .Select(s => new { Code = s.SeatCode.Trim().ToUpperInvariant(), s.PassengerName })
            .ToList();
        var codes = seats.Select(s => s.Code).ToList();
        if (codes.Distinct().Count() != codes.Count)
            throw new ServiceException(400, "Trùng mã ghế trong yêu cầu.");

        var valid = await _db.SeatTemplates
            .CountAsync(s => s.VehicleTypeId == trip.Vehicle.VehicleTypeId && codes.Contains(s.SeatCode));
        if (valid != codes.Count)
            throw new ServiceException(400, "Có mã ghế không thuộc loại xe của chuyến.");

        // Nhả các giữ chỗ đã quá hạn của chuyến này trước khi kiểm tra ghế
        await ReleaseExpiredAsync(trip.Id);

        var taken = await _db.Tickets
            .Where(t => t.TripId == trip.Id && t.Status != "Cancelled" && codes.Contains(t.SeatCode))
            .Select(t => t.SeatCode)
            .ToListAsync();
        if (taken.Count > 0)
            throw new ServiceException(409, $"Ghế đã có người đặt: {string.Join(", ", taken)}");

        // Khuyến mãi
        var subtotal = trip.Price * seats.Count;
        decimal discount = 0;
        Promotion? promo = null;
        if (!string.IsNullOrWhiteSpace(dto.PromoCode))
        {
            var pc = dto.PromoCode.Trim().ToUpperInvariant();
            promo = await _db.Promotions.AsNoTracking().FirstOrDefaultAsync(p => p.Code == pc)
                    ?? throw new ServiceException(400, "Mã giảm giá không tồn tại.");
            discount = Rules.Discount(promo, subtotal, now, out var err);
            if (err != null) throw new ServiceException(400, err);
        }

        var booking = new Booking
        {
            Code = Rules.NewBookingCode(),
            CustomerId = dto.CustomerId,
            Status = "Holding",
            HoldExpiresAt = now.AddMinutes(HoldMinutes),
            CreatedAt = now,
            DiscountAmount = discount,
            PromotionId = promo?.Id,
            TotalAmount = subtotal - discount,
            Tickets = seats.Select(s => new Ticket
            {
                TripId = trip.Id,
                SeatCode = s.Code,
                PassengerName = s.PassengerName,
                Price = trip.Price,
                TicketCode = Rules.NewTicketCode(),
                Status = "Active"
            }).ToList()
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Bookings.Add(booking);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (Rules.IsDuplicate(ex))
        {
            // 2 người bấm cùng lúc: unique index (TripId, SeatCode) chặn người đến sau
            throw new ServiceException(409, "Ghế vừa có người khác giữ, vui lòng chọn ghế khác.");
        }

        if (promo != null)
        {
            var rows = await _db.Promotions
                .Where(p => p.Id == promo.Id && (p.UsageLimit == null || p.UsedCount < p.UsageLimit))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount + 1));
            if (rows == 0) throw new ServiceException(409, "Mã giảm giá vừa hết lượt sử dụng.");
        }

        await tx.CommitAsync();
        return (booking.Id, Shape(booking));
    }

    public async Task<object> GetAsync(int id)
    {
        var b = await _db.Bookings.AsNoTracking().Include(x => x.Tickets).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy booking.");
        return Shape(b);
    }

    // ============ #23 Nhả giữ chỗ quá hạn ============
    // Phải đổi vé sang Cancelled thì unique index mới cho người khác đặt lại ghế đó.
    public async Task<int> ReleaseExpiredAsync(int? tripId = null)
    {
        var now = DateTime.Now;
        var q = _db.Bookings.Where(b => b.Status == "Holding" && b.HoldExpiresAt < now);
        if (tripId.HasValue) q = q.Where(b => b.Tickets.Any(t => t.TripId == tripId.Value));

        var list = await q.Select(b => new { b.Id, b.PromotionId }).ToListAsync();
        if (list.Count == 0) return 0;
        var ids = list.Select(x => x.Id).ToList();

        await _db.Tickets.Where(t => ids.Contains(t.BookingId) && t.Status != "Cancelled")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "Cancelled"));
        await _db.Payments.Where(p => ids.Contains(p.BookingId) && p.Status == "Pending")
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Failed"));
        await _db.Bookings.Where(b => ids.Contains(b.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "Expired"));

        foreach (var pid in list.Where(x => x.PromotionId != null).Select(x => x.PromotionId!.Value))
        {
            await _db.Promotions.Where(p => p.Id == pid && p.UsedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount - 1));
        }
        return ids.Count;
    }

    // ============ #24 Hủy booking + hoàn tiền ============
    public async Task<object> CancelAsync(int id)
    {
        var b = await _db.Bookings
                    .Include(x => x.Tickets).ThenInclude(t => t.Trip)
                    .Include(x => x.Payments)
                    .FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy booking.");
        if (b.Status is not ("Holding" or "Paid"))
            throw new ServiceException(400, "Không thể hủy booking ở trạng thái này.");

        var now = DateTime.Now;
        var active = b.Tickets.Where(t => t.Status != "Cancelled").ToList();
        var depart = active.Count > 0 ? active.Min(t => t.Trip.DepartAt) : now;
        var wasHolding = b.Status == "Holding";

        decimal rate = 0, refund = 0;
        if (b.Status == "Paid")
        {
            if (depart <= now) throw new ServiceException(400, "Chuyến đã khởi hành, không thể hủy.");
            var paidPayment = b.Payments.First(p => p.Status == "Paid");
            var paid = b.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount);
            rate = Rules.RefundRate(depart, now);
            refund = Math.Round(paid * rate, 0);
            if (refund > 0)
            {
                _db.Payments.Add(new Payment
                {
                    BookingId = b.Id,
                    Amount = refund,
                    Method = paidPayment.Method,
                    Status = "Refunded",
                    PaidAt = now
                });
            }
        }

        foreach (var p in b.Payments.Where(p => p.Status == "Pending")) p.Status = "Failed";
        foreach (var t in b.Tickets) t.Status = "Cancelled";   // nhả ghế
        b.Status = "Cancelled";
        b.HoldExpiresAt = null;
        await _db.SaveChangesAsync();

        if (wasHolding && b.PromotionId != null)
        {
            var promoId = b.PromotionId.Value;
            await _db.Promotions.Where(p => p.Id == promoId && p.UsedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount - 1));
        }

        return new { BookingId = b.Id, b.Status, RefundRate = rate, RefundAmount = refund };
    }

    // ============ #24 Đổi vé: đổi ghế, hoặc đổi sang chuyến khác cùng tuyến ============
    public async Task<object> ChangeTicketAsync(int ticketId, ChangeTicketDto dto)
    {
        var t = await _db.Tickets.Include(x => x.Booking).Include(x => x.Trip)
                    .FirstOrDefaultAsync(x => x.Id == ticketId)
                ?? throw new ServiceException(404, "Không tìm thấy vé.");
        if (t.Status != "Active")
            throw new ServiceException(400, "Chỉ đổi được vé còn hiệu lực.");
        if (t.Booking.Status is not ("Holding" or "Paid"))
            throw new ServiceException(400, "Booking không cho phép đổi vé.");

        var now = DateTime.Now;
        if (t.Trip.DepartAt - now < TimeSpan.FromHours(2))
            throw new ServiceException(400, "Chỉ được đổi vé trước giờ khởi hành ít nhất 2 giờ.");

        var newTripId = dto.NewTripId ?? t.TripId;
        var newSeat = dto.NewSeatCode.Trim().ToUpperInvariant();
        if (newTripId == t.TripId && newSeat == t.SeatCode)
            throw new ServiceException(400, "Ghế mới trùng ghế hiện tại.");

        var newTrip = newTripId == t.TripId
            ? t.Trip
            : await _db.Trips.FirstOrDefaultAsync(x => x.Id == newTripId)
              ?? throw new ServiceException(404, "Chuyến mới không tồn tại.");
        if (newTrip.Status != "Open" || newTrip.DepartAt <= now)
            throw new ServiceException(400, "Chuyến mới đã đóng hoặc đã khởi hành.");
        if (newTrip.BusRouteId != t.Trip.BusRouteId)
            throw new ServiceException(400, "Chỉ đổi sang chuyến cùng tuyến.");

        var vtId = await _db.Vehicles.Where(v => v.Id == newTrip.VehicleId)
            .Select(v => v.VehicleTypeId).FirstAsync();
        if (!await _db.SeatTemplates.AnyAsync(s => s.VehicleTypeId == vtId && s.SeatCode == newSeat))
            throw new ServiceException(400, "Ghế mới không thuộc loại xe của chuyến.");

        await ReleaseExpiredAsync(newTripId);
        if (await _db.Tickets.AnyAsync(k => k.TripId == newTripId && k.SeatCode == newSeat
                                           && k.Status != "Cancelled" && k.Id != t.Id))
            throw new ServiceException(409, "Ghế mới đã có người đặt.");

        var diff = newTrip.Price - t.Price;
        if (t.Booking.Status == "Paid" && diff > 0)
            throw new ServiceException(400, "Chuyến mới giá cao hơn vé đã thanh toán, hãy hủy và đặt lại.");
        if (t.Booking.Status == "Holding" && diff != 0)
        {
            t.Booking.TotalAmount += diff;
            t.Price = newTrip.Price;
        }

        t.TripId = newTripId;
        t.Trip = newTrip;
        t.SeatCode = newSeat;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (Rules.IsDuplicate(ex))
        {
            throw new ServiceException(409, "Ghế mới vừa có người khác giữ.");
        }
        return new { t.Id, t.TicketCode, t.TripId, t.SeatCode, t.Price, t.Status };
    }

    // ============ #27 Tra cứu vé theo mã ============
    public async Task<object> GetTicketInfoAsync(string ticketCode)
    {
        var code = ticketCode.Trim().ToUpperInvariant();
        return await _db.Tickets.AsNoTracking()
                   .Where(t => t.TicketCode == code)
                   .Select(t => new
                   {
                       t.TicketCode,
                       t.Status,
                       t.SeatCode,
                       t.PassengerName,
                       t.Price,
                       BookingCode = t.Booking.Code,
                       BookingStatus = t.Booking.Status,
                       Customer = t.Booking.Customer.FullName,
                       Route = t.Trip.BusRoute.Name,
                       t.Trip.DepartAt,
                       Plate = t.Trip.Vehicle.Plate
                   })
                   .FirstOrDefaultAsync()
               ?? throw new ServiceException(404, "Không tìm thấy vé.");
    }
}