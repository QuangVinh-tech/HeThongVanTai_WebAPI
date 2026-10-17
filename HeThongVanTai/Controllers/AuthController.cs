using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
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
        private readonly RefreshTokenService _refresh;
        private readonly IServiceScopeFactory _scopeFactory;

        public AuthController(UserManager<IdentityUser> userManager, ITokenRepository tokenRepository,
            IEmailService email, IMemoryCache cache, IConfiguration config, ILogger<AuthController> logger,
            RefreshTokenService refresh, IServiceScopeFactory scopeFactory)
        {
            _refresh = refresh;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
            _tokenRepository = tokenRepository;
            _email = email;
            _cache = cache;
            _config = config;
            _logger = logger;
        }

        // Khóa tạm sau nhiều lần sai mật khẩu (Identity lockout): mặc định 5 lần, khóa 5 phút
        private int MaxFailedAttempts => _config.GetValue("Auth:MaxFailedAttempts", 5);
        private int LockoutMinutes => _config.GetValue("Auth:LockoutMinutes", 5);

        // Admin khóa tài khoản bằng cách đặt LockoutEnd = MaxValue (xem UsersController)
        private static bool IsAdminLock(IdentityUser u) =>
            u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow.AddDays(365);

        // Cho phép tắt bắt buộc xác thực (ví dụ máy dev chưa có SMTP): "Auth": { "RequireEmailConfirmation": false }
        private bool RequireConfirmation => _config.GetValue("Auth:RequireEmailConfirmation", true);

        // POST: /api/Auth/Register
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
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

        // POST: /api/Auth/forgot-password  { email }
        // Gửi liên kết đặt lại mật khẩu (hết hạn sau Auth:ResetLinkMinutes phút, mặc định 30). Luôn trả cùng một câu
        // dù email có tồn tại hay không; việc gửi mail chạy nền để thời gian phản hồi cũng không để lộ điều đó.
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
        {
            var email = dto.Email.Trim();

            // Mỗi email chỉ yêu cầu 1 lần mỗi 60 giây (tính theo email người ta nhập, nên không lộ email có tồn tại)
            var cooldownKey = "pwreset-cd:" + email.ToLowerInvariant();
            if (_cache.TryGetValue(cooldownKey, out DateTime until))
            {
                var wait = Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds));
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = $"Vui lòng đợi {wait} giây rồi yêu cầu lại." });
            }
            _cache.Set(cooldownKey, DateTime.UtcNow.AddSeconds(60), TimeSpan.FromSeconds(60));

            var user = await _userManager.FindByEmailAsync(email);
            // Tài khoản bị admin khóa không được tự mở khóa bằng cách đặt lại mật khẩu
            if (user != null && !IsAdminLock(user))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var link = BuildResetLink(user.Email!, token);
                var to = user.Email!;
                var minutes = ResetLinkMinutes;
                _ = SendResetEmailInBackgroundAsync(to, link, minutes);
                _logger.LogInformation("Yêu cầu đặt lại mật khẩu: {Email}", LogText.Clean(to));
            }
            else
            {
                _logger.LogInformation("Yêu cầu đặt lại mật khẩu cho email không có hoặc bị khóa: {Email}", LogText.Clean(email));
            }

            return Ok(new { message = ForgotPasswordMessage });
        }

        // POST: /api/Auth/reset-password  { email, token, newPassword }
        // Liên kết dùng một lần (đặt lại xong thì mã bảo mật của tài khoản đổi nên liên kết cũ vô hiệu) và hết hạn sau 30 phút.
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
        {
            // Cùng một thông báo cho mọi trường hợp liên kết không dùng được (email lạ, sai mã, hết hạn, đã dùng)
            var bad = new { message = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới." };

            var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
            if (user == null || IsAdminLock(user)) return BadRequest(bad);

            string token;
            try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token)); }
            catch (FormatException) { return BadRequest(bad); }

            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code == "InvalidToken"))
                {
                    _logger.LogWarning("Đặt lại mật khẩu thất bại (liên kết sai hoặc hết hạn): {Email}", LogText.Clean(user.Email));
                    return BadRequest(bad);
                }
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(PasswordErrorText)) });
            }

            // Nhận được liên kết qua email nghĩa là chủ email đã xác nhận; mở luôn khóa tạm do sai mật khẩu
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            // Mọi phiên đang đăng nhập (kể cả của kẻ lạ nếu có) phải đăng nhập lại
            await _refresh.RevokeAllAsync(user.Id);

            _logger.LogInformation("Đặt lại mật khẩu thành công: {Email}", LogText.Clean(user.Email));
            return Ok(new { message = "Đã đặt lại mật khẩu. Bạn có thể đăng nhập bằng mật khẩu mới." });
        }

        // POST: /api/Auth/Login
        // Sai mật khẩu quá số lần cho phép thì tạm khóa tài khoản; giới hạn số lần gọi theo địa chỉ IP (policy "auth").
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Username);
            if (user != null)
            {
                if (!user.LockoutEnabled) await _userManager.SetLockoutEnabledAsync(user, true);

                if (await _userManager.IsLockedOutAsync(user))
                {
                    if (IsAdminLock(user))
                    {
                        // Khóa bởi quản trị viên: chỉ nói rõ khi mật khẩu đúng (không lộ tài khoản nào tồn tại)
                        if (await _userManager.CheckPasswordAsync(user, dto.Password))
                        {
                            _logger.LogWarning("Đăng nhập bị chặn, tài khoản đã khóa: {Email}", user.Email);
                            return StatusCode(StatusCodes.Status403Forbidden,
                                new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });
                        }
                    }
                    else
                    {
                        var minutes = Math.Max(1, (int)Math.Ceiling((user.LockoutEnd!.Value - DateTimeOffset.UtcNow).TotalMinutes));
                        _logger.LogWarning("Đăng nhập bị chặn, tài khoản tạm khóa do sai mật khẩu: {Email}", user.Email);
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            message = $"Tài khoản đang tạm khóa do nhập sai mật khẩu quá nhiều lần. Vui lòng thử lại sau {minutes} phút."
                        });
                    }
                }
                else if (await _userManager.CheckPasswordAsync(user, dto.Password))
                {
                    // Mật khẩu đúng nhưng chưa xác thực email
                    if (RequireConfirmation && !user.EmailConfirmed)
                    {
                        _logger.LogWarning("Đăng nhập bị chặn, chưa xác thực email: {Email}", user.Email);
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = NotConfirmedMessage });
                    }

                    await _userManager.ResetAccessFailedCountAsync(user);

                    var roles = await _userManager.GetRolesAsync(user);
                    var token = _tokenRepository.CreateJWTToken(user, roles.ToList());
                    var refresh = await _refresh.IssueAsync(user.Id);
                    _logger.LogInformation("Đăng nhập thành công: {Email}", user.Email);
                    return Ok(new LoginResponseDTO
                    {
                        JwtToken = token,
                        RefreshToken = refresh.Token,
                        RefreshExpiresAt = refresh.ExpiresAt
                    });
                }
                else
                {
                    await _userManager.AccessFailedAsync(user);
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        _logger.LogWarning("Tài khoản bị tạm khóa do sai mật khẩu {Count} lần: {Email}", MaxFailedAttempts, user.Email);
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            message = $"Bạn đã nhập sai mật khẩu {MaxFailedAttempts} lần. Tài khoản tạm khóa {LockoutMinutes} phút."
                        });
                    }
                }
            }
            // Chỉ ghi email, tuyệt đối không ghi mật khẩu hay token
            _logger.LogWarning("Đăng nhập thất bại: {Email}", LogText.Clean(dto.Username));
            return BadRequest("Username or password incorrect");
        }

        // POST: /api/Auth/refresh  body: { "refreshToken": "..." }
        // Đổi refresh token lấy cặp token mới (access token ngắn hạn + refresh token mới). Vai trò được nạp lại ở đây.
        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDTO dto)
        {
            var rotated = await _refresh.RotateAsync(dto.RefreshToken);
            if (rotated == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });

            var userId = rotated.Value.UserId;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || await _userManager.IsLockedOutAsync(user) || (RequireConfirmation && !user.EmailConfirmed))
            {
                await _refresh.RevokeAllAsync(userId);
                return Unauthorized(new { message = "Tài khoản không còn được phép đăng nhập." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var jwt = _tokenRepository.CreateJWTToken(user, roles.ToList());
            return Ok(new LoginResponseDTO
            {
                JwtToken = jwt,
                RefreshToken = rotated.Value.Token.Token,
                RefreshExpiresAt = rotated.Value.Token.ExpiresAt
            });
        }

        // POST: /api/Auth/logout  body: { "refreshToken": "..." }  (thu hồi phiên; luôn trả OK)
        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequestDTO dto)
        {
            await _refresh.RevokeAsync(dto.RefreshToken);
            return Ok(new { message = "Đã đăng xuất." });
        }

        // GET: /api/Auth/me  (để kiểm tra token đang là ai, vai trò gì)
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me() => Ok(new
        {
            Email = User.FindFirstValue(ClaimTypes.Email),
            Roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value)
        });

        // POST: /api/Auth/change-password  body: { "currentPassword": "...", "newPassword": "..." }
        // Mọi vai trò đã đăng nhập đổi được mật khẩu của chính mình; phải nhập đúng mật khẩu cũ.
        // Không ghi mật khẩu (cũ hay mới) vào log.
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
        {
            var userId = UserClaims.UserId(User);
            var user = userId == null ? null : await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (dto.NewPassword == dto.CurrentPassword)
                return BadRequest(new { message = "Mật khẩu mới phải khác mật khẩu hiện tại." });

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
                {
                    _logger.LogWarning("Đổi mật khẩu thất bại (sai mật khẩu hiện tại): {Email}", user.Email);
                    return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
                }

                _logger.LogWarning("Đổi mật khẩu thất bại (mật khẩu mới không hợp lệ): {Email}", user.Email);
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(PasswordErrorText)) });
            }

            // Đổi mật khẩu thì các phiên khác (thiết bị khác) phải đăng nhập lại; giữ phiên đang dùng
            await _refresh.RevokeAllAsync(user.Id, dto.RefreshToken);

            _logger.LogInformation("Đổi mật khẩu thành công: {Email}", user.Email);
            return Ok(new { message = "Đã đổi mật khẩu." });
        }

        // ---------- hàm phụ ----------

        private int ResetLinkMinutes => Math.Max(1, _config.GetValue("Auth:ResetLinkMinutes", 30));

        private string ForgotPasswordMessage =>
            $"Nếu email này có tài khoản, chúng tôi đã gửi liên kết đặt lại mật khẩu. Liên kết có hiệu lực {ResetLinkMinutes} phút.";

        // Địa chỉ trang đặt lại mật khẩu lấy từ cấu hình (Web:BaseUrl), không lấy từ header Host của request
        private string BuildResetLink(string email, string token)
        {
            var baseUrl = (_config["Web:BaseUrl"] ?? "https://localhost:7300").TrimEnd('/');
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            return $"{baseUrl}/Account/ResetPassword?email={Uri.EscapeDataString(email)}&token={encoded}";
        }

        // Gửi chạy nền bằng scope riêng (scope của request đã đóng khi trả lời xong). Lỗi chỉ ghi log.
        private async Task SendResetEmailInBackgroundAsync(string to, string link, int minutes)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mail = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await mail.SendAsync(EmailTemplates.ResetPassword(to, link, minutes));
            }
            catch (EmailNotConfiguredException)
            {
                _logger.LogWarning("Chưa gửi được email đặt lại mật khẩu tới {Email} vì chưa cấu hình SMTP", LogText.Clean(to));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không gửi được email đặt lại mật khẩu tới {Email}", LogText.Clean(to));
            }
        }

        // Việt hóa các lỗi mật khẩu thường gặp của Identity
        private static string PasswordErrorText(IdentityError e) => e.Code switch
        {
            "PasswordTooShort" => "Mật khẩu mới quá ngắn (tối thiểu 6 ký tự).",
            "PasswordRequiresDigit" => "Mật khẩu mới phải có ít nhất một chữ số.",
            "PasswordRequiresLower" => "Mật khẩu mới phải có ít nhất một chữ thường.",
            "PasswordRequiresUpper" => "Mật khẩu mới phải có ít nhất một chữ hoa.",
            "PasswordRequiresNonAlphanumeric" => "Mật khẩu mới phải có ít nhất một ký tự đặc biệt.",
            "PasswordRequiresUniqueChars" => "Mật khẩu mới có quá ít ký tự khác nhau.",
            _ => e.Description
        };

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
