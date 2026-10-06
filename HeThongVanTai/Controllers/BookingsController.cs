using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Controllers;

[ApiController]
public class BookingsController : ControllerBase
{
    private readonly BookingService _svc;
    public BookingsController(BookingService svc) => _svc = svc;

    // Tìm chuyến theo điểm đi, điểm đến, ngày
    [HttpGet("api/trips/search")]
    public async Task<IActionResult> Search(
        [FromQuery] int fromStationId, [FromQuery] int toStationId, [FromQuery] DateTime date)
        => Ok(await _svc.SearchAsync(fromStationId, toStationId, date));

    // Đặt vé nhiều hành khách + giữ ghế + mã giảm giá
    // TODO sau khi Vinh xong Auth: [Authorize] và lấy CustomerId từ token
    [HttpPost("api/bookings")]
    public async Task<IActionResult> Create(CreateBookingDto dto)
    {
        var (id, body) = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, body);
    }

    [HttpGet("api/bookings/{id:int}")]
    public async Task<IActionResult> GetById(int id) => Ok(await _svc.GetAsync(id));

    // Hủy booking; đã thanh toán thì hoàn tiền theo chính sách
    [HttpPut("api/bookings/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id) => Ok(await _svc.CancelAsync(id));
}