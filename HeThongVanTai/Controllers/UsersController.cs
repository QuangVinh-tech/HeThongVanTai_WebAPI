using HeThongVanTai.Models.Domain;
using HeThongVanTai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Controllers
{
    // Quản lý người dùng: chỉ Admin. Vai trò đổi ở đây có hiệu lực ở lần đăng nhập sau
    // (vai trò nằm trong token đã cấp, token sống 60 phút).
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : ControllerBase
    {
        private static readonly string[] AllRoles =
            { AppRoles.Admin, AppRoles.Operator, AppRoles.Seller, AppRoles.Accountant, AppRoles.Customer };

        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<UsersController> _logger;

        public UsersController(UserManager<IdentityUser> userManager, ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public class UpdateRolesDto
        {
            public string[] Roles { get; set; } = Array.Empty<string>();
        }

        public class LockUserDto
        {
            public bool Locked { get; set; }
        }

        private static bool IsLocked(IdentityUser u) =>
            u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow;

        private static object Shape(IdentityUser u, IEnumerable<string> roles) => new
        {
            u.Id,
            u.Email,
            Roles = roles.OrderBy(r => r).ToList(),
            IsLocked = IsLocked(u)
        };

        // GET: /api/Users/roles
        [HttpGet("roles")]
        public IActionResult GetRoles() => Ok(AllRoles);

        // GET: /api/Users?keyword=&role=&page=1&pageSize=10
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? keyword, [FromQuery] string? role,
                                              [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(role))
            {
                if (!AllRoles.Contains(role))
                    return BadRequest(new { message = "Vai trò không hợp lệ." });
                var ids = (await _userManager.GetUsersInRoleAsync(role)).Select(u => u.Id).ToList();
                query = query.Where(u => ids.Contains(u.Id));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                query = query.Where(u => u.Email != null && u.Email.Contains(k));
            }

            var total = await query.CountAsync();
            var users = await query.OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            var items = new List<object>();
            foreach (var u in users)
                items.Add(Shape(u, await _userManager.GetRolesAsync(u)));

            return Ok(new { Items = items, Total = total, Page = page, PageSize = pageSize });
        }

        // GET: /api/Users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });
            return Ok(Shape(user, await _userManager.GetRolesAsync(user)));
        }

        // PUT: /api/Users/{id}/roles  body: { "roles": ["Seller", "Customer"] }
        [HttpPut("{id}/roles")]
        public async Task<IActionResult> UpdateRoles(string id, [FromBody] UpdateRolesDto dto)
        {
            var roles = dto.Roles.Select(r => r?.Trim() ?? "").Where(r => r != "").Distinct().ToArray();
            if (roles.Length == 0)
                return BadRequest(new { message = "Phải chọn ít nhất một vai trò." });
            var unknown = roles.Where(r => !AllRoles.Contains(r)).ToList();
            if (unknown.Count > 0)
                return BadRequest(new { message = "Vai trò không hợp lệ: " + string.Join(", ", unknown) });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            var current = (await _userManager.GetRolesAsync(user)).ToList();

            // Không để hệ thống mất quản trị viên
            if (current.Contains(AppRoles.Admin) && !roles.Contains(AppRoles.Admin))
            {
                if (user.Id == UserClaims.UserId(User))
                    return BadRequest(new { message = "Bạn không thể tự bỏ vai trò Admin của chính mình." });
                if ((await _userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count <= 1)
                    return BadRequest(new { message = "Không thể bỏ vai trò Admin của quản trị viên cuối cùng." });
            }

            var toAdd = roles.Except(current).ToList();
            var toRemove = current.Except(roles).ToList();

            // Thêm trước, bỏ sau: lỗi giữa chừng thì người dùng vẫn còn vai trò
            if (toAdd.Count > 0)
            {
                var added = await _userManager.AddToRolesAsync(user, toAdd);
                if (!added.Succeeded)
                    return BadRequest(new { message = string.Join(" ", added.Errors.Select(e => e.Description)) });
            }
            if (toRemove.Count > 0)
            {
                var removed = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded)
                    return BadRequest(new { message = string.Join(" ", removed.Errors.Select(e => e.Description)) });
            }

            _logger.LogInformation("{Admin} đổi vai trò của {Email}: {Roles}",
                UserClaims.Email(User), user.Email, string.Join(",", roles));
            return Ok(Shape(user, roles));
        }

        // PATCH: /api/Users/{id}/lock  body: { "locked": true }
        [HttpPatch("{id}/lock")]
        public async Task<IActionResult> SetLock(string id, [FromBody] LockUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            if (dto.Locked)
            {
                if (user.Id == UserClaims.UserId(User))
                    return BadRequest(new { message = "Bạn không thể tự khóa tài khoản của chính mình." });

                if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
                {
                    var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
                    if (!admins.Any(a => a.Id != user.Id && !IsLocked(a)))
                        return BadRequest(new { message = "Không thể khóa quản trị viên cuối cùng." });
                }
            }

            var enabled = await _userManager.SetLockoutEnabledAsync(user, true);
            var result = enabled.Succeeded
                ? await _userManager.SetLockoutEndDateAsync(user, dto.Locked ? (DateTimeOffset?)DateTimeOffset.MaxValue : null)
                : enabled;
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

            if (!dto.Locked) await _userManager.ResetAccessFailedCountAsync(user);

            _logger.LogInformation("{Admin} {Action} tài khoản {Email}",
                UserClaims.Email(User), dto.Locked ? "khóa" : "mở khóa", user.Email);
            return Ok(Shape(user, await _userManager.GetRolesAsync(user)));
        }
    }
}
