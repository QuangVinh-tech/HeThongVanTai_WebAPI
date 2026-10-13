using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongVanTai.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;
    private readonly ILogger<AccountController> _logger;

    // Cụm trong thông báo lỗi 403 của API khi chưa xác thực email (xem AuthController bên API)
    private const string NotConfirmedMarker = "chưa được xác thực";

    public AccountController(ApiClient api, ILogger<AccountController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // Email do người dùng nhập: bỏ xuống dòng, cắt ngắn trước khi ghi log
    private static string Safe(string? value)
    {
        var s = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length > 100 ? s[..100] : s;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _api.PostAsync<LoginResponse>("api/Auth/Login",
            new { username = model.Email, password = model.Password });

        var jwt = result.Data?.JwtToken;
        var info = string.IsNullOrEmpty(jwt)
            ? null
            : JwtReader.Read(jwt, CookieAuthenticationDefaults.AuthenticationScheme);

        // Mật khẩu đúng nhưng email chưa xác thực: gửi lại mã (nếu được) và chuyển sang trang nhập mã
        if (result.Status == 403 && (result.Error ?? "").Contains(NotConfirmedMarker))
        {
            await _api.PostAsync<MessageResponse>("api/Auth/resend-otp", new { email = model.Email });
            TempData["Error"] = "Tài khoản chưa xác thực email. Hãy nhập mã 6 số đã gửi tới email của bạn.";
            return RedirectToAction(nameof(VerifyEmail), new { email = model.Email });
        }

        if (!result.Ok || info == null)
        {
            var message = result.Status == 400
                ? "Email hoặc mật khẩu không đúng."
                : result.Error ?? "Đăng nhập thất bại. Vui lòng thử lại.";
            // Chỉ ghi email, tuyệt đối không ghi mật khẩu hay token
            _logger.LogWarning("Đăng nhập Web thất bại: {Email} (API trả {Status})", Safe(model.Email), result.Status);
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        // Cookie sống đúng bằng hạn của token; token được cất trong cookie để gắn vào các lời gọi API
        var properties = new AuthenticationProperties { IsPersistent = false, ExpiresUtc = info.ExpiresAt };
        properties.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = jwt! } });
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, info.Principal, properties);
        _logger.LogInformation("Đăng nhập Web thành công: {Email}", Safe(model.Email));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        if (info.Principal.IsInRole(AppRoles.Admin) || info.Principal.IsInRole(AppRoles.Operator))
        {
            return RedirectToAction("Dashboard", "Admin");
        }
        if (info.Principal.IsInRole(AppRoles.Accountant))
        {
            return RedirectToAction("Index", "Reports");
        }
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Không gửi roles: API luôn gán vai trò Customer cho người tự đăng ký
        var result = await _api.PostAsync<RegisterResponse>("api/Auth/Register",
            new { username = model.Email, password = model.Password });

        if (!result.Ok)
        {
            _logger.LogWarning("Đăng ký Web thất bại: {Email}", Safe(model.Email));
            ModelState.AddModelError(string.Empty, result.Error ?? "Đăng ký thất bại. Vui lòng thử lại.");
            return View(model);
        }

        _logger.LogInformation("Đăng ký Web thành công: {Email}", Safe(model.Email));
        if (result.Data?.RequiresVerification == true)
        {
            TempData[result.Data.EmailSent ? "Success" : "Error"] = result.Data.Message;
            return RedirectToAction(nameof(VerifyEmail), new { email = model.Email });
        }

        TempData["Success"] = "Đăng ký thành công. Hãy đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    // Nhập mã OTP 6 số gửi qua email để kích hoạt tài khoản
    [AllowAnonymous]
    [HttpGet]
    public IActionResult VerifyEmail(string? email)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Register));
        return View(new VerifyEmailViewModel { Email = email });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.PostAsync<MessageResponse>("api/Auth/verify-email",
            new { email = model.Email, code = model.Code.Trim() });

        if (!result.Ok)
        {
            // Không ghi mã OTP vào log
            _logger.LogWarning("Xác thực email Web thất bại: {Email} (API trả {Status})", Safe(model.Email), result.Status);
            model.Code = "";
            ModelState.Clear();
            ModelState.AddModelError(string.Empty, result.Error ?? "Xác thực thất bại. Vui lòng thử lại.");
            return View(model);
        }

        _logger.LogInformation("Xác thực email Web thành công: {Email}", Safe(model.Email));
        TempData["Success"] = "Xác thực thành công. Hãy đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Register));

        var result = await _api.PostAsync<MessageResponse>("api/Auth/resend-otp", new { email });
        if (result.Ok) TempData["Success"] = result.Data?.Message ?? "Đã gửi lại mã.";
        else TempData["Error"] = result.Error ?? "Không gửi lại được mã. Vui lòng thử lại.";
        return RedirectToAction(nameof(VerifyEmail), new { email });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("Đăng xuất Web: {Email}", Safe(User.Identity?.Name));
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
