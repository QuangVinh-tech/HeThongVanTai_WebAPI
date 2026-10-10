using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers;

[ApiController]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReviewsController(AppDbContext db) => _db = db;

    // Khách đã đi đánh giá chuyến: 1 vé đánh giá 1 lần
    [Authorize]
    [HttpPost("api/reviews")]
    public async Task<IActionResult> Create(CreateReviewDto dto)
    {
        var uid = UserClaims.UserId(User);
        if (uid == null) return Unauthorized();

        var t = await _db.Tickets
            .Include(x => x.Booking).ThenInclude(b => b.Customer)
            .Include(x => x.Trip)
            .FirstOrDefaultAsync(x => x.Id == dto.TicketId);

        if (t == null)
            return NotFound(new { message = "Không tìm thấy vé." });
        if (t.Booking.Customer.UserId != uid)
            return StatusCode(403, new { message = "Đây không phải vé của bạn." });
        if (t.Status != "Active" || t.Booking.Status != "Paid")
            return BadRequest(new { message = "Chỉ đánh giá được vé đã thanh toán và còn hiệu lực." });
        if ((t.Trip.ArriveAt ?? t.Trip.DepartAt) > DateTime.Now)
            return BadRequest(new { message = "Chuyến chưa kết thúc, chưa thể đánh giá." });
        if (await _db.Reviews.AnyAsync(r => r.TicketId == t.Id))
            return Conflict(new { message = "Vé này đã được đánh giá." });

        var review = new Review
        {
            TicketId = t.Id,
            TripId = t.TripId,
            CustomerId = t.Booking.CustomerId,
            Rating = dto.Rating,
            Comment = dto.Comment?.Trim()
        };
        _db.Reviews.Add(review);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (Rules.IsDuplicate(ex))
        {
            return Conflict(new { message = "Vé này đã được đánh giá." });
        }

        return StatusCode(201, new { review.Id, review.Rating, review.Comment, review.CreatedAt });
    }

    // Công khai: điểm trung bình và các nhận xét mới nhất của chuyến
    [AllowAnonymous]
    [HttpGet("api/trips/{tripId:int}/reviews")]
    public async Task<IActionResult> ByTrip(int tripId)
    {
        var q = _db.Reviews.AsNoTracking().Where(r => r.TripId == tripId);
        var count = await q.CountAsync();
        var average = count == 0 ? 0 : Math.Round(await q.AverageAsync(r => (double)r.Rating), 1);
        var items = await q.OrderByDescending(r => r.CreatedAt).Take(20)
            .Select(r => new { r.Rating, r.Comment, r.CreatedAt })
            .ToListAsync();
        return Ok(new { count, average, items });
    }

    // Danh sách TicketId mình đã đánh giá, để trang "Vé của tôi" ẩn nút Đánh giá
    [Authorize]
    [HttpGet("api/reviews/mine")]
    public async Task<IActionResult> Mine()
    {
        var uid = UserClaims.UserId(User);
        if (uid == null) return Unauthorized();

        var ids = await _db.Reviews.AsNoTracking()
            .Where(r => _db.Customers.Any(c => c.Id == r.CustomerId && c.UserId == uid))
            .Select(r => r.TicketId)
            .ToListAsync();
        return Ok(ids);
    }
}