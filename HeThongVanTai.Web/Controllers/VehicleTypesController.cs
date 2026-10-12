using System.Text.Json;
using System.Text.RegularExpressions;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin/VehicleTypes")]
public class VehicleTypesController : Controller
{
    private const int MaxSeats = 100;
    private static readonly Regex SeatCodePattern = new("^[A-Za-z0-9-]{1,10}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly DanhMucApiService _svc;
    private readonly ILogger<VehicleTypesController> _logger;

    public VehicleTypesController(DanhMucApiService svc, ILogger<VehicleTypesController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await _svc.GetVehicleTypesAsync();
        ViewData["LoadError"] = result.Ok ? null : result.Error;
        return View(result.Data ?? new List<VehicleTypeItem>());
    }

    [HttpGet("Create")]
    public IActionResult Create() => View(new VehicleTypeForm());

    // API tự sinh sơ đồ ghế (A01.., B01..) theo số ghế và số tầng; sau đó chỉnh ở trang Sơ đồ ghế
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleTypeForm model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _svc.CreateVehicleTypeAsync(model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được loại xe. Vui lòng thử lại.");
            return View(model);
        }

        _logger.LogInformation("{User} thêm loại xe {Name} ({Seats} ghế)", User.Identity?.Name, model.Name, model.SeatCount);
        TempData["Success"] = "Đã thêm loại xe và tạo sơ đồ ghế mẫu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/Seats")]
    public async Task<IActionResult> Seats(int id)
    {
        var result = await _svc.GetVehicleTypeAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy loại xe.";
            return RedirectToAction(nameof(Index));
        }
        return View(ToSeatMap(result.Data));
    }

    [HttpPost("{id:int}/Seats")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Seats(int id, SeatMapForm model)
    {
        var seats = ParseSeats(model.SeatsJson, out var error);
        if (seats == null)
        {
            // Lỗi dữ liệu: hiện lại đúng sơ đồ người dùng vừa sửa
            return await ShowSeatsWithError(id, model, error!);
        }

        var result = await _svc.UpdateSeatsAsync(id, seats);
        if (!result.Ok)
        {
            return await ShowSeatsWithError(id, model, result.Error ?? "Không lưu được sơ đồ ghế. Vui lòng thử lại.");
        }

        _logger.LogInformation("{User} sửa sơ đồ ghế loại xe {Id}: {Count} ghế", User.Identity?.Name, id, seats.Count);
        TempData["Success"] = "Đã lưu sơ đồ ghế.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _svc.DeleteVehicleTypeAsync(id);
        if (result.Ok)
        {
            _logger.LogInformation("{User} xóa loại xe {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã xóa loại xe và sơ đồ ghế mẫu.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không xóa được loại xe.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ShowSeatsWithError(int id, SeatMapForm posted, string error)
    {
        var current = await _svc.GetVehicleTypeAsync(id);
        if (!current.Ok || current.Data == null)
        {
            TempData["Error"] = current.Error ?? "Không tìm thấy loại xe.";
            return RedirectToAction(nameof(Index));
        }

        var model = ToSeatMap(current.Data);
        model.SeatsJson = string.IsNullOrWhiteSpace(posted.SeatsJson) ? model.SeatsJson : posted.SeatsJson;
        ModelState.AddModelError(string.Empty, error);
        return View("Seats", model);
    }

    private static SeatMapForm ToSeatMap(VehicleTypeItem vt) => new()
    {
        Id = vt.Id,
        Name = vt.Name,
        Seats = vt.Seats,
        SeatsJson = JsonSerializer.Serialize(
            vt.Seats.Select(s => new SeatPayload { Code = s.SeatCode, Floor = s.Floor }), JsonOpts)
    };

    // Kiểm tra danh sách ghế từ trình duyệt; trả về null kèm thông báo nếu sai
    private static List<SeatItem>? ParseSeats(string? json, out string? error)
    {
        error = null;
        List<SeatPayload>? payload;
        try
        {
            payload = JsonSerializer.Deserialize<List<SeatPayload>>(json ?? "[]", JsonOpts);
        }
        catch (JsonException)
        {
            error = "Dữ liệu sơ đồ ghế không hợp lệ.";
            return null;
        }

        if (payload == null || payload.Count == 0)
        {
            error = "Sơ đồ ghế phải có ít nhất 1 ghế.";
            return null;
        }
        if (payload.Count > MaxSeats)
        {
            error = $"Mỗi loại xe tối đa {MaxSeats} ghế.";
            return null;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SeatItem>();
        foreach (var p in payload)
        {
            var code = (p.Code ?? "").Trim().ToUpperInvariant();
            if (!SeatCodePattern.IsMatch(code))
            {
                error = $"Mã ghế \"{code}\" không hợp lệ (chỉ gồm chữ, số, dấu gạch ngang, tối đa 10 ký tự).";
                return null;
            }
            if (p.Floor is < 1 or > 2)
            {
                error = $"Ghế {code} có số tầng không hợp lệ (chỉ tầng 1 hoặc 2).";
                return null;
            }
            if (!seen.Add(code))
            {
                error = $"Mã ghế {code} bị trùng.";
                return null;
            }
            result.Add(new SeatItem { SeatCode = code, Floor = p.Floor });
        }
        return result;
    }

    private class SeatPayload
    {
        public string? Code { get; set; }
        public int Floor { get; set; } = 1;
    }
}
