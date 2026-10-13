using System.Text.Json;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DatVe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Quản lý khuyến mãi: /Admin/Promotions (chỉ Admin)</summary>
[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/Promotions")]
public class AdminPromotionsController : Controller
{
    private const int PageSize = 10;

    private readonly ApiClient _api;
    private readonly ILogger<AdminPromotionsController> _logger;

    public AdminPromotionsController(ApiClient api, ILogger<AdminPromotionsController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ---------- Danh sách (API trả hết, Web tìm theo mã và phân trang) ----------
    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, int page = 1)
    {
        var vm = new PromotionsViewModel { Keyword = keyword };

        var r = await _api.GetAsync<List<PromotionItem>>("api/promotions");
        if (!r.Ok)
        {
            vm.Error = r.Error;
            return View(vm);
        }

        var all = r.Data ?? new List<PromotionItem>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            all = all.Where(p => p.Code.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        vm.Total = all.Count;
        vm.TotalPages = (int)Math.Ceiling(all.Count / (double)PageSize);
        vm.Page = Math.Clamp(page, 1, Math.Max(vm.TotalPages, 1));
        vm.Items = all.Skip((vm.Page - 1) * PageSize).Take(PageSize).ToList();
        return View(vm);
    }

    // ---------- Thêm ----------
    [HttpGet("Create")]
    public IActionResult Create() =>
        View("Form", new PromotionForm { StartAt = DateTime.Today, EndAt = DateTime.Today.AddDays(30).AddHours(23).AddMinutes(59) });

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PromotionForm form)
    {
        CheckRules(form);
        if (!ModelState.IsValid) return View("Form", form);

        var r = await _api.PostAsync<JsonElement>("api/promotions", ToBody(form));
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Không thêm được mã giảm giá.");
            return View("Form", form);
        }

        _logger.LogInformation("{User} thêm mã giảm giá {Code}", User.Identity?.Name, form.Code);
        TempData["Success"] = "Đã thêm mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Sửa ----------
    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var promo = await FindAsync(id);
        if (promo == null)
        {
            TempData["Error"] = "Không tìm thấy mã giảm giá.";
            return RedirectToAction(nameof(Index));
        }
        return View("Form", new PromotionForm
        {
            Id = promo.Id,
            Code = promo.Code,
            DiscountType = promo.DiscountType,
            Value = promo.Value,
            MaxDiscount = promo.MaxDiscount,
            MinOrder = promo.MinOrder,
            StartAt = promo.StartAt,
            EndAt = promo.EndAt,
            UsageLimit = promo.UsageLimit
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PromotionForm form)
    {
        form.Id = id;
        CheckRules(form);
        if (!ModelState.IsValid) return View("Form", form);

        var r = await _api.PutAsync<JsonElement>($"api/promotions/{id}", ToBody(form));
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Không lưu được mã giảm giá.");
            return View("Form", form);
        }

        _logger.LogInformation("{User} sửa mã giảm giá {Id}", User.Identity?.Name, id);
        TempData["Success"] = "Đã lưu mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Bật / tắt ----------
    [HttpPost("Toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool value)
    {
        var r = await _api.PatchAsync<JsonElement>($"api/promotions/{id}/active?value={(value ? "true" : "false")}");
        if (!r.Ok) TempData["Error"] = r.Error ?? "Không đổi được trạng thái.";
        else TempData["Success"] = value ? "Đã bật mã giảm giá." : "Đã tắt mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Xóa (POST kèm token chống giả mạo) ----------
    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _api.DeleteAsync<JsonElement>($"api/promotions/{id}");
        if (!r.Ok)
        {
            // Mã đã được dùng trong đặt vé thì API từ chối, gợi ý chuyển sang tắt
            TempData["Error"] = r.Error ?? "Không xóa được mã giảm giá.";
        }
        else
        {
            _logger.LogInformation("{User} xóa mã giảm giá {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã xóa mã giảm giá.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Hàm dùng chung ----------
    private void CheckRules(PromotionForm f)
    {
        if (f.DiscountType != "Percent" && f.DiscountType != "Amount")
            ModelState.AddModelError(nameof(f.DiscountType), "Loại giảm giá phải là phần trăm hoặc số tiền.");
        if (f.DiscountType == "Percent" && f.Value > 100)
            ModelState.AddModelError(nameof(f.Value), "Giảm theo phần trăm không quá 100.");
        if (f.StartAt.HasValue && f.EndAt.HasValue && f.EndAt <= f.StartAt)
            ModelState.AddModelError(nameof(f.EndAt), "Thời điểm kết thúc phải sau thời điểm bắt đầu.");
    }

    private static object ToBody(PromotionForm f) => new
    {
        code = f.Code.Trim(),
        discountType = f.DiscountType,
        value = f.Value,
        maxDiscount = f.DiscountType == "Percent" ? f.MaxDiscount : null,
        minOrder = f.MinOrder,
        startAt = f.StartAt,
        endAt = f.EndAt,
        usageLimit = f.UsageLimit
    };

    // API không có "lấy một mã theo id", nên đọc danh sách rồi tìm
    private async Task<PromotionItem?> FindAsync(int id)
    {
        var r = await _api.GetAsync<List<PromotionItem>>("api/promotions");
        return r.Ok ? r.Data?.FirstOrDefault(p => p.Id == id) : null;
    }
}