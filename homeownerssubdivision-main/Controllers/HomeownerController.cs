using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Homeowner")]
    public class HomeownerController : BaseController
    {
        private const string DefaultProfileImagePath = "/restnest.png";
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _config;
        private readonly IUserIdentityService _userIdentityService;
        private readonly IProfileImageStorageService _profileImageStorageService;
        private readonly ILogger<HomeownerController> _logger;

        public HomeownerController(
            IDataService data,
            IWebHostEnvironment hostingEnvironment,
            IConfiguration config,
            IUserIdentityService userIdentityService,
            IProfileImageStorageService profileImageStorageService,
            ILogger<HomeownerController> logger) : base(data)
        {
            _hostingEnvironment = hostingEnvironment;
            _config = config;
            _userIdentityService = userIdentityService;
            _profileImageStorageService = profileImageStorageService;
            _logger = logger;
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<SubmitRequestViewModel> BuildSubmitRequestViewModelAsync(int homeownerId)
        {
            var homeownerTask = _data.GetHomeownerByIdAsync(homeownerId);
            var serviceRequestsTask = _data.GetServiceRequestsForHomeownerDashboardAsync(homeownerId);

            await Task.WhenAll(homeownerTask, serviceRequestsTask);

            return new SubmitRequestViewModel
            {
                NewRequest = new ServiceRequest(),
                ServiceRequests = serviceRequestsTask.Result
                    .Where(r => !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList(),
                HomeownerId = homeownerId,
                HomeownerName = homeownerTask.Result?.FullName,
                SuccessMessage = TempData["Success"] as string,
                ErrorMessage = TempData["Error"] as string
            };
        }

        private async Task<HomeownerDashboardHomeViewModel> BuildDashboardHomeViewModelAsync(int homeownerId)
        {
            var homeownerTask = _data.GetHomeownerByIdAsync(homeownerId);
            var activeReservationsTask = _data.GetReservationCountByHomeownerIdAndStatusAsync(homeownerId, "Approved");
            var openServiceRequestsTask = _data.GetOpenServiceRequestCountByHomeownerIdAsync(homeownerId);
            var billingsTask = _data.GetBillingsByHomeownerIdAsync(homeownerId);

            await Task.WhenAll(homeownerTask, activeReservationsTask, openServiceRequestsTask, billingsTask);

            return new HomeownerDashboardHomeViewModel
            {
                HomeownerName = homeownerTask.Result?.FullName ?? GetCurrentUserName() ?? "Homeowner",
                ActiveReservations = activeReservationsTask.Result,
                OpenServiceRequests = openServiceRequestsTask.Result,
                OutstandingBalance = billingsTask.Result
                    .Where(b => !string.Equals(b.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(b => b.Amount)
            };
        }

        private async Task<HomeownerSettingsViewModel> BuildSettingsViewModelAsync(int homeownerId)
        {
            var homeownerTask = _data.GetHomeownerByIdAsync(homeownerId);
            var profileImageTask = _data.GetHomeownerProfileImageAsync(homeownerId);

            await Task.WhenAll(homeownerTask, profileImageTask);

            var homeowner = homeownerTask.Result;
            var profileImagePath = profileImageTask.Result?.ImagePath;

            return new HomeownerSettingsViewModel
            {
                HomeownerId = homeownerId,
                FullName = homeowner?.FullName ?? string.Empty,
                Email = homeowner?.Email ?? string.Empty,
                ContactNumber = homeowner?.ContactNumber ?? string.Empty,
                Address = homeowner?.Address ?? string.Empty,
                BlockLotNumber = homeowner?.BlockLotNumber ?? string.Empty,
                CreatedAt = homeowner?.CreatedAt ?? DateTime.UtcNow,
                IsActive = homeowner?.IsActive ?? false,
                ProfileImageUrl = string.IsNullOrWhiteSpace(profileImagePath)
                    ? DefaultProfileImagePath
                    : profileImagePath,
                IsDevelopment = _hostingEnvironment.IsDevelopment()
            };
        }

        private async Task RefreshHomeownerPrincipalAsync(Homeowner homeowner)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, homeowner.FullName ?? "Unknown Homeowner"),
                new(ClaimTypes.Email, homeowner.Email ?? "unknown@domain.com"),
                new(ClaimTypes.Role, "Homeowner"),
                new("HomeownerID", homeowner.HomeownerID.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            HttpContext.Session.SetInt32("HomeownerID", homeowner.HomeownerID);
        }

        public async Task<IActionResult> Dashboard()
        {
            int homeownerId = GetCurrentHomeownerId();

            var profileImageObj = await _data.GetHomeownerProfileImageAsync(homeownerId);
            var profileImage = profileImageObj?.ImagePath;

            if (string.IsNullOrEmpty(profileImage))
            {
                profileImage = DefaultProfileImagePath;
            }

            ViewData["ProfileImage"] = profileImage;
            var model = await BuildDashboardHomeViewModelAsync(homeownerId);

            if (IsAjaxRequest())
            {
                return PartialView("~/Views/Homeowner/_DashboardHome.cshtml", model);
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfileImage(IFormFile profileImage)
        {
            if (profileImage == null || profileImage.Length == 0)
            {
                return BadRequest(new { message = "Please choose an image file to upload." });
            }

            int homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Unauthorized(new { message = "Your session has expired. Please log in again." });
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp",
                ".gif"
            };

            var extension = Path.GetExtension(profileImage.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Please upload a JPG, PNG, WEBP, or GIF image." });
            }

            if (profileImage.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { message = "Profile images must be 5 MB or smaller." });
            }

            var existingImage = await _data.GetHomeownerProfileImageAsync(homeownerId);
            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            var enforceDailyLimit = !_hostingEnvironment.IsDevelopment();

            if (enforceDailyLimit &&
                existingImage != null &&
                existingImage.LastUpdatedDate.Date == today.Date &&
                existingImage.ChangeCount >= 3)
            {
                return BadRequest(new { message = "You have reached the maximum profile updates for today." });
            }

            try
            {
                var imagePath = await _profileImageStorageService.UploadHomeownerProfileImageAsync(profileImage, homeownerId);

                if (existingImage != null)
                {
                    if (existingImage.LastUpdatedDate.Date != today.Date)
                    {
                        existingImage.ChangeCount = 0;
                        existingImage.LastUpdatedDate = today;
                    }

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
                return Ok(new
                {
                    message = "Profile picture updated successfully.",
                    imagePath,
                    settingsUrl = Url.Action("Settings", "Homeowner"),
                    homeUrl = Url.Action("Dashboard", "Homeowner")
                });
            }
            catch (SupabaseStorageUploadException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Supabase Storage upload failed for homeowner {HomeownerId}. Category={Category}, StorageMessage={StorageMessage}",
                    homeownerId,
                    ex.ErrorCategory,
                    ex.StorageMessage);

                return BadRequest(new
                {
                    message = ex.UserMessage,
                    imageHostError = ex.StorageMessage,
                    category = ex.ErrorCategory
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update profile image for homeowner {HomeownerId}.", homeownerId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Unable to upload your profile picture right now."
                });
            }
        }

        // Display the Submit Request form
        [HttpGet]
        public async Task<IActionResult> SubmitRequest()
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await BuildSubmitRequestViewModelAsync(homeownerId);
            return IsAjaxRequest()
                ? PartialView("~/Views/Service/SubmitRequest.cshtml", viewModel)
                : View("~/Views/Service/SubmitRequest.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(SubmitRequestViewModel model)
        {
            var homeownerId = GetCurrentHomeownerId();

            if (homeownerId == 0)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "You must be logged in to submit a request." });
                }

                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid || model.NewRequest == null)
            {
                var message = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                    .FirstOrDefault() ?? "Please complete all required request fields.";

                if (IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = false,
                        message,
                        validationErrors = BuildValidationErrors()
                    });
                }

                model = await BuildSubmitRequestViewModelAsync(homeownerId);
                model.ErrorMessage = message;
                return View("~/Views/Service/SubmitRequest.cshtml", model);
            }

            model.NewRequest.HomeownerID = homeownerId;
            model.NewRequest.CreatedAt = DateTime.UtcNow;
            model.NewRequest.Category = model.NewRequest.Category?.Trim();
            model.NewRequest.Priority = model.NewRequest.Priority?.Trim();
            model.NewRequest.Description = model.NewRequest.Description?.Trim();

            if (string.IsNullOrEmpty(model.NewRequest.Status))
            {
                model.NewRequest.Status = "Pending";
            }

            var staffByPosition = string.IsNullOrWhiteSpace(model.NewRequest.Category)
                ? new List<Staff>()
                : await _data.GetStaffByPositionAsync(model.NewRequest.Category);

            if (staffByPosition.Any())
            {
                model.NewRequest.AssignedStaffID = staffByPosition.First().StaffID;
            }

            await _data.AddServiceRequestAsync(model.NewRequest);

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    message = "Request submitted successfully."
                });
            }

            TempData["Success"] = "Request submitted successfully!";
            return RedirectToAction("SubmitRequest");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int requestId)
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Json(new { success = false, message = "You must be logged in to cancel a request." });
            }

            var request = await _data.GetServiceRequestByIdAsync(requestId);

            if (request == null)
            {
                return Json(new { success = false, message = "Request not found." });
            }

            if (request.HomeownerID != homeownerId)
            {
                return Json(new { success = false, message = "You cannot cancel this request." });
            }

            if (request.Status != "Pending")
            {
                return Json(new { success = false, message = "Only pending requests can be canceled." });
            }

            // Delete the request from the database
            await _data.DeleteServiceRequestAsync(requestId);

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> LoadServiceRequestList()
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Unauthorized();
            }

            var requests = (await _data.GetServiceRequestsForHomeownerDashboardAsync(homeownerId))
                .Where(r => !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return PartialView("~/Views/Service/_ServiceRequestList.cshtml", requests);
        }







        // View the submitted service requests for the logged-in homeowner
        public async Task<IActionResult> ViewRequest()
        {
            var homeownerId = GetCurrentHomeownerId();
            var serviceRequests = await _data.GetServiceRequestsForHomeownerDashboardAsync(homeownerId);

            return View(serviceRequests.ToList()); // This will show the requests along with their status
        }

        [HttpGet]
        public async Task<IActionResult> Calendar()
        {
            var model = await BuildCalendarViewModelAsync();

            if (IsAjaxRequest())
            {
                return PartialView("Calendar", model);
            }

            return View("Calendar", model);
        }

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildSettingsViewModelAsync(homeownerId);

            return IsAjaxRequest()
                ? PartialView("~/Views/Homeowner/Settings.cshtml", model)
                : View("~/Views/Homeowner/Settings.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(HomeownerSettingsUpdateViewModel model)
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0 || homeownerId != model.HomeownerId)
            {
                return Json(new { success = false, message = "Your session has expired. Please log in again." });
            }

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            var homeowner = await _data.GetHomeownerByIdAsync(homeownerId);
            if (homeowner == null)
            {
                return Json(new { success = false, message = "Homeowner record was not found." });
            }

            homeowner.FullName = model.FullName.Trim();
            homeowner.ContactNumber = model.ContactNumber.Trim();
            homeowner.Address = model.Address.Trim();
            homeowner.BlockLotNumber = model.BlockLotNumber.Trim();

            await _data.UpdateHomeownerAsync(homeowner);
            await RefreshHomeownerPrincipalAsync(homeowner);

            return Json(new
            {
                success = true,
                message = "Settings updated successfully.",
                displayName = homeowner.FullName,
                homeUrl = Url.Action("Dashboard", "Homeowner"),
                settingsUrl = Url.Action("Settings", "Homeowner")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePasswordReset()
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Json(new { success = false, message = "Your session has expired. Please log in again." });
            }

            var homeowner = await _data.GetHomeownerByIdAsync(homeownerId);
            if (homeowner == null || string.IsNullOrWhiteSpace(homeowner.Email))
            {
                return Json(new { success = false, message = "Unable to find an account email for password reset." });
            }

            try
            {
                var resetLink = await _userIdentityService.GeneratePasswordResetLinkAsync(homeowner.Email);
                if (string.IsNullOrWhiteSpace(resetLink))
                {
                    return Json(new { success = false, message = "Password reset link could not be generated right now." });
                }

                return Json(new
                {
                    success = true,
                    message = _hostingEnvironment.IsDevelopment()
                        ? "Password reset link generated successfully."
                        : "Password reset instructions are ready for this account.",
                    resetLink = _hostingEnvironment.IsDevelopment() ? resetLink : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate password reset link for homeowner {HomeownerId}.", homeownerId);
                return Json(new { success = false, message = "Unable to generate a password reset link right now." });
            }
        }

        private async Task<HomeownerEventsViewModel> BuildCalendarViewModelAsync()
        {
            var events = (await _data.GetEventsAsync())
                .OrderBy(e => e.EventDate)
                .ToList();

            var categories = events
                .Select(e => e.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category)
                .ToList();

            categories.Insert(0, "All");

            return new HomeownerEventsViewModel
            {
                Events = events,
                Categories = categories
            };
        }

        private Dictionary<string, string[]> BuildValidationErrors()
        {
            return ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)
                        .ToArray());
        }








    }
}
