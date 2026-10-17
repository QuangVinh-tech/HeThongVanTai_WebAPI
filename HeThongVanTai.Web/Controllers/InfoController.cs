using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

[AllowAnonymous]
public class InfoController : Controller
{
    private readonly DanhMucApiService _danhMuc;
    public InfoController(DanhMucApiService danhMuc) => _danhMuc = danhMuc;

    public async Task<IActionResult> Schedule()
    {
        var routes = await _danhMuc.GetRoutesAsync();
        ViewData["Error"] = routes.Ok ? null : routes.Error;
        var list = routes.Ok && routes.Data != null ? routes.Data : new List<RouteItem>();
        return View(list.OrderBy(r => r.FromCity ?? r.FromStationName)
                        .ThenBy(r => r.ToCity ?? r.ToStationName).ToList());
    }

    public async Task<IActionResult> Offices()
    {
        var stations = await _danhMuc.GetStationsAsync(null, null);
        ViewData["Error"] = stations.Ok ? null : stations.Error;
        return View(stations.Ok && stations.Data != null ? stations.Data : new List<StationItem>());
    }

    public IActionResult Contact() => View();
    public IActionResult Policy() => View();
    public IActionResult Careers() => View();
}