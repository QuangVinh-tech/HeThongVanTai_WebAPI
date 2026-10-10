using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Controllers;

[ApiController]
public class BookingsController : ControllerBase
{
    private readonly BookingService _svc;
    public BookingsController(BookingService svc) => _svc = svc;

    private bool IsStaff => User.IsInRole("Admin") || User.IsInRole("Seller");

    private IActionResult Deny() =>
        StatusCode(403, new { message = "Bạn không có quyền với đặt vé này." });

    private async Task<bool> CanAccessBookingAsync(int bookingId)
    {
        if (IsStaff) return true;
        var uid = UserClaims.UserId(User);
        return uid != null && await _svc.IsOwnerAsync(bookingId, uid);
    }

    // Công khai: tìm chuyến
    [HttpGet("api/trips/search")]
    public async Task<IActionResult> Search(
        [FromQuery] int fromStationId, [FromQuery] int toStationId, [FromQuery] DateTime date)
        => Ok(await _svc.SearchAsync(fromStationId, toStationId, date));

    // Đặt vé: khách thường lấy hồ sơ từ token; Admin/Seller (bán tại quầy) gửi customerId trong body
    [Authorize]
    [HttpPost("api/bookings")]
    public async Task<IActionResult> Create(CreateBookingDto dto)
    {
        if (!IsStaff)
        {
            var uid = UserClaims.UserId(User);
            if (uid == null) return Unauthorized();
            dto.CustomerId = await _svc.GetOrCreateCustomerIdAsync(uid, UserClaims.Email(User));
        }
        var (id, body) = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, body);
    }

    [Authorize]
    [HttpGet("api/bookings/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await CanAccessBookingAsync(id)) return Deny();
        return Ok(await _svc.GetAsync(id));
    }

    // Công khai: tra cứu theo mã đặt vé
    [HttpGet("api/bookings/by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code) => Ok(await _svc.GetByCodeAsync(code));

    // "Vé của tôi"
    [Authorize]
    [HttpGet("api/bookings/mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var uid = UserClaims.UserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _svc.MineAsync(uid, page, pageSize));
    }

    // Xem trước số tiền hoàn trước khi hủy
    [Authorize]
    [HttpGet("api/bookings/{id:int}/refund-preview")]
    public async Task<IActionResult> RefundPreview(int id)
    {
        if (!await CanAccessBookingAsync(id)) return Deny();
        return Ok(await _svc.RefundPreviewAsync(id));
    }

    [Authorize]
    [HttpPut("api/bookings/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!await CanAccessBookingAsync(id)) return Deny();
        return Ok(await _svc.CancelAsync(id));
    }

    // Quản trị: lọc, sắp xếp, phân trang
    [Authorize(Roles = "Admin,Seller")]
    [HttpGet("api/bookings")]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? keyword,
        [FromQuery(Name = "from")] DateTime? fromDate, [FromQuery(Name = "to")] DateTime? toDate,
        [FromQuery] string? sortBy, [FromQuery] bool desc = true,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => Ok(await _svc.ListAsync(status, keyword, fromDate, toDate, sortBy, desc, page, pageSize));
}