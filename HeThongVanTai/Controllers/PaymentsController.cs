using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _svc;
    private readonly BookingService _bookings;
    private readonly IConfiguration _cfg;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(PaymentService svc, BookingService bookings, IConfiguration cfg, ILogger<PaymentsController> logger)
    {
        _svc = svc;
        _bookings = bookings;
        _cfg = cfg;
        _logger = logger;
    }

    private bool IsStaff => User.IsInRole("Admin") || User.IsInRole("Seller");

    private IActionResult Deny() =>
        StatusCode(403, new { message = "Bạn không có quyền với đặt vé này." });

    // Cash: Paid ngay (chỉ nhân viên quầy). BankTransfer: Pending, chờ xác nhận (khách dùng được)
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(PayDto dto)
    {
        if (!IsStaff)
        {
            if (string.Equals(dto.Method, "Cash", StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, new { message = "Tiền mặt chỉ thu tại quầy, vui lòng chọn chuyển khoản." });

            var uid = UserClaims.UserId(User);
            if (uid == null || !await _bookings.IsOwnerAsync(dto.BookingId, uid))
                return Deny();
        }
        return StatusCode(201, await _svc.CreateAsync(dto));
    }

    // Nhân viên / kế toán xác nhận đã nhận tiền chuyển khoản
    [Authorize(Roles = "Admin,Seller,Accountant")]
    [HttpPut("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id) => Ok(await _svc.ConfirmAsync(id));

    [Authorize]
    [HttpGet("booking/{bookingId:int}")]
    public async Task<IActionResult> ByBooking(int bookingId)
    {
        var uid = UserClaims.UserId(User);
        var ok = IsStaff || User.IsInRole("Accountant")
                 || (uid != null && await _bookings.IsOwnerAsync(bookingId, uid));
        if (!ok) return Deny();
        return Ok(await _svc.ByBookingAsync(bookingId));
    }

    // Webhook SePay: gọi khi có tiền vào tài khoản. Không có JWT nên xác thực bằng API key trong header
    // "Authorization: Apikey <khóa>" (khóa đặt ở Payment:WebhookApiKey, không để trong git).
    [AllowAnonymous]
    [HttpPost("webhook/sepay")]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookDto dto)
    {
        var expected = _cfg["Payment:WebhookApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(503, new { success = false, message = "Chưa cấu hình Payment:WebhookApiKey." });

        var header = Request.Headers.Authorization.ToString();
        const string prefix = "Apikey ";
        var given = header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? header[prefix.Length..].Trim() : "";
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
        if (!ok)
        {
            _logger.LogWarning("Webhook thanh toán bị từ chối: sai API key");
            return Unauthorized(new { success = false });
        }

        // Chỉ xử lý tiền vào
        if (!string.Equals(dto.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            return Ok(new { success = true, result = "ignored: không phải tiền vào" });

        var result = await _svc.ConfirmByTransferAsync(dto.Content, dto.TransferAmount, dto.ReferenceCode ?? dto.Id.ToString());
        return Ok(new { success = true, result });
    }
}