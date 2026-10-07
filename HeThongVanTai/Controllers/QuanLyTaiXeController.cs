using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;

namespace HeThongVanTai.Controllers
{
    [Route("QuanLyTaiXe")]
    public class QuanLyTaiXeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public QuanLyTaiXeController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? keyword, string? status, int page = 1)
        {
            int pageSize = 8;
            var query = _context.Drivers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(d => d.FullName.Contains(keyword) || (d.Phone != null && d.Phone.Contains(keyword)) || (d.LicenseNo != null && d.LicenseNo.Contains(keyword)));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(d => d.Status == status);

            int totalItems = await query.CountAsync();
            var drivers = await query
                .OrderBy(d => d.LicenseExpiry)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return View(drivers);
        }

        [HttpPost("Save")]
        public async Task<IActionResult> Save(Driver model, IFormFile? avatarFile)
        {
            if (avatarFile != null && avatarFile.Length > 0)
            {
                var ext = Path.GetExtension(avatarFile.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png" };
                if (!allowed.Contains(ext) || avatarFile.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Ảnh đại diện chỉ nhận .jpg, .jpeg, .png và tối đa 5MB!";
                    return RedirectToAction(nameof(Index));
                }

                string folder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "Images");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"driver_{Guid.NewGuid():N}{ext}";
                string fullPath = Path.Combine(folder, fileName);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await avatarFile.CopyToAsync(stream);

                model.AvatarUrl = $"/Images/{fileName}";
            }

            if (model.Id == 0)
            {
                _context.Drivers.Add(model);
                TempData["Success"] = "Thêm tài xế thành công!";
            }
            else
            {
                var existing = await _context.Drivers.FindAsync(model.Id);
                if (existing != null)
                {
                    existing.FullName = model.FullName;
                    existing.Phone = model.Phone;
                    existing.LicenseNo = model.LicenseNo;
                    existing.LicenseClass = model.LicenseClass;
                    existing.LicenseExpiry = model.LicenseExpiry;
                    existing.Status = model.Status;
                    if (!string.IsNullOrWhiteSpace(model.AvatarUrl))
                        existing.AvatarUrl = model.AvatarUrl;
                    TempData["Success"] = "Cập nhật tài xế thành công!";
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver != null)
            {
                if (await _context.Trips.AnyAsync(t => t.DriverId == id))
                {
                    TempData["Error"] = "Tài xế đã có lịch chạy, không thể xóa!";
                }
                else
                {
                    _context.Drivers.Remove(driver);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa tài xế thành công!";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}