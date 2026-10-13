using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Repositories;
using HeThongVanTai.Services;
using HeThongVanTai.Services.Email;

namespace HeThongVanTai.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // Mục đích của mã OTP (Identity dùng làm một phần khóa tạo mã). Mã 6 số, hiệu lực khoảng 9 phút.
        private const string OtpPurpose = "ConfirmEmailOtp";
        private const int ResendCooldownSeconds = 60;

        // Web nhận biết lỗi này qua cụm "chưa được xác thực" (xem AccountController.Login bên Web)
        private const string NotConfirmedMessage =
            "Tài khoản chưa được xác thực email. Vui lòng nhập mã OTP đã gửi tới email của bạn.";

        private readonly UserManager<IdentityUser> _userManager;
        private readonly ITokenRepository _tokenRepository;
        private readonly IEmailService _email;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<IdentityUser> userManager, ITokenRepository tokenRepository,
            IEmailService email, IMemoryCache cache, IConfiguration config, ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _tokenRepository = tokenRepository;
            _email = email;
            _cache = cache;
            _config = config;
            _logger = logger;
        }

        // Cho phép tắt bắt buộc xác thực (ví dụ máy dev chưa có SMTP): "Auth": { "RequireEmailConfirmation": false }
        private bool RequireConfirmation => _config.GetValue("Auth:RequireEmailConfirmation", true);

        // POST: /api/Auth/Register
        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO dto)
        {
            var byAdmin = User.IsInRole(AppRoles.Admin);

            // Email đã đăng ký nhưng chưa xác thực: coi là đăng ký lại. Đặt lại mật khẩu theo lần này
            // (để người khác không giữ trước email của bạn rồi biết mật khẩu) và gửi lại mã OTP.
            var existing = await _userManager.FindByEmailAsync(dto.Username);
            if (existing != null && !existing.EmailConfirmed && !byAdmin)
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existing);
                var reset = await _userManager.ResetPasswordAsync(existing, resetToken, dto.Password);
                if (!reset.Succeeded) return BadRequest(reset.Errors.Select(e => e.Description));

                var resent = await SendOtpAsync(existing);
                StartCooldown(existing.Email!);
                _logger.LogInformation("Đăng ký lại tài khoản chưa xác thực: {Email}", LogText.Clean(dto.Username));
                return Ok(VerifyResponse(resent));
            }

            var user = new IdentityUser
            {
                UserName = dto.Username,
                Email = dto.Username,
                // Admin tạo hộ tài khoản nội bộ thì không cần OTP
                EmailConfirmed = byAdmin || !RequireConfirmation
            };
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Đăng ký thất bại: {Email}", LogText.Clean(dto.Username));
                return BadRequest(result.Errors.Select(e => e.Description));
            }

            // Người lạ luôn là Customer; chỉ Admin đang đăng nhập mới được gán vai trò khác
            var roles = byAdmin && dto.Roles.Length > 0
                ? dto.Roles
                : new[] { AppRoles.Customer };
            result = await _userManager.AddToRolesAsync(user, roles);
            if (!result.Succeeded) return BadRequest(result.Errors.Select(e => e.Description));

            _logger.LogInformation("Đăng ký thành công: {Email}, vai trò {Roles}, cần OTP {NeedOtp}",
                user.Email, string.Join(",", roles), !user.EmailConfirmed);

            if (user.EmailConfirmed)
                return Ok(new RegisterResponseDTO { Message = "Đăng ký thành công. Bạn có thể đăng nhập.", EmailSent = false });

            var sent = await SendOtpAsync(user);
            StartCooldown(user.Email!);
            return Ok(VerifyResponse(sent));
        }

        // POST: /api/Auth/verify-email  { email, code }
        [AllowAnonymous]
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDTO dto)
        {
            // Cùng một thông báo cho email không tồn tại và mã sai để không lộ email nào đã đăng ký
            var bad = new { message = "Mã xác thực không đúng hoặc đã hết hạn." };

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return BadRequest(bad);

            if (user.EmailConfirmed)
                return Ok(new { message = "Email đã được xác thực trước đó. Bạn có thể đăng nhập." });

            // Nhập sai nhiều lần thì tạm khóa (mặc định 5 lần, 5 phút) để chặn dò mã 6 số
            if (await _userManager.IsLockedOutAsync(user))
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = "Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau ít phút." });

            var ok = await _userManager.VerifyUserTokenAsync(
                user, TokenOptions.DefaultEmailProvider, OtpPurpose, dto.Code.Trim());
            if (!ok)
            {
                await _userManager.AccessFailedAsync(user);
                _logger.LogWarning("Nhập sai mã OTP: {Email}", LogText.Clean(dto.Email));
                return BadRequest(bad);
            }

            user.EmailConfirmed = true;
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded) return BadRequest(update.Errors.Select(e => e.Description));
            await _userManager.ResetAccessFailedCountAsync(user);

            _logger.LogInformation("Xác thực email thành công: {Email}", LogText.Clean(dto.Email));
            return Ok(new { message = "Xác thực thành công. Bạn có thể đăng nhập." });
        }

        // POST: /api/Auth/resend-otp  { email }
        [AllowAnonymous]
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDTO dto)
        {
            var key = CooldownKey(dto.Email);
            if (_cache.TryGetValue(key, out DateTime until))
            {
                var wait = Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds));
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = $"Vui lòng đợi {wait} giây rồi gửi lại mã." });
            }
            StartCooldown(dto.Email);

            // Luôn trả cùng một câu, dù email có tồn tại hay không
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user != null && !user.EmailConfirmed) await SendOtpAsync(user);
            return Ok(new { message = "Nếu email này đang chờ xác thực, mã mới đã được gửi." });
        }

        // POST: /api/Auth/Login
        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                // Tài khoản bị khóa không được đăng nhập (kiểm tra sau mật khẩu để không lộ tài khoản nào tồn tại)
                if (await _userManager.IsLockedOutAsync(user))
                {
                    _logger.LogWarning("Đăng nhập bị chặn, tài khoản đã khóa: {Email}", user.Email);
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });
                }

                // Mật khẩu đúng nhưng chưa xác thực email
                if (RequireConfirmation && !user.EmailConfirmed)
                {
                    _logger.LogWarning("Đăng nhập bị chặn, chưa xác thực email: {Email}", user.Email);
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = NotConfirmedMessage });
                }

                var roles = await _userManager.GetRolesAsync(user);
                var token = _tokenRepository.CreateJWTToken(user, roles.ToList());
                _logger.LogInformation("Đăng nhập thành công: {Email}", user.Email);
                return Ok(new LoginResponseDTO { JwtToken = token });
            }
            // Chỉ ghi email, tuyệt đối không ghi mật khẩu hay token
            _logger.LogWarning("Đăng nhập thất bại: {Email}", LogText.Clean(dto.Username));
            return BadRequest("Username or password incorrect");
        }

        // GET: /api/Auth/me  (để kiểm tra token đang là ai, vai trò gì)
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me() => Ok(new
        {
            Email = User.FindFirstValue(ClaimTypes.Email),
            Roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value)
        });

        // ---------- hàm phụ ----------

        private static string CooldownKey(string email) => "otp-resend:" + email.Trim().ToLowerInvariant();

        private void StartCooldown(string email) =>
            _cache.Set(CooldownKey(email), DateTime.UtcNow.AddSeconds(ResendCooldownSeconds),
                TimeSpan.FromSeconds(ResendCooldownSeconds));

        private static RegisterResponseDTO VerifyResponse(bool emailSent) => new()
        {
            RequiresVerification = true,
            EmailSent = emailSent,
            Message = emailSent
                ? "Chúng tôi đã gửi mã xác thực 6 số tới email của bạn."
                : "Đăng ký thành công nhưng chưa gửi được email. Hãy bấm \"Gửi lại mã\" sau ít phút."
        };

        // Tạo mã OTP và gửi email. Trả false nếu gửi lỗi (không ném lỗi để việc đăng ký vẫn hoàn tất).
        private async Task<bool> SendOtpAsync(IdentityUser user)
        {
            var code = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, OtpPurpose);
            try
            {
                await _email.SendAsync(new EmailMessage
                {
                    To = user.Email!,
                    Subject = "Mã xác thực tài khoản VậnTải HK",
                    Html = $"<p>Xin chào,</p>" +
                           $"<p>Mã xác thực tài khoản VậnTải HK của bạn là:</p>" +
                           $"<p style=\"font-size:28px;font-weight:bold;letter-spacing:6px\">{code}</p>" +
                           $"<p>Mã có hiệu lực trong khoảng 9 phút. Không chia sẻ mã này cho bất kỳ ai. " +
                           $"Nếu bạn không đăng ký tài khoản, hãy bỏ qua email này.</p>",
                    Text = $"Ma xac thuc: {code}"
                });
                return true;
            }
            catch (EmailNotConfiguredException)
            {
                // EmailService đã ghi log (Development: kèm mã OTP). Không phải lỗi bất ngờ nên không in stack trace.
                _logger.LogWarning("Chưa gửi được email OTP tới {Email} vì chưa cấu hình SMTP", LogText.Clean(user.Email));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không gửi được email OTP tới {Email}", LogText.Clean(user.Email));
                return false;
            }
        }
    }
}
