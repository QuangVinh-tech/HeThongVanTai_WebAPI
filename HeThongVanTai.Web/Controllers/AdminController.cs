using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Trang quản trị: chỉ Admin và Operator. Mỗi người thêm controller riêng cho trang của mình
// (ví dụ [Route("Admin/Vehicles")]) và kiểm tra quyền theo bảng phân quyền của nhóm.
[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly ApiClient _api;

    public AdminController(ApiClient api)
    {
        _api = api;
    }

    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        // Trang giữ chỗ: gọi thử API có đăng nhập để kiểm tra token được gắn đúng
        var me = await _api.GetAsync<MeResponse>("api/Auth/me");
        return View(new DashboardPlaceholderViewModel
        {
            ApiOk = me.Ok,
            Email = me.Data?.Email ?? User.Identity?.Name,
            Roles = me.Data?.Roles ?? new List<string>(),
            Error = me.Error
        });
    }
}
