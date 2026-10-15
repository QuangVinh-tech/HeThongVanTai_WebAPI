using HeThongVanTai.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Trang đích sau đăng nhập của vai trò Tài xế (/Driver/Trips).
// Hiện là trang khung để phân quyền chạy đúng; việc 40 (Tân) bổ sung danh sách chuyến được phân công,
// danh sách hành khách và nút bắt đầu, báo trễ, hoàn thành chuyến.
[Authorize(Roles = AppRoles.Driver)]
[Route("Driver/Trips")]
public class DriverTripsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
