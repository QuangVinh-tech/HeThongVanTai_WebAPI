using System.Diagnostics;
using HeThongVanTai.Web.Models;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

public class HomeController : Controller
{
    private static readonly string[] BannerExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly DanhMucApiService _danhMuc;
    private readonly IWebHostEnvironment _env;

    public HomeController(DanhMucApiService danhMuc, IWebHostEnvironment env)
    {
        _danhMuc = danhMuc;
        _env = env;
    }

    // Ảnh banner do quản trị tự thả vào wwwroot/images/banners (xếp theo tên file, tối đa 8 ảnh).
    // Chưa có ảnh nào thì trang chủ dùng banner thiết kế sẵn.
    private List<string> GetBannerImages()
    {
        var dir = Path.Combine(_env.WebRootPath ?? "", "images", "banners");
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.EnumerateFiles(dir)
            .Where(f => BannerExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(f => "/images/banners/" + Uri.EscapeDataString(Path.GetFileName(f)))
            .ToList();
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        ViewData["BannerImages"] = GetBannerImages();

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
