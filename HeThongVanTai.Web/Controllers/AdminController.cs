using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.Dashboard;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Trang quản trị: chỉ Admin và Operator. Mỗi người thêm controller riêng cho trang của mình
// (ví dụ [Route("Admin/Vehicles")]) và kiểm tra quyền theo bảng phân quyền của nhóm.
[Authorize(Roles = AppRoles.AdminOperator)]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly DashboardApiService _dashboard;

    public AdminController(DashboardApiService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        // Gọi song song; một API lỗi thì phần còn lại vẫn hiển thị
        var summaryTask = _dashboard.GetSummaryAsync(7);
        var alertsTask = _dashboard.GetAlertsAsync(30);
        await Task.WhenAll(summaryTask, alertsTask);
        var summary = summaryTask.Result;
        var alerts = alertsTask.Result;

        return View(new DashboardViewModel
        {
            Summary = summary.Ok ? summary.Data : null,
            SummaryError = summary.Ok ? null : summary.Error,
            Alerts = alerts.Data ?? new List<DashboardAlert>(),
            AlertsError = alerts.Ok ? null : alerts.Error
        });
    }
}
