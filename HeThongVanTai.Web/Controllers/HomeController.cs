using System.Diagnostics;
using HeThongVanTai.Web.Models;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

public class HomeController : Controller
{
    private readonly DanhMucApiService _danhMuc;

    public HomeController(DanhMucApiService danhMuc) => _danhMuc = danhMuc;

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        // Load stations for the search form on the home page
        var stations = await _danhMuc.GetStationsAsync(null, null);
        ViewData["Stations"] = stations.Ok && stations.Data != null ? stations.Data : new List<StationItem>();

        // Tuyến phổ biến: lấy từ API tuyến, chọn các tuyến dài nhất (API chưa có số liệu lượt đặt)
        var routes = await _danhMuc.GetRoutesAsync();
        ViewData["PopularRoutes"] = routes.Ok && routes.Data != null
            ? routes.Data.OrderByDescending(r => r.DistanceKm).Take(6).ToList()
            : new List<RouteItem>();
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
