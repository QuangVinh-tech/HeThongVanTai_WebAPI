using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;

namespace HeThongVanTai.Controllers
{
    [Route("QuanLyXe")]
    public class QuanLyXeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public QuanLyXeController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? searchPlate, string? status, string? sortBy, int page = 1)
        {
            int pageSize = 8;
            var query = _context.Vehicles.Include(v => v.VehicleType).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchPlate))
                query = query.Where(v => v.Plate.Contains(searchPlate));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(v => v.Status == status);

            query = sortBy switch
            {
                "plate_desc" => query.OrderByDescending(v => v.Plate),
                "reg_asc" => query.OrderBy(v => v.RegistrationExpiry),
                "ins_asc" => query.OrderBy(v => v.InsuranceExpiry),
                _ => query.OrderBy(v => v.Plate)
            };

            int totalItems = await query.CountAsync();
            var vehicles = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.SearchPlate = searchPlate;
            ViewBag.Status = status;
            ViewBag.SortBy = sortBy;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.VehicleTypes = new SelectList(await _context.VehicleTypes.ToListAsync(), "Id", "Name");

            return View(vehicles);
        }

        [HttpPost("Save")]
        public async Task<IActionResult> Save(Vehicle model, IFormFile? imageFile)
        {
            string normalizedPlate = (model.Plate ?? "").Trim().ToUpper();
            if (await _context.Vehicles.AnyAsync(v => v.Plate.ToUpper() == normalizedPlate && v.Id != model.Id))
            {
                TempData["Error"] = "Biển số xe đã tồn tại trong hệ thống!";
                return RedirectToAction(nameof(Index));
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var ext = Path.GetExtension(imageFile.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png" };
                if (!allowed.Contains(ext) || imageFile.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "File ảnh chỉ hỗ trợ .jpg, .jpeg, .png và tối đa 5MB!";
                    return RedirectToAction(nameof(Index));
                }

                string folder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "Images");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"vehicle_{Guid.NewGuid():N}{ext}";
                string fullPath = Path.Combine(folder, fileName);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                model.ImageUrl = $"/Images/{fileName}";
            }

            if (model.Id == 0)
            {
                model.Plate = normalizedPlate;
                _context.Vehicles.Add(model);
                TempData["Success"] = "Thêm xe mới thành công!";
            }
            else
            {
                var existing = await _context.Vehicles.FindAsync(model.Id);
                if (existing != null)
                {
                    existing.Plate = normalizedPlate;
                    existing.VehicleTypeId = model.VehicleTypeId;
                    existing.RegistrationExpiry = model.RegistrationExpiry;
                    existing.InsuranceExpiry = model.InsuranceExpiry;
                    existing.Status = model.Status;
                    if (!string.IsNullOrWhiteSpace(model.ImageUrl))
                        existing.ImageUrl = model.ImageUrl;
                    TempData["Success"] = "Cập nhật thông tin xe thành công!";
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle != null)
            {
                if (await _context.Trips.AnyAsync(t => t.VehicleId == id))
                {
                    TempData["Error"] = "Xe đã có chuyến chạy, không thể xóa!";
                }
                else
                {
                    _context.Vehicles.Remove(vehicle);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa xe thành công!";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}