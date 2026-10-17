using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

public class BookingService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ILogger<BookingService> _logger;

    public BookingService(AppDbContext db, IConfiguration cfg, ILogger<BookingService> logger)
    {
        _db = db;
        _cfg = cfg;
        _logger = logger;
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
        b.ContactPhone,
        b.NeedTransfer,
        b.TransferAddress,
        Tickets = b.Tickets.Select(t => new { t.Id, t.TicketCode, t.TripId, t.SeatCode, t.PassengerName, t.Price, t.Status })
    };

    // ===================== Tìm chuyến =====================
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
                VehicleType = t.Vehicle.VehicleType.Name,
                TotalSeats = t.Vehicle.VehicleType.SeatCount,
                AvailableSeats = t.Vehicle.VehicleType.SeatCount - t.Tickets.Count(k => k.Status != "Cancelled")
            })
            .ToListAsync();
    }

    // Tìm chuyến có lọc, sắp xếp, phân trang (Web dùng). Kết quả: { items, total, page, pageSize }
    // sort: "time" (mặc định), "time_desc", "price", "price_desc"
    public async Task<object> SearchPagedAsync(int fromId, int toId, DateTime date,
        int? fromHour, int? toHour, int? vehicleTypeId, decimal? minPrice, decimal? maxPrice,
        int? tickets, string? sort, int page, int pageSize)
    {
        await ReleaseExpiredAsync();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var now = DateTime.Now;
        var start = date.Date;
        var end = start.AddDays(1);

        var q = _db.Trips.AsNoTracking()
            .Where(t => t.Status == "Open" && t.DepartAt > now
                     && t.BusRoute.FromStationId == fromId && t.BusRoute.ToStationId == toId
                     && t.DepartAt >= start && t.DepartAt < end);

        if (fromHour.HasValue) q = q.Where(t => t.DepartAt.Hour >= fromHour.Value);
        if (toHour.HasValue) q = q.Where(t => t.DepartAt.Hour < toHour.Value);
        if (vehicleTypeId.HasValue) q = q.Where(t => t.Vehicle.VehicleType.Id == vehicleTypeId.Value);
        if (minPrice.HasValue) q = q.Where(t => t.Price >= minPrice.Value);
        if (maxPrice.HasValue) q = q.Where(t => t.Price <= maxPrice.Value);
        if (tickets.HasValue && tickets.Value > 0)
            q = q.Where(t => t.Vehicle.VehicleType.SeatCount - t.Tickets.Count(k => k.Status != "Cancelled") >= tickets.Value);

        q = (sort ?? "").ToLowerInvariant() switch
        {
            "price" or "price_asc" => q.OrderBy(t => t.Price),
            "price_desc" => q.OrderByDescending(t => t.Price),
            "time_desc" => q.OrderByDescending(t => t.DepartAt),
            _ => q.OrderBy(t => t.DepartAt)
        };

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new
            {
                TripId = t.Id,
                Route = t.BusRoute.Name,
                t.DepartAt,
                t.ArriveAt,
                t.Price,
                Plate = t.Vehicle.Plate,
                VehicleType = t.Vehicle.VehicleType.Name,
                TotalSeats = t.Vehicle.VehicleType.SeatCount,
                AvailableSeats = t.Vehicle.VehicleType.SeatCount - t.Tickets.Count(k => k.Status != "Cancelled")
            })
            .ToListAsync();

        return new { items, total, page, pageSize };
    }

    // ============== Đặt vé, giữ ghế, áp mã ==============
    public async Task<(int Id, object Body)> CreateAsync(CreateBookingDto dto)
    {
        var now = DateTime.Now;

        var trip = await _db.Trips.Include(t => t.Vehicle).FirstOrDefaultAsync(t => t.Id == dto.TripId)
                   ?? throw new ServiceException(404, "Chuyến không tồn tại.");
        if (trip.Status != "Open" || trip.DepartAt <= now)
            throw new ServiceException(400, "Chuyến đã đóng hoặc đã khởi hành.");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId)
                       ?? throw new ServiceException(400, "Khách hàng không tồn tại.");

        // Liên hệ và trung chuyển
        var contactPhone = dto.ContactPhone?.Trim();
        if (string.IsNullOrWhiteSpace(contactPhone))
            throw new ServiceException(400, "Vui lòng nhập số điện thoại liên hệ.");
        var transferAddress = dto.TransferAddress?.Trim();
        if (dto.NeedTransfer && string.IsNullOrWhiteSpace(transferAddress))
            throw new ServiceException(400, "Vui lòng nhập địa chỉ trung chuyển.");
        if (!dto.NeedTransfer) transferAddress = null;

        var seats = dto.Seats
            .Select(s => new { Code = s.SeatCode.Trim().ToUpperInvariant(), s.PassengerName })
            .ToList();
        var codes = seats.Select(s => s.Code).ToList();
        if (codes.Distinct().Count() != codes.Count)
            throw new ServiceException(400, "Trùng mã ghế trong yêu cầu.");

        var valid = await _db.SeatTemplates
            .CountAsync(s => s.VehicleTypeId == trip.Vehicle.VehicleTypeId && codes.Contains(s.SeatCode));
        if (valid != codes.Count)
            throw new ServiceException(400, "Có mã ghế không thuộc loại xe của chuyến này.");

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
            ContactPhone = contactPhone,
            NeedTransfer = dto.NeedTransfer,
            TransferAddress = transferAddress,
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

        if (string.IsNullOrWhiteSpace(customer.Phone)) customer.Phone = contactPhone;

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
        _logger.LogInformation("Đặt vé: booking {BookingCode}, chuyến {TripId}, ghế {Seats}, tổng {Total}, khuyến mãi {PromoCode}",
            booking.Code, trip.Id, string.Join(",", codes), booking.TotalAmount, promo?.Code ?? "-");
        return (booking.Id, Shape(booking));
    }

    public async Task<object> GetAsync(int id)
    {
        var b = await _db.Bookings.AsNoTracking().Include(x => x.Tickets).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy booking.");
        return Shape(b);
    }

    // ============ Nhả giữ chỗ quá hạn ============
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
        _logger.LogInformation("Nhả {Count} booking hết hạn giữ chỗ", ids.Count);
        return ids.Count;
    }

    // ============ Hủy booking + hoàn tiền ============
    public async Task<object> CancelAsync(int id)
    {
        var b = await _db.Bookings
                    .Include(x => x.Tickets).ThenInclude(t => t.Trip)
                    .Include(x => x.Payments)
                    .FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy booking.");
        if (b.Status is not ("Holding" or "Paid"))
            throw new ServiceException(400, "Không thể hủy booking ở trạng thái này.");

        // Có vé đã soát (đã lên xe) thì không cho hủy
        if (b.Tickets.Any(t => t.Status == "CheckedIn"))
            throw new ServiceException(400, "Có vé đã được soát, không thể hủy.");

        var now = DateTime.Now;
        var active = b.Tickets.Where(t => t.Status != "Cancelled").ToList();
        var depart = active.Count > 0 ? active.Min(t => t.Trip.DepartAt) : now;
        var wasHolding = b.Status == "Holding";

        decimal rate = 0, refund = 0;
        if (b.Status == "Paid")
        {
            if (depart <= now) throw new ServiceException(400, "Chuyến đã khởi hành, không thể hủy.");
            var paidPayment = b.Payments.FirstOrDefault(p => p.Status == "Paid");
            var paid = b.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount);
            rate = Rules.RefundRate(depart, now);
            refund = Math.Round(paid * rate, 0);
            if (refund > 0 && paidPayment != null)
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

        _logger.LogInformation("Hủy booking {BookingCode}: hoàn {RefundRate:P0} = {RefundAmount}", b.Code, rate, refund);
        return new { BookingId = b.Id, b.Status, RefundRate = rate, RefundAmount = refund };
    }
    // ============ Đổi vé: đổi ghế, hoặc đổi sang chuyến khác cùng tuyến ============
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
        _logger.LogInformation("Đổi vé {TicketCode}: chuyến {TripId}, ghế {SeatCode}", t.TicketCode, t.TripId, t.SeatCode);
        return new { t.Id, t.TicketCode, t.TripId, t.SeatCode, t.Price, t.Status };
    }

    // ============ Tra cứu vé theo mã ============
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
                       BookingCreatedAt = t.Booking.CreatedAt,
                       Customer = t.Booking.Customer.FullName,
                       t.Booking.ContactPhone,
                       t.Booking.NeedTransfer,
                       t.Booking.TransferAddress,
                       Route = t.Trip.BusRoute.Name,
                       FromStation = t.Trip.BusRoute.FromStation.Name,
                       FromAddress = t.Trip.BusRoute.FromStation.Address,
                       ToStation = t.Trip.BusRoute.ToStation.Name,
                       ToAddress = t.Trip.BusRoute.ToStation.Address,
                       DistanceKm = t.Trip.BusRoute.DistanceKm,
                       DurationMin = t.Trip.BusRoute.DurationMin,
                       t.Trip.DepartAt,
                       t.Trip.ArriveAt,
                       Plate = t.Trip.Vehicle.Plate,
                       VehicleType = t.Trip.Vehicle.VehicleType.Name,
                       DriverName = t.Trip.Driver.FullName,
                       DriverPhone = t.Trip.Driver.Phone
                   })
                   .FirstOrDefaultAsync()
               ?? throw new ServiceException(404, "Không tìm thấy vé.");
    }
    // Soát vé: Active -> CheckedIn. Mỗi vé chỉ soát được một lần.
    public async Task<object> CheckInAsync(string ticketCode)
    {
        const int OpenBeforeHours = 3;   // mở soát vé trước giờ chạy
        const int CloseAfterHours = 6;   // đóng soát vé sau giờ chạy

        var code = (ticketCode ?? "").Trim().ToUpperInvariant();
        var t = await _db.Tickets.AsNoTracking()
                    .Include(x => x.Booking)
                    .Include(x => x.Trip).ThenInclude(tr => tr.BusRoute)
                    .FirstOrDefaultAsync(x => x.TicketCode == code)
                ?? throw new ServiceException(404, "Không tìm thấy vé.");

        if (t.Status == "CheckedIn")
            throw new ServiceException(400, "Vé này đã được soát trước đó.");
        if (t.Status != "Active")
            throw new ServiceException(400, "Vé không còn hiệu lực (đã hủy).");
        if (t.Booking.Status != "Paid")
            throw new ServiceException(400, "Vé chưa thanh toán nên chưa thể lên xe.");
        if (t.Trip.Status == "Cancelled")
            throw new ServiceException(400, "Chuyến xe đã bị hủy.");

        var now = DateTime.Now;
        if (now < t.Trip.DepartAt.AddHours(-OpenBeforeHours))
            throw new ServiceException(400, $"Chưa đến giờ soát vé (mở trước giờ chạy {OpenBeforeHours} giờ).");
        if (now > t.Trip.DepartAt.AddHours(CloseAfterHours))
            throw new ServiceException(400, "Chuyến đã kết thúc, không thể soát vé.");

        // Cập nhật có điều kiện để hai người quét cùng lúc thì chỉ một người thành công
        var updated = await _db.Tickets
            .Where(x => x.Id == t.Id && x.Status == "Active")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "CheckedIn"));
        if (updated == 0)
            throw new ServiceException(400, "Vé này đã được soát trước đó.");

        return new
        {
            t.TicketCode,
            t.SeatCode,
            t.PassengerName,
            Route = t.Trip.BusRoute.Name,
            t.Trip.DepartAt
        };
    }


    public async Task<object> GetByCodeAsync(string code)
    {
        var c = code.Trim().ToUpperInvariant();
        return await _db.Bookings.AsNoTracking()
                   .Where(b => b.Code == c)
                   .Select(b => new
                   {
                       b.Id,
                       b.Code,
                       b.Status,
                       b.TotalAmount,
                       b.DiscountAmount,
                       b.HoldExpiresAt,
                       b.CreatedAt,
                       Customer = b.Customer.FullName,
                       b.ContactPhone,
                       b.NeedTransfer,
                       b.TransferAddress,
                       Tickets = b.Tickets.Select(t => new
                       {
                           t.Id,
                           t.TicketCode,
                           t.SeatCode,
                           t.PassengerName,
                           t.Price,
                           t.Status,
                           Route = t.Trip.BusRoute.Name,
                           FromStation = t.Trip.BusRoute.FromStation.Name,
                           ToStation = t.Trip.BusRoute.ToStation.Name,
                           t.Trip.DepartAt,
                           Plate = t.Trip.Vehicle.Plate
                       })
                   })
                   .FirstOrDefaultAsync()
               ?? throw new ServiceException(404, "Không tìm thấy mã đặt vé.");
    }

    // Xem trước số tiền hoàn trước khi khách bấm hủy
    public async Task<object> RefundPreviewAsync(int id)
    {
        var b = await _db.Bookings.AsNoTracking()
                    .Include(x => x.Tickets).ThenInclude(t => t.Trip)
                    .Include(x => x.Payments)
                    .FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy booking.");

        var now = DateTime.Now;
        var active = b.Tickets.Where(t => t.Status == "Active").ToList();
        if (b.Status is not ("Holding" or "Paid") || active.Count == 0)
            return new { CanCancel = false, Reason = "Booking không thể hủy ở trạng thái này.", RefundRate = 0m, RefundAmount = 0m };

        var depart = active.Min(t => t.Trip.DepartAt);
        if (b.Status == "Paid" && depart <= now)
            return new { CanCancel = false, Reason = "Chuyến đã khởi hành.", RefundRate = 0m, RefundAmount = 0m };

        decimal rate = 0, refund = 0;
        if (b.Status == "Paid")
        {
            var paid = b.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount);
            rate = Rules.RefundRate(depart, now);
            refund = Math.Round(paid * rate, 0);
        }
        return new { CanCancel = true, Reason = (string?)null, RefundRate = rate, RefundAmount = refund };
    }

    // Danh sách đặt vé cho quản trị: lọc, sắp xếp, phân trang
    public async Task<object> ListAsync(string? status, string? keyword, DateTime? fromDate, DateTime? toDate,
                                        string? sortBy, bool desc, int page, int pageSize)
    {
        await ReleaseExpiredAsync();   // để trạng thái hết hạn hiển thị đúng
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Bookings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(b => b.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(b => b.Code.Contains(k) || b.Customer.FullName.Contains(k)
                          || (b.Customer.Phone != null && b.Customer.Phone.Contains(k))
                          || (b.ContactPhone != null && b.ContactPhone.Contains(k)));
        }
        if (fromDate.HasValue)
        {
            var f = fromDate.Value.Date;
            q = q.Where(b => b.CreatedAt >= f);
        }
        if (toDate.HasValue)
        {
            var t = toDate.Value.Date.AddDays(1);
            q = q.Where(b => b.CreatedAt < t);
        }

        IQueryable<Booking> ordered = (sortBy?.ToLowerInvariant(), desc) switch
        {
            ("total", false) => q.OrderBy(b => b.TotalAmount),
            ("total", true) => q.OrderByDescending(b => b.TotalAmount),
            ("status", false) => q.OrderBy(b => b.Status),
            ("status", true) => q.OrderByDescending(b => b.Status),
            (_, false) => q.OrderBy(b => b.CreatedAt),
            _ => q.OrderByDescending(b => b.CreatedAt)
        };

        var total = await ordered.CountAsync();
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.Code,
                b.Status,
                b.TotalAmount,
                b.DiscountAmount,
                b.CreatedAt,
                b.HoldExpiresAt,
                Customer = b.Customer.FullName,
                Phone = b.ContactPhone ?? b.Customer.Phone,
                b.NeedTransfer,
                b.TransferAddress,
                Seats = b.Tickets.Count(t => t.Status != "Cancelled")
            })
            .ToListAsync();
        return new { total, page, pageSize, items };
    }

    // Lấy hoặc tạo hồ sơ Customer cho tài khoản đang đăng nhập
    public async Task<int> GetOrCreateCustomerIdAsync(string userId, string? email)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.UserId == userId);
        if (c != null) return c.Id;

        if (!string.IsNullOrWhiteSpace(email))
        {
            c = await _db.Customers.FirstOrDefaultAsync(x => x.UserId == null && x.Email == email);
            if (c != null)
            {
                c.UserId = userId;
                await _db.SaveChangesAsync();
                return c.Id;
            }
        }
        c = new Customer { UserId = userId, FullName = email ?? "Khách hàng", Email = email };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return c.Id;
    }

    public Task<bool> IsOwnerAsync(int bookingId, string userId) =>
        _db.Bookings.AnyAsync(b => b.Id == bookingId && b.Customer.UserId == userId);

    public Task<bool> IsTicketOwnerAsync(int ticketId, string userId) =>
        _db.Tickets.AnyAsync(t => t.Id == ticketId && t.Booking.Customer.UserId == userId);

    // "Vé của tôi"
    public async Task<object> MineAsync(string userId, int page, int pageSize, string? status = null)
    {
        await ReleaseExpiredAsync();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var filtered = _db.Bookings.AsNoTracking().Where(b => b.Customer.UserId == userId);
        if (!string.IsNullOrWhiteSpace(status)) filtered = filtered.Where(b => b.Status == status);
        var q = filtered.OrderByDescending(b => b.CreatedAt);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.Code,
                b.Status,
                b.TotalAmount,
                b.DiscountAmount,
                b.CreatedAt,
                b.HoldExpiresAt,
                b.ContactPhone,
                b.NeedTransfer,
                b.TransferAddress,
                Tickets = b.Tickets.Select(t => new
                {
                    t.Id,
                    t.TicketCode,
                    t.SeatCode,
                    t.PassengerName,
                    t.Price,
                    t.Status,
                    t.TripId,
                    t.Trip.BusRouteId,
                    Route = t.Trip.BusRoute.Name,
                    t.Trip.DepartAt
                })
            })
            .ToListAsync();
        return new { total, page, pageSize, items };
    }

    // ============ Chi tiết đặt vé của chính khách (có thanh toán) ============
    public async Task<BookingDetailDto> MineDetailAsync(string userId, string code)
    {
        await ReleaseExpiredAsync();
        var c = code.Trim().ToUpperInvariant();
        return await DetailQuery(_db.Bookings.AsNoTracking().Where(b => b.Code == c && b.Customer.UserId == userId))
                   .FirstOrDefaultAsync()
               ?? throw new ServiceException(404, "Không tìm thấy đặt vé.");
    }

    // ============ Tra cứu không cần đăng nhập: mã đặt vé + SĐT hoặc email đã dùng khi đặt ============
    public async Task<BookingDetailDto> LookupAsync(string code, string contact)
    {
        await ReleaseExpiredAsync();
        var c = code.Trim().ToUpperInvariant();
        var k = contact.Trim();
        var kLower = k.ToLowerInvariant();

        // Sai mã hay sai thông tin liên hệ đều trả cùng một lỗi, không cho đoán mã đặt vé
        return await DetailQuery(_db.Bookings.AsNoTracking().Where(b => b.Code == c
                       && (b.Customer.Phone == k
                           || b.ContactPhone == k
                           || (b.Customer.Email != null && b.Customer.Email.ToLower() == kLower))))
                   .FirstOrDefaultAsync()
               ?? throw new ServiceException(404, "Không tìm thấy đặt vé khớp với mã và thông tin liên hệ.");
    }

    private static IQueryable<BookingDetailDto> DetailQuery(IQueryable<Booking> q) =>
        q.Select(b => new BookingDetailDto
        {
            Id = b.Id,
            Code = b.Code,
            Status = b.Status,
            TotalAmount = b.TotalAmount,
            DiscountAmount = b.DiscountAmount,
            HoldExpiresAt = b.HoldExpiresAt,
            CreatedAt = b.CreatedAt,
            Customer = b.Customer.FullName,
            ContactPhone = b.ContactPhone,
            NeedTransfer = b.NeedTransfer,
            TransferAddress = b.TransferAddress,
            Tickets = b.Tickets.Select(t => new TicketDetailDto
            {
                Id = t.Id,
                TicketCode = t.TicketCode,
                SeatCode = t.SeatCode,
                PassengerName = t.PassengerName,
                Price = t.Price,
                Status = t.Status,
                Route = t.Trip.BusRoute.Name,
                FromStation = t.Trip.BusRoute.FromStation.Name,
                ToStation = t.Trip.BusRoute.ToStation.Name,
                DepartAt = t.Trip.DepartAt,
                ArriveAt = t.Trip.ArriveAt,
                Plate = t.Trip.Vehicle.Plate,
                VehicleType = t.Trip.Vehicle.VehicleType.Name,
                FromAddress = t.Trip.BusRoute.FromStation.Address,
                ToAddress = t.Trip.BusRoute.ToStation.Address,
                DistanceKm = t.Trip.BusRoute.DistanceKm,
                DurationMin = t.Trip.BusRoute.DurationMin,
                DriverName = t.Trip.Driver.FullName,
                DriverPhone = t.Trip.Driver.Phone
            }).ToList(),
            Payments = b.Payments.OrderBy(p => p.Id).Select(p => new PaymentDetailDto
            {
                Id = p.Id,
                Amount = p.Amount,
                Method = p.Method,
                Status = p.Status,
                PaidAt = p.PaidAt
            }).ToList()
        });
}
