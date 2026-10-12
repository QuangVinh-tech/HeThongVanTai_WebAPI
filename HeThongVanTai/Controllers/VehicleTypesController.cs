using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;

namespace HeThongVanTai.Controllers
{
    [Route("api/vehicle-types")]
    [ApiController]
    [Authorize(Roles = AppRoles.AdminOperator)]
    public class VehicleTypesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VehicleTypesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var types = await _context.VehicleTypes
                .Include(vt => vt.SeatTemplates.OrderBy(s => s.Floor).ThenBy(s => s.SeatCode))
                .Select(vt => new
                {
                    vt.Id,
                    vt.Name,
                    vt.SeatCount,
                    Seats = vt.SeatTemplates.Select(s => new { s.Id, s.SeatCode, s.Floor })
                })
                .ToListAsync();

            return Ok(types);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var vt = await _context.VehicleTypes
                .Include(v => v.SeatTemplates.OrderBy(s => s.Floor).ThenBy(s => s.SeatCode))
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vt == null) return NotFound(new { message = "Không tìm thấy loại xe." });

            return Ok(new
            {
                vt.Id,
                vt.Name,
                vt.SeatCount,
                Seats = vt.SeatTemplates.Select(s => new { s.Id, s.SeatCode, s.Floor })
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateVehicleTypeDto dto)
        {
            var vehicleType = new VehicleType
            {
                Name = dto.Name,
                SeatCount = dto.SeatCount
            };

            _context.VehicleTypes.Add(vehicleType);
            await _context.SaveChangesAsync();

            var seatList = new List<SeatTemplate>();

            if (dto.CustomSeats != null && dto.CustomSeats.Any())
            {
                foreach (var s in dto.CustomSeats)
                {
                    seatList.Add(new SeatTemplate
                    {
                        VehicleTypeId = vehicleType.Id,
                        SeatCode = s.SeatCode,
                        Floor = s.Floor
                    });
                }
                vehicleType.SeatCount = seatList.Count;
            }
            else
            {
                int floors = dto.Floors <= 1 ? 1 : 2;
                int seatsPerFloor = (int)Math.Ceiling((double)dto.SeatCount / floors);
                int currentSeat = 0;

                for (int floor = 1; floor <= floors; floor++)
                {
                    string prefix = floor == 1 ? "A" : "B";
                    for (int i = 1; i <= seatsPerFloor && currentSeat < dto.SeatCount; i++)
                    {
                        currentSeat++;
                        seatList.Add(new SeatTemplate
                        {
                            VehicleTypeId = vehicleType.Id,
                            SeatCode = $"{prefix}{i:D2}",
                            Floor = floor
                        });
                    }
                }
            }

            _context.SeatTemplates.AddRange(seatList);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = vehicleType.Id }, new
            {
                vehicleType.Id,
                vehicleType.Name,
                vehicleType.SeatCount,
                TotalSeatsCreated = seatList.Count
            });
        }

        [HttpPut("{id}/seats")]
        public async Task<IActionResult> UpdateSeatTemplates(int id, [FromBody] List<SeatTemplateItemDto> seats)
        {
            var vt = await _context.VehicleTypes
                .Include(v => v.SeatTemplates)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vt == null) return NotFound(new { message = "Không tìm thấy loại xe." });

            _context.SeatTemplates.RemoveRange(vt.SeatTemplates);

            var newSeats = seats.Select(s => new SeatTemplate
            {
                VehicleTypeId = id,
                SeatCode = s.SeatCode,
                Floor = s.Floor
            }).ToList();

            _context.SeatTemplates.AddRange(newSeats);
            vt.SeatCount = newSeats.Count;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật sơ đồ ghế thành công.", seatCount = vt.SeatCount });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var vt = await _context.VehicleTypes
                .Include(v => v.SeatTemplates)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vt == null) return NotFound(new { message = "Không tìm thấy loại xe." });

            bool inUse = await _context.Vehicles.AnyAsync(v => v.VehicleTypeId == id);
            if (inUse)
                return BadRequest(new { message = "Không thể xóa loại xe đang có phương tiện sử dụng." });

            _context.SeatTemplates.RemoveRange(vt.SeatTemplates);
            _context.VehicleTypes.Remove(vt);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa loại xe và sơ đồ ghế mẫu." });
        }
    }
}