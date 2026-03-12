using HOMEOWNER.Data;
using HOMEOWNER.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Homeowner")]
    public class HomeownerProfileImageController : BaseController
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeownerProfileImageController(IDataService data, IWebHostEnvironment webHostEnvironment) : base(data)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpPost]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Unauthorized("Homeowner not found.");
            }

            var existingImage = await _data.GetHomeownerProfileImageAsync(homeownerId);
            var today = DateTime.UtcNow.Date;

            if (existingImage != null)
            {
                if (existingImage.LastUpdatedDate < today)
                {
                    existingImage.ChangeCount = 0;
                    existingImage.LastUpdatedDate = today;
                }

                if (existingImage.ChangeCount >= 3)
                {
                    return BadRequest("You can only change your profile picture 3 times per day.");
                }
            }

            var fileName = $"homeowner_{homeownerId}{Path.GetExtension(file.FileName)}";
            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profile_pictures");

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var filePath = Path.Combine(uploadPath, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            var imagePath = $"/uploads/profile_pictures/{fileName}";

            if (existingImage != null)
            {
                existingImage.ImagePath = imagePath;
                existingImage.UploadedAt = DateTime.UtcNow;
                existingImage.ChangeCount += 1;
            }
            else
            {
                existingImage = new HomeownerProfileImage
                {
                    HomeownerID = homeownerId,
                    ImagePath = imagePath,
                    UploadedAt = DateTime.UtcNow,
                    ChangeCount = 1,
                    LastUpdatedDate = today
                };
            }

            await _data.AddOrUpdateHomeownerProfileImageAsync(existingImage);
            return Ok(new { imagePath });
        }
    }
}
