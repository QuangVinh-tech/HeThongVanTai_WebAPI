using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

/// <summary>
/// Yêu cầu đổi ghế: khách gửi, Admin xác nhận (ghế mới phải còn trống) hoặc từ chối.
/// Khi xác nhận, việc đổi ghế thật dùng lại BookingService.ChangeTicketAsync nên giữ nguyên mọi quy tắc cũ
/// (vé còn hiệu lực, trước giờ đi ít nhất 2 giờ, ghế thuộc xe của chuyến, ghế chưa có người đặt).
/// </summary>
public class SeatChangeService
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const int MinHoursBeforeDeparture = 2;

    private readonly AppDbContext _db;
    private readonly BookingService _bookings;
    private readonly ILogger<SeatChangeService> _logger;

    public SeatChangeService(AppDbContext db, BookingService bookings, ILogger<SeatChangeService> logger)
    {
        _db = db;
        _bookings = bookings;
        _logger = logger;
    }

    // ---------------- Khách ----------------

    public async Task<SeatChangeItemDto> CreateAsync(CreateSeatChangeDto dto, string userId)
    {
        var t = await _db.Tickets.Include(x => x.Booking).ThenInclude(b => b.Customer).Include(x => x.Trip)
                    .FirstOrDefaultAsync(x => x.Id == dto.TicketId)
                ?? throw new ServiceException(404, "Không tìm thấy vé.");
        if (t.Booking.Customer.UserId != userId)
            throw new ServiceException(403, "Bạn không có quyền với vé này.");
        if (t.Status != "Active")
            throw new ServiceException(400, "Chỉ đổi được vé còn hiệu lực.");
        if (t.Booking.Status is not ("Holding" or "Paid"))
            throw new ServiceException(400, "Đặt vé này không cho phép đổi ghế.");

        var now = DateTime.Now;
        if (t.Trip.Status != "Open" || t.Trip.DepartAt - now < TimeSpan.FromHours(MinHoursBeforeDeparture))
            throw new ServiceException(400, $"Chỉ gửi được yêu cầu trước giờ khởi hành ít nhất {MinHoursBeforeDeparture} giờ.");

        var seat = (dto.NewSeatCode ?? "").Trim().ToUpperInvariant();
        if (seat.Length == 0) throw new ServiceException(400, "Vui lòng chọn ghế mới.");
        if (seat == t.SeatCode) throw new ServiceException(400, "Ghế mới trùng ghế hiện tại.");

        var vehicleTypeId = await _db.Vehicles.Where(v => v.Id == t.Trip.VehicleId).Select(v => v.VehicleTypeId).FirstAsync();
        if (!await _db.SeatTemplates.AnyAsync(s => s.VehicleTypeId == vehicleTypeId && s.SeatCode == seat))
            throw new ServiceException(400, "Ghế mới không thuộc loại xe của chuyến.");

        await _bookings.ReleaseExpiredAsync(t.TripId);
        if (await IsSeatTakenAsync(t.TripId, seat, t.Id))
            throw new ServiceException(409, "Ghế mới đã có người đặt.");

        if (await _db.SeatChangeRequests.AnyAsync(r => r.TicketId == t.Id && r.Status == Pending))
            throw new ServiceException(409, "Vé này đang có yêu cầu đổi ghế chờ xác nhận.");

        var req = new SeatChangeRequest
        {
            TicketId = t.Id,
            TripId = t.TripId,
            FromSeat = t.SeatCode,
            ToSeat = seat,
            Reason = Clean(dto.Reason),
            Status = Pending,
            RequestedByUserId = userId,
            RequestedAt = now
        };
        _db.SeatChangeRequests.Add(req);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Yêu cầu đổi ghế {Id}: vé {TicketCode} từ {From} sang {To}", req.Id, t.TicketCode, req.FromSeat, req.ToSeat);
        return await GetAsync(req.Id);
    }

    public async Task<List<SeatChangeItemDto>> MineAsync(string userId) =>
        await Project(_db.SeatChangeRequests.AsNoTracking()
                .Where(r => r.Ticket.Booking.Customer.UserId == userId)
                .OrderByDescending(r => r.RequestedAt).Take(100))
            .ToListAsync();

    public async Task<SeatChangeItemDto> CancelAsync(int id, string userId)
    {
        var r = await _db.SeatChangeRequests.Include(x => x.Ticket).ThenInclude(t => t.Booking).ThenInclude(b => b.Customer)
                    .FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy yêu cầu đổi ghế.");
        if (r.Ticket.Booking.Customer.UserId != userId)
            throw new ServiceException(403, "Bạn không có quyền với yêu cầu này.");
        if (r.Status != Pending)
            throw new ServiceException(409, "Chỉ hủy được yêu cầu đang chờ xác nhận.");

        r.Status = Cancelled;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Khách hủy yêu cầu đổi ghế {Id}", id);
        return await GetAsync(id);
    }

    // ---------------- Admin ----------------

    public async Task<SeatChangeListDto> ListAsync(string? status, int page, int pageSize)
    {
        await _bookings.ReleaseExpiredAsync();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var q = _db.SeatChangeRequests.AsNoTracking().AsQueryable();
        var filter = status is Pending or Approved or Rejected or Cancelled ? status : null;
        if (filter != null) q = q.Where(r => r.Status == filter);

        var total = await q.CountAsync();
        var ordered = filter == Pending ? q.OrderBy(r => r.RequestedAt) : q.OrderByDescending(r => r.RequestedAt);
        var items = await Project(ordered.Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync();

        // Với yêu cầu đang chờ: ghế mới còn trống không?
        var pendingItems = items.Where(i => i.Status == Pending).ToList();
        if (pendingItems.Count > 0)
        {
            var tripIds = pendingItems.Select(i => i.TripId).Distinct().ToList();
            var taken = await _db.Tickets.AsNoTracking()
                .Where(k => tripIds.Contains(k.TripId) && k.Status != "Cancelled")
                .Select(k => new { k.Id, k.TripId, k.SeatCode })
                .ToListAsync();
            foreach (var i in pendingItems)
                i.ToSeatAvailable = !taken.Any(k => k.TripId == i.TripId && k.SeatCode == i.ToSeat && k.Id != i.TicketId);
        }

        return new SeatChangeListDto
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            PendingCount = await _db.SeatChangeRequests.CountAsync(r => r.Status == Pending),
            Items = items
        };
    }

    /// <summary>Xác nhận: đổi ghế thật. Ghế mới đã có người đặt thì báo lỗi 409 và yêu cầu vẫn ở trạng thái chờ để Admin từ chối.</summary>
    public async Task<SeatChangeItemDto> ApproveAsync(int id, string? reviewer, string? note)
    {
        var r = await _db.SeatChangeRequests.Include(x => x.Ticket).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy yêu cầu đổi ghế.");
        if (r.Status != Pending)
            throw new ServiceException(409, "Yêu cầu này đã được xử lý.");
        if (r.Ticket.SeatCode != r.FromSeat)
            throw new ServiceException(409, "Vé đã đổi sang ghế khác sau khi khách gửi yêu cầu. Hãy từ chối yêu cầu này.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _bookings.ChangeTicketAsync(r.TicketId, new ChangeTicketDto { NewSeatCode = r.ToSeat });
        r.Status = Approved;
        r.ReviewedBy = reviewer;
        r.ReviewedAt = DateTime.Now;
        r.ReviewNote = Clean(note);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _logger.LogInformation("Admin {Reviewer} xác nhận đổi ghế {Id}: {From} sang {To}", LogText.Clean(reviewer), id, r.FromSeat, r.ToSeat);
        return await GetAsync(id);
    }

    public async Task<SeatChangeItemDto> RejectAsync(int id, string? reviewer, string? note)
    {
        var reason = Clean(note);
        if (reason == null) throw new ServiceException(400, "Vui lòng nhập lý do từ chối.");

        var r = await _db.SeatChangeRequests.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ServiceException(404, "Không tìm thấy yêu cầu đổi ghế.");
        if (r.Status != Pending)
            throw new ServiceException(409, "Yêu cầu này đã được xử lý.");

        r.Status = Rejected;
        r.ReviewedBy = reviewer;
        r.ReviewedAt = DateTime.Now;
        r.ReviewNote = reason;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin {Reviewer} từ chối đổi ghế {Id}", LogText.Clean(reviewer), id);
        return await GetAsync(id);
    }

    // ---------------- Dùng chung ----------------

    private Task<bool> IsSeatTakenAsync(int tripId, string seat, int exceptTicketId) =>
        _db.Tickets.AnyAsync(k => k.TripId == tripId && k.SeatCode == seat && k.Status != "Cancelled" && k.Id != exceptTicketId);

    private async Task<SeatChangeItemDto> GetAsync(int id) =>
        await Project(_db.SeatChangeRequests.AsNoTracking().Where(r => r.Id == id)).FirstAsync();

    private static IQueryable<SeatChangeItemDto> Project(IQueryable<SeatChangeRequest> q) =>
        q.Select(r => new SeatChangeItemDto
        {
            Id = r.Id,
            TicketId = r.TicketId,
            TripId = r.TripId,
            TicketCode = r.Ticket.TicketCode,
            BookingCode = r.Ticket.Booking.Code,
            PassengerName = r.Ticket.PassengerName,
            CustomerName = r.Ticket.Booking.Customer.FullName,
            ContactPhone = r.Ticket.Booking.ContactPhone,
            Route = r.Ticket.Trip.BusRoute.Name,
            DepartAt = r.Ticket.Trip.DepartAt,
            CurrentSeat = r.Ticket.SeatCode,
            FromSeat = r.FromSeat,
            ToSeat = r.ToSeat,
            Status = r.Status,
            Reason = r.Reason,
            RequestedAt = r.RequestedAt,
            ReviewedBy = r.ReviewedBy,
            ReviewedAt = r.ReviewedAt,
            ReviewNote = r.ReviewNote
        });

    private static string? Clean(string? s)
    {
        var v = (s ?? "").Trim();
        return v.Length == 0 ? null : (v.Length > 300 ? v[..300] : v);
    }
}
