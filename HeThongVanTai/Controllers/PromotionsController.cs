using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromotionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PromotionsController(AppDbContext db) => _db = db;

    private static string? Validate(SavePromotionDto dto)
    {
        if (dto.DiscountType != "Percent" && dto.DiscountType != "Amount")
            return "DiscountType phải là Percent hoặc Amount.";
        if (dto.DiscountType == "Percent" && dto.Value > 100)
            return "Giảm theo % không quá 100.";
        if (dto.EndAt <= dto.StartAt)
            return "Ngày kết thúc phải sau ngày bắt đầu.";
        return null;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Promotions.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync());

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(SavePromotionDto dto)
    {
        var err = Validate(dto);
        if (err != null) return BadRequest(new { message = err });

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

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SavePromotionDto dto)
    {
        var err = Validate(dto);
        if (err != null) return BadRequest(new { message = err });

        var p = await _db.Promotions.FindAsync(id);
        if (p is null) return NotFound();

        var code = dto.Code.Trim().ToUpperInvariant();
        if (await _db.Promotions.AnyAsync(x => x.Code == code && x.Id != id))
            return Conflict(new { message = "Mã giảm giá đã tồn tại." });
        if (dto.UsageLimit.HasValue && dto.UsageLimit < p.UsedCount)
            return BadRequest(new { message = "Giới hạn lượt dùng không được nhỏ hơn số lượt đã dùng." });

        p.Code = code;
        p.DiscountType = dto.DiscountType;
        p.Value = dto.Value;
        p.MaxDiscount = dto.MaxDiscount;
        p.MinOrder = dto.MinOrder;
        p.StartAt = dto.StartAt;
        p.EndAt = dto.EndAt;
        p.UsageLimit = dto.UsageLimit;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value)
    {
        var p = await _db.Promotions.FindAsync(id);
        if (p is null) return NotFound();
        p.IsActive = value;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
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

    // Khách xem trước số tiền giảm (phải đăng nhập)
    [Authorize]
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