using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using HeThongVanTai.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settings;
    private readonly IAuditService? _audit;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(SettingsService settings, ILogger<SettingsController> logger, IAuditService? audit = null)
    {
        _settings = settings;
        _logger = logger;
        _audit = audit;
    }

    // Công khai: chân trang, trang Liên hệ, Chính sách. Không chứa dữ liệu nhạy cảm.
    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<IActionResult> GetPublic() => Ok(await _settings.GetAsync());

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _settings.GetAsync());

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPut]
    public async Task<IActionResult> Save(SettingsDto dto)
    {
        var by = UserClaims.Email(User) ?? User.Identity?.Name;
        var error = await _settings.SaveAsync(dto, by);
        if (error != null) return BadRequest(new { message = error });

        _logger.LogInformation("Admin {User} cập nhật cài đặt hệ thống: giữ ghế {Hold} phút, {Tiers} mốc hoàn tiền",
            LogText.Clean(by), dto.HoldMinutes, dto.RefundTiers.Count);
        if (_audit != null)
            await _audit.LogAsync("Update", "SystemSetting", "settings",
                $"Giữ ghế {dto.HoldMinutes} phút; {dto.RefundTiers.Count} mốc hoàn tiền");
        return Ok(await _settings.GetAsync());
    }
}
