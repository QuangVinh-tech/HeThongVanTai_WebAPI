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
public class ShipmentsController : ControllerBase
{
    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;
    private readonly ShipmentService _svc;

    public ShipmentsController(AppDbContext db, ShipmentService svc)
    {
        _db = db;
        _svc = svc;
    }

    private static IQueryable<ShipmentRowDto> ToRows(IQueryable<Shipment> q) =>
        q.OrderByDescending(s => s.Id).Select(s => new ShipmentRowDto
        {
            Id = s.Id,
            Code = s.Code,
            FromStation = s.FromStation.Name,
            ToStation = s.ToStation.Name,
            ReceiverName = s.ReceiverName,
            ReceiverPhone = s.ReceiverPhone,
            ServiceType = s.ServiceType,
            Status = s.Status,
            PaymentStatus = s.PaymentStatus,
            FeePayer = s.FeePayer,
            TotalAmount = s.TotalAmount,
            ParcelCount = s.Items.Sum(i => i.Quantity),
            CreatedAt = s.CreatedAt
        });

    private async Task<IActionResult> PageAsync(IQueryable<Shipment> q, string? status, int page, int pageSize)
    {
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(s => s.Status == status);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var total = await q.CountAsync();
        var items = await ToRows(q).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    // ---------- Công khai ----------
    [AllowAnonymous]
    [HttpPost("quote")]
    public async Task<IActionResult> Quote(QuoteRequestDto dto) => Ok(await _svc.QuoteAsync(dto));

    // Công khai nên chỉ trả thông tin tối thiểu và che số điện thoại
    [AllowAnonymous]
    [HttpGet("track/{code}")]
    public async Task<IActionResult> Track(string code)
    {
        var c = code.Trim().ToUpperInvariant();
        var s = await _db.Shipments.AsNoTracking()
            .Include(x => x.FromStation).Include(x => x.ToStation).Include(x => x.Logs)
            .FirstOrDefaultAsync(x => x.Code == c);
        if (s is null) return NotFound(new { message = "Không tìm thấy đơn hàng với mã này." });

        return Ok(new
        {
            s.Code,
            s.Status,
            s.ServiceType,
            s.CreatedAt,
            From = s.FromStation.Name,
            To = s.ToStation.Name,
            s.ReceiverName,
            ReceiverPhone = ShippingRules.MaskPhone(s.ReceiverPhone),
            Logs = s.Logs.OrderBy(l => l.At).Select(l => new { l.Status, l.Note, l.At })
        });
    }

    // ---------- Khách hàng ----------
    [Authorize(Roles = AppRoles.AdminSellerCustomer)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateShipmentDto dto)
    {
        var canDebt = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Seller);
        var s = await _svc.CreateAsync(dto, UserClaims.UserId(User), UserClaims.Email(User), canDebt);
        return StatusCode(201, new { s.Id, s.Code, s.ShippingFee, s.DiscountAmount, s.TotalAmount });
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var uid = UserClaims.UserId(User);
        var q = _db.Shipments.AsNoTracking().Where(s => s.CreatedByUserId == uid);
        return await PageAsync(q, status, page, pageSize);
    }

    [Authorize]
    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        await _svc.CancelAsync(id, UserClaims.UserId(User) ?? "", UserClaims.Email(User));
        return Ok(new { message = "Đã hủy đơn hàng." });
    }

    // ---------- Nhân viên ----------
    [Authorize(Roles = "Admin,Operator,Seller,Accountant")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? keyword,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var q = _db.Shipments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(s => s.Code.Contains(k) || s.SenderPhone.Contains(k) || s.ReceiverPhone.Contains(k));
        }
        return await PageAsync(q, status, page, pageSize);
    }

    [Authorize(Roles = "Admin,Operator,Seller,Accountant")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await _db.Shipments.AsNoTracking()
            .Include(x => x.FromStation).Include(x => x.ToStation)
            .Include(x => x.Items).Include(x => x.Logs)
            .Include(x => x.Trip).ThenInclude(t => t!.BusRoute)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound(new { message = "Không tìm thấy đơn hàng." });

        return Ok(new
        {
            s.Id,
            s.Code,
            FromStation = s.FromStation.Name,
            ToStation = s.ToStation.Name,
            s.SenderName,
            s.SenderPhone,
            s.SenderAddress,
            s.ReceiverName,
            s.ReceiverPhone,
            s.ReceiverAddress,
            s.PickupMode,
            s.DeliveryMode,
            s.ServiceType,
            s.FeePayer,
            s.DeclaredValue,
            s.ShippingFee,
            s.DiscountAmount,
            s.TotalAmount,
            s.Status,
            s.PaymentStatus,
            s.PaidAt,
            s.Note,
            s.CreatedAt,
            Trip = s.Trip == null ? null : new { s.Trip.Id, Route = s.Trip.BusRoute.Name, s.Trip.DepartAt },
            Items = s.Items.Select(i => new { i.GoodsType, i.Name, i.Weight, i.Length, i.Width, i.Height, i.Quantity, i.ChargeWeight }),
            Logs = s.Logs.OrderBy(l => l.At).Select(l => new { l.Status, l.Note, l.At, l.ByEmail })
        });
    }

    [Authorize(Roles = AppRoles.AdminOperator)]
    [HttpGet("{id:int}/available-trips")]
    public async Task<IActionResult> AvailableTrips(int id)
    {
        var s = await _db.Shipments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound(new { message = "Không tìm thấy đơn hàng." });

        var now = DateTime.Now;
        var trips = await _db.Trips.AsNoTracking()
            .Where(t => t.Status == "Open" && t.DepartAt > now &&
                ((t.BusRoute.FromStationId == s.FromStationId && t.BusRoute.ToStationId == s.ToStationId) ||
                 (t.BusRoute.FromStationId == s.ToStationId && t.BusRoute.ToStationId == s.FromStationId)))
            .OrderBy(t => t.DepartAt).Take(30)
            .Select(t => new { t.Id, Route = t.BusRoute.Name, t.DepartAt, Plate = t.Vehicle.Plate })
            .ToListAsync();
        return Ok(trips);
    }

    [Authorize(Roles = AppRoles.AdminOperator)]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusDto dto)
    {
        await _svc.ChangeStatusAsync(id, dto.Status, dto.Note, UserClaims.Email(User));
        return Ok(new { message = "Đã cập nhật trạng thái." });
    }

    [Authorize(Roles = AppRoles.AdminOperator)]
    [HttpPut("{id:int}/assign-trip")]
    public async Task<IActionResult> AssignTrip(int id, AssignTripDto dto)
    {
        await _svc.AssignTripAsync(id, dto.TripId, UserClaims.Email(User));
        return Ok(new { message = "Đã gắn chuyến xe." });
    }

    [Authorize(Roles = "Admin,Seller,Accountant")]
    [HttpPatch("{id:int}/collect-fee")]
    public async Task<IActionResult> CollectFee(int id)
    {
        await _svc.CollectFeeAsync(id, UserClaims.Email(User));
        return Ok(new { message = "Đã ghi nhận thu cước." });
    }
}