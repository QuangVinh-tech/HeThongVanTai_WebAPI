using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers;

// Nhật ký thao tác: chỉ Admin xem (theo sheet Phân quyền)
[ApiController]
[Route("api/AuditLogs")]
[Authorize(Roles = AppRoles.Admin)]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditLogsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword, [FromQuery] string? entityType, [FromQuery] string? action,
        [FromQuery(Name = "from")] DateTime? fromDate, [FromQuery(Name = "to")] DateTime? toDate,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(a => a.UserName.Contains(k) || (a.Detail != null && a.Detail.Contains(k))
                             || (a.EntityId != null && a.EntityId == k));
        }
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (fromDate.HasValue) { var f = fromDate.Value.Date; q = q.Where(a => a.CreatedAt >= f); }
        if (toDate.HasValue) { var t = toDate.Value.Date.AddDays(1); q = q.Where(a => a.CreatedAt < t); }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.Id, a.CreatedAt, a.UserName, a.Action, a.EntityType, a.EntityId, a.Detail })
            .ToListAsync();

        return Ok(new
        {
            TotalItems = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Items = items
        });
    }
}
