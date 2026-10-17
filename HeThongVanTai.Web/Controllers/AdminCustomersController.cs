using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.KhachHang;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

// Quản lý khách hàng (việc 38): Admin và Seller; Customer không vào được (403)
[Authorize(Roles = AppRoles.AdminSeller)]
[Route("Admin/Customers")]
public class AdminCustomersController : Controller
{
    private readonly CustomersApiService _svc;
    private readonly ILogger<AdminCustomersController> _logger;

    public AdminCustomersController(CustomersApiService svc, ILogger<AdminCustomersController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, int page = 1)
    {
        if (page < 1) page = 1;
        var result = await _svc.GetPageAsync(keyword, page);
        var model = new CustomerListModel
        {
            Keyword = keyword,
            Page = page,
            PageSize = result.Data?.PageSize > 0 ? result.Data.PageSize : CustomersApiService.PageSize,
            Total = result.Data?.Total ?? 0,
            Items = result.Data?.Items ?? new List<CustomerItem>(),
            LoadError = result.Ok ? null : result.Error
        };
        return View(model);
    }

    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new CustomerForm());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerForm model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var result = await _svc.CreateAsync(model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được khách hàng.");
            return View("Form", model);
        }

        _logger.LogInformation("{User} thêm khách hàng", User.Identity?.Name);
        TempData["Success"] = "Đã thêm khách hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _svc.GetAsync(id);
        if (!result.Ok || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Không tìm thấy khách hàng.";
            return RedirectToAction(nameof(Index));
        }
        var c = result.Data;
        return View("Form", new CustomerForm { Id = c.Id, FullName = c.FullName, Phone = c.Phone, Email = c.Email });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerForm model)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await _svc.UpdateAsync(id, model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được khách hàng.");
            return View("Form", model);
        }

        _logger.LogInformation("{User} sửa khách hàng {Id}", User.Identity?.Name, id);
        TempData["Success"] = "Đã lưu khách hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _svc.DeleteAsync(id);
        if (result.Ok)
        {
            _logger.LogInformation("{User} xóa khách hàng {Id}", User.Identity?.Name, id);
            TempData["Success"] = "Đã xóa khách hàng.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không xóa được khách hàng.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("History/{id:int}")]
    public async Task<IActionResult> History(int id)
    {
        var customer = await _svc.GetAsync(id);
        if (!customer.Ok || customer.Data == null)
        {
            TempData["Error"] = customer.Error ?? "Không tìm thấy khách hàng.";
            return RedirectToAction(nameof(Index));
        }

        var history = await _svc.GetHistoryAsync(id);
        return View(new CustomerHistoryModel
        {
            Customer = customer.Data,
            Bookings = history.Data ?? new List<HistoryBooking>(),
            LoadError = history.Ok ? null : history.Error
        });
    }
}
