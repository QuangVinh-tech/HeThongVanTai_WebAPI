
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _svc;
    private readonly BookingService _bookings;
    private readonly IConfiguration _cfg;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        PaymentService svc,
        BookingService bookings,
        IConfiguration cfg,
        ILogger<PaymentsController> logger)
    {
        _svc = svc;
        _bookings = bookings;
        _cfg = cfg;
        _logger = logger;
    }

    private bool IsStaff =>
        User.IsInRole("Admin") || User.IsInRole("Seller");

    private IActionResult Deny() =>
        StatusCode(403, new
        {
            success = false,
            message = "Bạn không có quyền thực hiện thao tác với đặt vé này."
        });

    // So sánh API key, tránh so sánh trực tiếp chuỗi bí mật.
    private bool IsValidWebhookApiKey()
    {
        var expected = _cfg["Payment:WebhookApiKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            _logger.LogError(
                "Chưa cấu hình Payment:WebhookApiKey.");

            return false;
        }

        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Apikey ";

        if (!authorization.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var given = authorization[prefix.Length..].Trim();

        if (string.IsNullOrWhiteSpace(given))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var givenBytes = Encoding.UTF8.GetBytes(given);

        return expectedBytes.Length == givenBytes.Length
            && CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                givenBytes);
    }

    // Tạo thanh toán.
    // Khách: chỉ tạo thanh toán cho đặt vé của chính mình.
    // Nhân viên: được tạo thanh toán theo quyền hệ thống.
    [Authorize(Roles = AppRoles.AdminSellerCustomer)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PayDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Dữ liệu thanh toán không hợp lệ."
            });
        }

        if (dto.BookingId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mã đặt vé không hợp lệ."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Method))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng chọn phương thức thanh toán."
            });
        }

        if (!IsStaff)
        {
            if (string.Equals(
                    dto.Method,
                    "Cash",
                    StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "Tiền mặt chỉ được thu tại quầy. Vui lòng chọn chuyển khoản."
                });
            }

            var uid = UserClaims.UserId(User);

            if (uid == null ||
                !await _bookings.IsOwnerAsync(dto.BookingId, uid))
            {
                return Deny();
            }
        }

        var result = await _svc.CreateAsync(dto);

        return StatusCode(201, result);
    }

    // Nhân viên có quyền xác nhận thanh toán thủ công.
    [Authorize(Roles = AppRoles.AdminSellerAccountant)]
    [HttpPut("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mã thanh toán không hợp lệ."
            });
        }

        var result = await _svc.ConfirmAsync(id);

        return Ok(result);
    }

    // Tra cứu thanh toán của một đặt vé.
    [Authorize(Roles = AppRoles.AdminSellerCustomer + "," + AppRoles.Accountant)]
    [HttpGet("booking/{bookingId:int}")]
    public async Task<IActionResult> ByBooking(int bookingId)
    {
        if (bookingId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mã đặt vé không hợp lệ."
            });
        }

        var uid = UserClaims.UserId(User);

        var allowed =
            IsStaff ||
            User.IsInRole("Accountant") ||
            (uid != null &&
             await _bookings.IsOwnerAsync(bookingId, uid));

        if (!allowed)
            return Deny();

        var result = await _svc.ByBookingAsync(bookingId);

        return Ok(result);
    }

    // Webhook SePay.
    // URL: POST /api/Payments/webhook/sepay
    // Header: Authorization: Apikey <khóa bí mật>
    [AllowAnonymous]
    [HttpPost("webhook/sepay")]
    public async Task<IActionResult> SePayWebhook(
     [FromBody] SePayWebhookDto dto)
    {
        var expected = _cfg["Payment:WebhookApiKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            _logger.LogError(
                "Webhook bị từ chối vì chưa cấu hình Payment:WebhookApiKey.");

            return StatusCode(503, new
            {
                success = false,
                message = "Webhook chưa được cấu hình."
            });
        }

        if (!IsValidWebhookApiKey())
        {
            _logger.LogWarning(
                "Webhook SePay bị từ chối do API key không hợp lệ.");

            return Unauthorized(new
            {
                success = false,
                message = "API key không hợp lệ."
            });
        }

        if (dto == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Payload webhook không hợp lệ."
            });
        }

        // Chỉ xử lý giao dịch tiền vào.
        if (!string.Equals(
            dto.TransferType,
            "in",
            StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                success = true,
                result = "ignored: không phải giao dịch tiền vào"
            });
        }

        if (dto.Id <= 0 ||
            string.IsNullOrWhiteSpace(dto.Content) ||
            dto.TransferAmount <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Dữ liệu giao dịch không hợp lệ."
            });
        }

        try
        {
            var result = await _svc.ConfirmByTransferAsync(
                dto.Content,
                dto.TransferAmount,
                dto.Id.ToString(),
                dto.ReferenceCode
            );

            _logger.LogInformation(
                "Webhook SePay xử lý xong. TransactionId: {TransactionId}, ReferenceCode: {ReferenceCode}, Result: {Result}",
                dto.Id,
                dto.ReferenceCode,
                result);

            return Ok(new
            {
                success = true,
                result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi xử lý webhook SePay. TransactionId: {TransactionId}, ReferenceCode: {ReferenceCode}",
                dto.Id,
                dto.ReferenceCode);

            return StatusCode(500, new
            {
                success = false,
                message = "Không thể xử lý giao dịch lúc này."
            });
        }
    }
}
