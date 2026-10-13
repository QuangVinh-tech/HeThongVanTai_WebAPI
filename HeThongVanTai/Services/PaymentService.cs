using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

public class PaymentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext db, ILogger<PaymentService> logger)
    {
        _db = db;
        _logger = logger;
    }

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
        _logger.LogInformation("Thanh toán: booking {BookingCode}, {Method}, {Amount}, trạng thái {Status}",
            b.Code, method, payment.Amount, payment.Status);
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
        _logger.LogInformation("Xác nhận chuyển khoản: booking {BookingCode}, {Amount}", p.Booking.Code, p.Amount);
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

    // Mã đặt vé có dạng BK + yymmdd + 5 ký tự (xem Rules.NewBookingCode). Ngân hàng có thể thêm chữ phía trước/sau.
    private static readonly Regex BookingCodeRegex =
        new(@"BK\d{6}[A-Z0-9]{5}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Webhook ngân hàng: tiền vào tài khoản, nội dung chứa mã đặt vé thì tự xác nhận thanh toán.
    // Luôn trả kết quả (không ném lỗi) để webhook trả 200, tránh bị gửi lại liên tục.
    public async Task<string> ConfirmByTransferAsync(string? content, decimal amount, string? bankRef)
    {
        var m = BookingCodeRegex.Match(content ?? "");
        if (!m.Success) return "ignored: không có mã đặt vé trong nội dung";

        var code = m.Value.ToUpperInvariant();
        var b = await _db.Bookings.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Code == code);
        if (b == null) return "ignored: không tìm thấy đặt vé";

        if (b.Status == "Paid" || b.Payments.Any(p => p.Status == "Paid"))
            return "ok: đặt vé đã thanh toán trước đó";   // webhook gửi lặp thì bỏ qua

        // Tiền đã vào nhưng không tự xác nhận được: ghi log mức Warning để kế toán xử lý tay / hoàn tiền
        if (b.Status != "Holding" || b.HoldExpiresAt is null || b.HoldExpiresAt <= DateTime.Now)
        {
            _logger.LogWarning("Nhận chuyển khoản {Amount} cho đặt vé {BookingCode} nhưng đặt vé đã hết hạn/hủy (ref {Ref}), cần xử lý thủ công",
                amount, b.Code, LogText.Clean(bankRef));
            return "manual: đặt vé hết hạn hoặc đã hủy";
        }
        if (amount < b.TotalAmount)
        {
            _logger.LogWarning("Nhận chuyển khoản {Amount} cho đặt vé {BookingCode} nhưng thiếu so với {Total} (ref {Ref}), cần xử lý thủ công",
                amount, b.Code, b.TotalAmount, LogText.Clean(bankRef));
            return "manual: số tiền chưa đủ";
        }

        var payment = b.Payments.FirstOrDefault(p => p.Status == "Pending" && p.Method == "BankTransfer");
        if (payment == null)
        {
            // Khách quét QR chuyển luôn, chưa bấm nút trên web
            payment = new Payment { BookingId = b.Id, Amount = b.TotalAmount, Method = "BankTransfer", Status = "Pending" };
            _db.Payments.Add(payment);
        }
        Complete(b, payment);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Tự động xác nhận chuyển khoản: booking {BookingCode}, {Amount}", b.Code, payment.Amount);
        return "ok: đã xác nhận";
    }
}