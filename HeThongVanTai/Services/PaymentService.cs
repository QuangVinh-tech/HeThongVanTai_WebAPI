
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace HeThongVanTai.Services;

public class PaymentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext db,
        ILogger<PaymentService> logger)
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
        p.SePayTransactionId,
        p.BankReference,
        TransferContent = bookingCode
    };

    private static void Complete(Booking booking, Payment payment)
    {
        payment.Status = "Paid";
        payment.PaidAt = DateTime.Now;

        booking.Status = "Paid";
        booking.HoldExpiresAt = null;
    }

    public async Task<object> CreateAsync(PayDto dto)
    {
        var method = new[] { "Cash", "BankTransfer" }
            .FirstOrDefault(m => string.Equals(
                m, dto.Method, StringComparison.OrdinalIgnoreCase))
            ?? throw new ServiceException(
                400, "Phương thức phải là Cash hoặc BankTransfer.");

        var booking = await _db.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId)
            ?? throw new ServiceException(404, "Không tìm thấy booking.");

        if (booking.Status != "Holding")
            throw new ServiceException(
                400, "Booking không ở trạng thái chờ thanh toán.");

        if (booking.HoldExpiresAt is null ||
            booking.HoldExpiresAt <= DateTime.Now)
        {
            throw new ServiceException(
                400, "Booking đã hết hạn giữ chỗ.");
        }

        if (booking.Payments.Any(p =>
                p.Status == "Paid" || p.Status == "Pending"))
        {
            throw new ServiceException(
                409, "Booking đã có giao dịch thanh toán.");
        }

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalAmount,
            Method = method,
            Status = "Pending"
        };

        // Tiền mặt được xác nhận tại quầy theo luồng nghiệp vụ hiện có.
        if (method == "Cash")
            Complete(booking, payment);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Thanh toán booking {BookingCode}: {Method}, {Amount}, {Status}",
            booking.Code, method, payment.Amount, payment.Status);

        return Shape(payment, booking.Code);
    }

    public async Task<object> ConfirmAsync(int paymentId)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new ServiceException(404, "Không tìm thấy giao dịch.");

        if (payment.Status == "Paid")
            throw new ServiceException(409, "Giao dịch đã được xác nhận.");

        if (payment.Status != "Pending")
            throw new ServiceException(
                400, "Giao dịch không ở trạng thái chờ xác nhận.");

        var booking = payment.Booking;

        if (booking.Status != "Holding" ||
            booking.HoldExpiresAt is null ||
            booking.HoldExpiresAt <= DateTime.Now)
        {
            throw new ServiceException(
                400, "Booking đã hết hạn giữ chỗ hoặc đã bị hủy.");
        }

        Complete(booking, payment);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Xác nhận thanh toán booking {BookingCode}, {Amount}",
            booking.Code, payment.Amount);

        return Shape(payment, booking.Code);
    }

    public async Task<List<object>> ByBookingAsync(int bookingId)
    {
        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Booking)
            .Where(p => p.BookingId == bookingId)
            .OrderBy(p => p.Id)
            .ToListAsync();

        return payments
            .Select(p => Shape(p, p.Booking.Code))
            .ToList();
    }

    private static readonly Regex BookingCodeRegex = new(
        @"BK\d{6}[A-Z0-9]{5}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Xử lý webhook chuyển khoản từ SePay.
    /// </summary>
    public async Task<string> ConfirmByTransferAsync(
        string? content,
        decimal amount,
        string? sePayTransactionId,
        string? bankReference)
    {
        if (string.IsNullOrWhiteSpace(sePayTransactionId))
            return "manual: thiếu mã giao dịch SePay";

        if (amount <= 0)
            return "manual: số tiền giao dịch không hợp lệ";

        // Chặn webhook đã được xử lý trước đó.
        var existingPayment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.SePayTransactionId == sePayTransactionId);

        if (existingPayment != null)
        {
            _logger.LogInformation(
                "Webhook SePay trùng: transaction {TransactionId}",
                sePayTransactionId);

            return "ok: giao dịch đã được xử lý trước đó";
        }

        // Tìm mã booking trong nội dung chuyển khoản.
        var match = BookingCodeRegex.Match(content ?? "");

        if (!match.Success)
            return "ignored: không có mã đặt vé trong nội dung";

        var bookingCode = match.Value.ToUpperInvariant();

        var booking = await _db.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Code == bookingCode);

        if (booking == null)
            return "ignored: không tìm thấy đặt vé";

        // Không tự động xác nhận nếu booking đã được thanh toán.
        if (booking.Status == "Paid" ||
            booking.Payments.Any(p => p.Status == "Paid"))
        {
            _logger.LogWarning(
                "Nhận giao dịch {TransactionId} cho booking {BookingCode} đã thanh toán. Cần đối soát thủ công.",
                sePayTransactionId, booking.Code);

            return "manual: đặt vé đã thanh toán, cần đối soát giao dịch";
        }

        if (booking.Status != "Holding" ||
            booking.HoldExpiresAt is null ||
            booking.HoldExpiresAt <= DateTime.Now)
        {
            _logger.LogWarning(
                "Nhận giao dịch {TransactionId}, số tiền {Amount} cho booking {BookingCode} đã hết hạn hoặc bị hủy.",
                sePayTransactionId, amount, booking.Code);

            return "manual: đặt vé hết hạn hoặc đã hủy";
        }

        // Không tự động xác nhận nếu số tiền không khớp.
        // Giao dịch thừa hoặc thiếu đều cần đối soát thủ công.
        if (amount != booking.TotalAmount)
        {
            _logger.LogWarning(
                "Sai số tiền booking {BookingCode}: nhận {Received}, yêu cầu {Expected}, transaction {TransactionId}.",
                booking.Code, amount, booking.TotalAmount, sePayTransactionId);

            return amount < booking.TotalAmount
                ? "manual: số tiền chưa đủ"
                : "manual: số tiền vượt quá số tiền cần thanh toán";
        }

        // Dùng payment BankTransfer đang chờ nếu đã tạo trước đó.
        var payment = booking.Payments.FirstOrDefault(p =>
            p.Method == "BankTransfer" &&
            p.Status == "Pending");

        if (payment == null)
        {
            payment = new Payment
            {
                BookingId = booking.Id,
                Amount = booking.TotalAmount,
                Method = "BankTransfer",
                Status = "Pending"
            };

            _db.Payments.Add(payment);
        }

        payment.SePayTransactionId = sePayTransactionId;
        payment.BankReference = bankReference;

        Complete(booking, payment);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Có thể webhook đồng thời đã lưu cùng mã giao dịch.
            _logger.LogWarning(
                ex,
                "Không lưu được giao dịch SePay {TransactionId}; kiểm tra giao dịch trùng hoặc lỗi database.",
                sePayTransactionId);

            _db.Entry(booking).State = EntityState.Unchanged;

            if (payment.Id != 0)
                _db.Entry(payment).State = EntityState.Unchanged;
            else
                _db.Entry(payment).State = EntityState.Detached;

            var duplicate = await _db.Payments
                .AsNoTracking()
                .AnyAsync(p =>
                    p.SePayTransactionId == sePayTransactionId);

            if (duplicate)
                return "ok: giao dịch đã được xử lý trước đó";

            throw;
        }

        _logger.LogInformation(
            "SePay xác nhận thành công: booking {BookingCode}, amount {Amount}, transaction {TransactionId}, bank ref {BankReference}",
            booking.Code, amount, sePayTransactionId, bankReference);

        return "ok: đã xác nhận";
    }
}
