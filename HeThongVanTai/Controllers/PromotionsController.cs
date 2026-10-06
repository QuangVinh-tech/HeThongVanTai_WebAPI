using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromotionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PromotionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Promotions.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync());

    // TODO sau Auth: chỉ Admin được tạo / xóa
    [HttpPost]
    public async Task<IActionResult> Create(SavePromotionDto dto)
    {
        if (dto.DiscountType != "Percent" && dto.DiscountType != "Amount")
            return BadRequest(new { message = "DiscountType phải là Percent hoặc Amount." });
        if (dto.DiscountType == "Percent" && dto.Value > 100)
            return BadRequest(new { message = "Giảm theo % không quá 100." });
        if (dto.EndAt <= dto.StartAt)
            return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });

        var code = dto.Code.Trim().ToUpperInvariant();
        if (await _db.Promotions.AnyAsync(p => p.Code == code))
            return Conflict(new { message = "Mã giảm giá đã tồn tại." });

        var p = new Promotion
        {
            Code = code,
            DiscountType = dto.DiscountType,
            Value = dto.Value,
            MaxDiscount = dto.MaxDiscount,
            MinOrder = dto.MinOrder,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt,
            UsageLimit = dto.UsageLimit
        };
        _db.Promotions.Add(p);
        await _db.SaveChangesAsync();
        return StatusCode(201, p);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Promotions.FindAsync(id);
        if (p is null) return NotFound();
        if (await _db.Bookings.AnyAsync(b => b.PromotionId == id))
            return Conflict(new { message = "Mã đã được dùng trong booking, hãy tắt (IsActive = false) thay vì xóa." });

        _db.Promotions.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Khách xem trước số tiền giảm
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateCode(ValidatePromoDto dto)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        var p = await _db.Promotions.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
        if (p is null) return NotFound(new { message = "Mã giảm giá không tồn tại." });

        var discount = Rules.Discount(p, dto.Amount, DateTime.Now, out var err);
        if (err != null) return BadRequest(new { message = err });

        return Ok(new { p.Code, dto.Amount, Discount = discount, Total = dto.Amount - discount });
    }
}