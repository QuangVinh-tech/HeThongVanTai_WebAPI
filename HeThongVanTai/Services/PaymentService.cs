using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

public class PaymentService
{
    private readonly AppDbContext _db;
    public PaymentService(AppDbContext db) => _db = db;

    private static object Shape(Payment p, string bookingCode) => new
    {
        p.Id,
        p.BookingId,
        BookingCode = bookingCode,
        p.Amount,
        p.Method,
        p.Status,
        p.PaidAt,
        TransferContent = bookingCode   // khách chuyển khoản ghi nội dung = mã booking
    };

    private static void Complete(Booking b, Payment p)
    {
        p.Status = "Paid";
        p.PaidAt = DateTime.Now;
        b.Status = "Paid";
        b.HoldExpiresAt = null;
    }

    // Cash: Paid ngay (thu tại quầy). BankTransfer: Pending, chờ xác nhận.
    public async Task<object> CreateAsync(PayDto dto)
    {
        var method = new[] { "Cash", "BankTransfer" }
            .FirstOrDefault(m => string.Equals(m, dto.Method, StringComparison.OrdinalIgnoreCase))
            ?? throw new ServiceException(400, "Phương thức phải là Cash hoặc BankTransfer.");

        var b = await _db.Bookings.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == dto.BookingId)
                ?? throw new ServiceException(404, "Không tìm thấy booking.");

        if (b.Status != "Holding")
            throw new ServiceException(400, "Booking không ở trạng thái chờ thanh toán.");
        if (b.HoldExpiresAt is null || b.HoldExpiresAt <= DateTime.Now)
            throw new ServiceException(400, "Booking đã hết hạn giữ chỗ.");
        if (b.Payments.Any(p => p.Status == "Paid" || p.Status == "Pending"))
            throw new ServiceException(409, "Booking đã có giao dịch thanh toán.");

        var payment = new Payment
        {
            BookingId = b.Id,
            Amount = b.TotalAmount,     // số tiền do server lấy từ booking, client không được gửi
            Method = method,
            Status = "Pending"
        };
        if (method == "Cash") Complete(b, payment);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return Shape(payment, b.Code);
    }

    // Kế toán xác nhận đã nhận chuyển khoản
    public async Task<object> ConfirmAsync(int paymentId)
    {
        var p = await _db.Payments.Include(x => x.Booking).FirstOrDefaultAsync(x => x.Id == paymentId)
                ?? throw new ServiceException(404, "Không tìm thấy giao dịch.");

        if (p.Status == "Paid") throw new ServiceException(409, "Giao dịch đã được xác nhận.");
        if (p.Status != "Pending") throw new ServiceException(400, "Giao dịch không ở trạng thái chờ xác nhận.");
        if (p.Booking.Status != "Holding" || p.Booking.HoldExpiresAt is null || p.Booking.HoldExpiresAt <= DateTime.Now)
            throw new ServiceException(400, "Booking đã hết hạn giữ chỗ hoặc đã bị hủy.");

        Complete(p.Booking, p);
        await _db.SaveChangesAsync();
        return Shape(p, p.Booking.Code);
    }

    public async Task<List<object>> ByBookingAsync(int bookingId)
    {
        var list = await _db.Payments.AsNoTracking()
            .Include(p => p.Booking)
            .Where(p => p.BookingId == bookingId)
            .OrderBy(p => p.Id)
            .ToListAsync();
        return list.Select(p => Shape(p, p.Booking.Code)).ToList();
    }
}