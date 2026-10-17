using System.Text.RegularExpressions;
using HeThongVanTai.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

/// <summary>Soát vé: nhân viên quét QR hoặc nhập mã vé (/CheckIn).</summary>
[Authorize(Roles = AppRoles.CheckInStaff)]
[Route("CheckIn")]
public class CheckInController : Controller
{
    private static readonly Regex CodeFormat = new("^[A-Za-z0-9]{4,20}$", RegexOptions.Compiled);

    private readonly ApiClient _api;

    public CheckInController(ApiClient api) => _api = api;

    [HttpGet("")]
    public IActionResult Index() => View(new CheckInPageModel());

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string? code)
    {
        var vm = new CheckInPageModel { Code = code?.Trim().ToUpperInvariant() };

        if (string.IsNullOrWhiteSpace(vm.Code) || !CodeFormat.IsMatch(vm.Code))
        {
            vm.Error = "Mã vé không hợp lệ.";
            return View(vm);
        }

        var result = await _api.PostAsync<CheckInResult>($"api/tickets/{Uri.EscapeDataString(vm.Code)}/checkin");
        if (!result.Ok || result.Data == null)
        {
            vm.Error = result.Error ?? "Không soát được vé. Vui lòng thử lại.";
            return View(vm);
        }

        vm.Result = result.Data;
        vm.Code = null;   // xóa ô nhập để quét vé tiếp theo
        return View(vm);
    }
}

public class CheckInResult
{
    public string TicketCode { get; set; } = "";
    public string SeatCode { get; set; } = "";
    public string? PassengerName { get; set; }
    public string Route { get; set; } = "";
    public DateTime DepartAt { get; set; }
}

public class CheckInPageModel
{
    public string? Code { get; set; }
    public string? Error { get; set; }
    public CheckInResult? Result { get; set; }
}