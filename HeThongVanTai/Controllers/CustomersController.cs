using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;
    public CustomersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.FullName.Contains(k)
                          || (c.Phone != null && c.Phone.Contains(k))
                          || (c.Email != null && c.Email.Contains(k)));
        }

        var total = await q.CountAsync();
        var items = await q.OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveCustomerDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Phone) && await _db.Customers.AnyAsync(c => c.Phone == dto.Phone))
            return Conflict(new { message = "Số điện thoại đã tồn tại." });
        if (!string.IsNullOrWhiteSpace(dto.Email) && await _db.Customers.AnyAsync(c => c.Email == dto.Email))
            return Conflict(new { message = "Email đã tồn tại." });

        var c = new Customer { FullName = dto.FullName.Trim(), Phone = dto.Phone, Email = dto.Email };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = c.Id }, c);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveCustomerDto dto)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Phone) && await _db.Customers.AnyAsync(x => x.Phone == dto.Phone && x.Id != id))
            return Conflict(new { message = "Số điện thoại đã được khách khác dùng." });
        if (!string.IsNullOrWhiteSpace(dto.Email) && await _db.Customers.AnyAsync(x => x.Email == dto.Email && x.Id != id))
            return Conflict(new { message = "Email đã được khách khác dùng." });

        c.FullName = dto.FullName.Trim();
        c.Phone = dto.Phone;
        c.Email = dto.Email;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        if (await _db.Bookings.AnyAsync(b => b.CustomerId == id))
            return Conflict(new { message = "Khách đã có đặt vé, không thể xóa." });

        _db.Customers.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Lịch sử chuyến đi của khách
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == id)) return NotFound();

        var data = await _db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == id)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id,
                b.Code,
                b.Status,
                b.TotalAmount,
                b.DiscountAmount,
                b.CreatedAt,
                Tickets = b.Tickets.Select(t => new
                {
                    t.TicketCode,
                    t.SeatCode,
                    t.PassengerName,
                    t.Price,
                    t.Status,
                    Route = t.Trip.BusRoute.Name,
                    t.Trip.DepartAt
                })
            })
            .ToListAsync();
        return Ok(data);
    }
}