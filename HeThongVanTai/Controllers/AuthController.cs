using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Repositories;

namespace HeThongVanTai.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ITokenRepository _tokenRepository;

        public AuthController(UserManager<IdentityUser> userManager, ITokenRepository tokenRepository)
        {
            _userManager = userManager;
            _tokenRepository = tokenRepository;
        }

        // POST: /api/Auth/Register
        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO dto)
        {
            var user = new IdentityUser { UserName = dto.Username, Email = dto.Username };
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors.Select(e => e.Description));

            // Người lạ luôn là Customer; chỉ Admin đang đăng nhập mới được gán vai trò khác
            var roles = User.IsInRole(AppRoles.Admin) && dto.Roles.Length > 0
                ? dto.Roles
                : new[] { AppRoles.Customer };
            result = await _userManager.AddToRolesAsync(user, roles);
            if (!result.Succeeded) return BadRequest(result.Errors.Select(e => e.Description));

            return Ok("Register Successful! Let login!");
        }

        // POST: /api/Auth/Login
        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                var roles = await _userManager.GetRolesAsync(user);
                var token = _tokenRepository.CreateJWTToken(user, roles.ToList());
                return Ok(new LoginResponseDTO { JwtToken = token });
            }
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
    }
}