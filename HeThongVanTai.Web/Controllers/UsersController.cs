using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.Users;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Chỉ Admin được quản lý người dùng
[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/Users")]
public class UsersController : Controller
{
    private const int PageSize = 10;

    private readonly UsersApiService _svc;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UsersApiService svc, ILogger<UsersController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, string? role, int page = 1)
    {
        var result = await _svc.GetPageAsync(keyword, role, Math.Max(page, 1), PageSize);
        ViewData["Keyword"] = keyword;
        ViewData["Role"] = role;
        ViewData["LoadError"] = result.Ok ? null : result.Error;
        ViewData["CurrentUserEmail"] = User.Identity?.Name;
        return View(result.Data ?? new UserPage { Page = page, PageSize = PageSize });
    }

    [HttpGet("{id}/Roles")]
    public async Task<IActionResult> Roles(string id)
    {
        var result = await _svc.GetAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }
        return View(ToForm(result.Data));
    }

    [HttpPost("{id}/Roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Roles(string id, UserRolesForm model)
    {
        // Chỉ nhận vai trò nằm trong danh sách hệ thống; API kiểm tra lại một lần nữa
        var selected = (model.SelectedRoles ?? new List<string>())
            .Where(r => RoleLabels.All.Contains(r)).Distinct().ToList();

        if (selected.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Phải chọn ít nhất một vai trò.");
            return await ShowRolesWithError(id, selected);
        }

        var result = await _svc.UpdateRolesAsync(id, selected);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được vai trò. Vui lòng thử lại.");
            return await ShowRolesWithError(id, selected);
        }

        _logger.LogInformation("{Admin} đổi vai trò người dùng {UserId}: {Roles}",
            User.Identity?.Name, id, string.Join(",", selected));
        TempData["Success"] = "Đã lưu vai trò. Có hiệu lực ở lần đăng nhập sau của người dùng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/Lock")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Lock(string id) => SetLock(id, true);

    [HttpPost("{id}/Unlock")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unlock(string id) => SetLock(id, false);

    private async Task<IActionResult> SetLock(string id, bool locked)
    {
        var result = await _svc.SetLockAsync(id, locked);
        if (result.Ok)
        {
            _logger.LogInformation("{Admin} {Action} tài khoản {UserId}",
                User.Identity?.Name, locked ? "khóa" : "mở khóa", id);
            TempData["Success"] = locked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không đổi được trạng thái tài khoản.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ShowRolesWithError(string id, List<string> selected)
    {
        var current = await _svc.GetAsync(id);
        if (!current.Ok || current.Data == null)
        {
            TempData["Error"] = current.Error ?? "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }
        var form = ToForm(current.Data);
        form.SelectedRoles = selected;   // giữ nguyên lựa chọn người dùng vừa chọn
        return View("Roles", form);
    }

    private UserRolesForm ToForm(UserItem user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        IsSelf = string.Equals(user.Email, User.Identity?.Name, StringComparison.OrdinalIgnoreCase),
        SelectedRoles = user.Roles.ToList()
    };
}
