using System.Text.Json;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.CaiDat;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Cài đặt hệ thống: /Admin/Settings (chỉ Admin)</summary>
[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/Settings")]
public class AdminSettingsController : Controller
{
    private const int MaxTiers = 10;

    private readonly ApiClient _api;
    private readonly SettingsApiService _site;
    private readonly ILogger<AdminSettingsController> _logger;

    public AdminSettingsController(ApiClient api, SettingsApiService site, ILogger<AdminSettingsController> logger)
    {
        _api = api;
        _site = site;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var r = await _api.GetAsync<PublicSettings>("api/settings");
        if (!r.Ok || r.Data == null)
        {
            ViewData["Error"] = r.Error ?? "Không đọc được cài đặt từ API.";
            return View(ToForm(new PublicSettings()));
        }
        return View(ToForm(r.Data));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsForm form)
    {
        // Bỏ các dòng mốc để trống hoàn toàn
        form.Tiers = (form.Tiers ?? new()).Where(t => t.HoursBefore.HasValue || t.RatePercent.HasValue).ToList();
        CheckRules(form);
        if (!ModelState.IsValid) return View(form);

        var body = new
        {
            holdMinutes = form.HoldMinutes,
            refundTiers = form.Tiers.Select(t => new { hoursBefore = t.HoursBefore!.Value, ratePercent = t.RatePercent!.Value }),
            companyName = form.CompanyName?.Trim(),
            logoUrl = form.LogoUrl?.Trim() ?? "",
            hotline = form.Hotline?.Trim() ?? "",
            email = form.Email?.Trim() ?? ""
        };

        var r = await _api.PutAsync<JsonElement>("api/settings", body);
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Không lưu được cài đặt.");
            return View(form);
        }

        _site.Invalidate();
        _logger.LogInformation("Admin cập nhật cài đặt hệ thống");
        TempData["Success"] = "Đã lưu cài đặt hệ thống.";
        return RedirectToAction(nameof(Index));
    }

    // Kiểm tra sớm để báo lỗi ngay trên form; API vẫn kiểm tra lại.
    private void CheckRules(SettingsForm f)
    {
        if (f.Tiers.Count < 1 || f.Tiers.Count > MaxTiers)
        {
            ModelState.AddModelError("", $"Cần từ 1 đến {MaxTiers} mốc hoàn tiền.");
            return;
        }
        if (f.Tiers.Any(t => !t.HoursBefore.HasValue || !t.RatePercent.HasValue))
        {
            ModelState.AddModelError("", "Mỗi mốc hoàn tiền cần nhập đủ số giờ và tỉ lệ.");
            return;
        }
        if (f.Tiers.Any(t => t.HoursBefore < 0 || t.HoursBefore > 720))
            ModelState.AddModelError("", "Số giờ của mốc hoàn tiền phải từ 0 đến 720.");
        if (f.Tiers.Any(t => t.RatePercent < 0 || t.RatePercent > 100))
            ModelState.AddModelError("", "Tỉ lệ hoàn tiền phải từ 0 đến 100%.");
        if (f.Tiers.Select(t => t.HoursBefore).Distinct().Count() != f.Tiers.Count)
            ModelState.AddModelError("", "Các mốc hoàn tiền không được trùng số giờ.");

        var sorted = f.Tiers.OrderByDescending(t => t.HoursBefore).ToList();
        for (var i = 1; i < sorted.Count; i++)
            if (sorted[i].RatePercent > sorted[i - 1].RatePercent)
            {
                ModelState.AddModelError("", "Hủy càng sát giờ chạy thì tỉ lệ hoàn không được cao hơn.");
                break;
            }
    }

    private static SettingsForm ToForm(PublicSettings s) => new()
    {
        HoldMinutes = s.HoldMinutes,
        Tiers = s.RefundTiers.OrderByDescending(t => t.HoursBefore)
            .Select(t => new TierForm { HoursBefore = t.HoursBefore, RatePercent = t.RatePercent }).ToList(),
        CompanyName = s.CompanyName,
        LogoUrl = s.LogoUrl,
        Hotline = s.Hotline,
        Email = s.Email
    };
}
