using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Repositories;

namespace HeThongVanTai.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private readonly IImageRepository _imageRepository;

        public ImagesController(IImageRepository imageRepository)
        {
            _imageRepository = imageRepository;
        }

        [Authorize(Roles = AppRoles.AdminOperator)]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var images = await _imageRepository.GetAll();
            return Ok(images);
        }

        [HttpPost]
        [Route("Upload")]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Upload([FromForm] ImageUploadRequestDto request)
        {
            ValidateFileUpload(request);

            if (ModelState.IsValid)
            {
                var imageDomainModel = new Image
                {
                    File = request.File,
                    FileExtension = Path.GetExtension(request.File.FileName).ToLower(),
                    FileSizeInBytes = request.File.Length,
                    FileName = request.FileName ?? request.File.FileName,
                    FileDescription = request.FileDescription
                };

                await _imageRepository.Upload(imageDomainModel);

                return Ok(imageDomainModel);
            }

            return BadRequest(ModelState);
        }

        private void ValidateFileUpload(ImageUploadRequestDto request)
        {
            var allowedExtensions = new string[] { ".jpg", ".jpeg", ".png" };

            if (!allowedExtensions.Contains(Path.GetExtension(request.File.FileName).ToLower()))
            {
                ModelState.AddModelError("file", "Định dạng file không được hỗ trợ. Chỉ cho phép .jpg, .jpeg, .png");
            }

            if (request.File.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("file", "Kích thước file không được vượt quá 5MB");
            }
        }
    }
}