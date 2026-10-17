using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.NhatKy;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Nhật ký thao tác (việc 42): chỉ Admin xem
[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/AuditLogs")]
public class AuditLogsController : Controller
{
    private readonly AuditLogsApiService _svc;
    public AuditLogsController(AuditLogsApiService svc) => _svc = svc;

    [HttpGet("")]
    public async Task<IActionResult> Index(AuditFilter filter)
    {
        if (filter.Page < 1) filter.Page = 1;
        var result = await _svc.GetAsync(filter);
        return View(new AuditListModel
        {
            Filter = filter,
            Items = result.Data?.Items ?? new List<AuditLogItem>(),
            TotalItems = result.Data?.TotalItems ?? 0,
            TotalPages = result.Data?.TotalPages ?? 0,
            LoadError = result.Ok ? null : result.Error
        });
    }
}
