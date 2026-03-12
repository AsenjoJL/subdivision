using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : BaseController
    {
        private const string DefaultProfileImagePath = "/restnest.png";
        private readonly IUserIdentityService _userIdentityService;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IProfileImageStorageService _profileImageStorageService;
        private readonly ILogger<StaffController> _logger;

        public StaffController(
            IDataService data,
            IUserIdentityService userIdentityService,
            IWebHostEnvironment hostingEnvironment,
            IProfileImageStorageService profileImageStorageService,
            ILogger<StaffController> logger) : base(data)
        {
            _userIdentityService = userIdentityService;
            _hostingEnvironment = hostingEnvironment;
            _profileImageStorageService = profileImageStorageService;
            _logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string email, string password)
        {
            var staff = await _data.GetStaffByEmailAsync(email);

            if (staff == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var verification = await _userIdentityService.VerifyPasswordWithMigrationAsync(
                new UserIdentityProfile
                {
                    Email = staff.Email ?? string.Empty,
                    Password = password,
                    DisplayName = staff.FullName ?? string.Empty,
                    ExistingUid = staff.FirebaseUid,
                    IsEnabled = staff.IsActive
                },
                staff.PasswordHash);

            if (!verification.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!string.IsNullOrWhiteSpace(verification.FirebaseUid) &&
                !string.Equals(staff.FirebaseUid, verification.FirebaseUid, StringComparison.Ordinal))
            {
                staff.FirebaseUid = verification.FirebaseUid;
            }

            if (!string.IsNullOrWhiteSpace(staff.PasswordHash))
            {
                staff.PasswordHash = string.Empty;
            }

            await _data.UpdateStaffAsync(staff);

            HttpContext.Session.SetInt32("StaffID", staff.StaffID);
            HttpContext.Session.SetString("StaffName", staff.FullName ?? string.Empty);
            HttpContext.Session.SetString("StaffEmail", staff.Email ?? string.Empty);
            HttpContext.Session.SetString("StaffRole", staff.Position ?? string.Empty);

            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Dashboard()
        {
            var staffId = GetCurrentStaffId();
            if (staffId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildDashboardHomeViewModelAsync(staffId);

            if (IsAjaxRequest())
            {
                return PartialView("~/Views/Staff/_DashboardHome.cshtml", model);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Management()
        {
            var staffId = GetCurrentStaffId();
            if (staffId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildTaskManagementViewModelAsync(staffId);
            return PartialView("_ManagementPartial", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRequestStatus(int requestId, string status)
        {
            var staffId = GetCurrentStaffId();
            if (staffId == 0)
            {
                return Json(new { success = false, message = "You must be logged in to update requests." });
            }

            if (!string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Invalid status update." });
                }

                TempData["Error"] = "Invalid status update.";
                return RedirectToAction("Dashboard");
            }

            var currentPosition = GetCurrentStaffPosition();
            var serviceRequest = await _data.GetServiceRequestByIdAsync(requestId);

            if (serviceRequest == null)
            {
                return Json(new { success = false, message = "Request not found." });
            }

            if (!string.Equals(serviceRequest.Category, currentPosition, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "You cannot update requests outside your assignment group." });
            }

            serviceRequest.Status = "Completed";
            serviceRequest.CompletedAt = DateTime.UtcNow;

            await _data.UpdateServiceRequestAsync(serviceRequest);

            if (IsAjaxRequest())
            {
                return Json(new { success = true, message = "Request marked as completed." });
            }

            TempData["Success"] = "Request marked as completed!";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Profile()
        {
            var staffId = GetCurrentStaffId();
            if (staffId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildProfileViewModelAsync(staffId);
            return IsAjaxRequest()
                ? PartialView("Profile", model)
                : View("Profile", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(StaffProfileUpdateViewModel model)
        {
            var staffId = GetCurrentStaffId();
            if (staffId == 0 || staffId != model.StaffId)
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

            var staff = await _data.GetStaffByIdAsync(staffId);
            if (staff == null)
            {
                return Json(new { success = false, message = "Staff record was not found." });
            }

            var fullName = model.FullName.Trim();
            var email = model.Email.Trim();
            var phoneNumber = model.PhoneNumber.Trim();

            var adminsTask = _data.GetAdminsAsync();
            var homeownerTask = _data.GetHomeownerByEmailAsync(email);
            var staffTask = _data.GetStaffByEmailAsync(email);
            await Task.WhenAll(adminsTask, homeownerTask, staffTask);

            if (adminsTask.Result.Any(existing =>
                    string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, message = "This email is already registered.", field = nameof(model.Email) });
            }

            if (homeownerTask.Result != null)
            {
                return Json(new { success = false, message = "This email is already used by another account.", field = nameof(model.Email) });
            }

            if (staffTask.Result != null && staffTask.Result.StaffID != staff.StaffID)
            {
                return Json(new { success = false, message = "This email is already registered.", field = nameof(model.Email) });
            }

            staff.FullName = fullName;
            staff.Email = email;
            staff.PhoneNumber = phoneNumber;
            staff.FirebaseUid = await _userIdentityService.SyncUserProfileAsync(new UserIdentityProfile
            {
                ExistingUid = staff.FirebaseUid,
                Email = staff.Email ?? string.Empty,
                DisplayName = staff.FullName ?? string.Empty,
                IsEnabled = staff.IsActive
            }) ?? staff.FirebaseUid;

            await _data.UpdateStaffAsync(staff);
            await RefreshStaffPrincipalAsync(staff);

            return Json(new
            {
                success = true,
                message = "Staff profile updated successfully.",
                staffName = staff.FullName,
                email = staff.Email,
                phoneNumber = staff.PhoneNumber,
                dashboardUrl = Url.Action("Dashboard", "Staff"),
                profileUrl = Url.Action("Profile", "Staff")
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfileImage(IFormFile profileImage)
        {
            if (profileImage == null || profileImage.Length == 0)
            {
                return BadRequest(new { message = "Please choose an image file to upload." });
            }

            var staffId = GetCurrentStaffId();
            if (staffId == 0)
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

            var staff = await _data.GetStaffByIdAsync(staffId);
            if (staff == null)
            {
                return BadRequest(new { message = "Staff record was not found." });
            }

            try
            {
                var imagePath = await _profileImageStorageService.UploadStaffProfileImageAsync(profileImage, staffId);
                staff.ProfileImageUrl = imagePath;
                await _data.UpdateStaffAsync(staff);

                return Ok(new
                {
                    message = "Profile picture updated successfully.",
                    imagePath,
                    dashboardUrl = Url.Action("Dashboard", "Staff"),
                    profileUrl = Url.Action("Profile", "Staff")
                });
            }
            catch (SupabaseStorageUploadException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Supabase Storage upload failed for staff {StaffId}. Category={Category}, StorageMessage={StorageMessage}",
                    staffId,
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
                _logger.LogError(ex, "Failed to update profile image for staff {StaffId}.", staffId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Unable to upload your profile picture right now."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePasswordReset()
        {
            var staffId = GetCurrentStaffId();
            if (staffId == 0)
            {
                return Json(new { success = false, message = "Your session has expired. Please log in again." });
            }

            var staff = await _data.GetStaffByIdAsync(staffId);
            if (staff == null || string.IsNullOrWhiteSpace(staff.Email))
            {
                return Json(new { success = false, message = "Unable to find an account email for password reset." });
            }

            try
            {
                var resetLink = await _userIdentityService.GeneratePasswordResetLinkAsync(staff.Email);
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
                _logger.LogError(ex, "Failed to generate password reset link for staff {StaffId}.", staffId);
                return Json(new { success = false, message = "Unable to generate a password reset link right now." });
            }
        }

        public IActionResult UnauthorizedAccess()
        {
            return View("UnauthorizedAccess");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<StaffDashboardHomeViewModel> BuildDashboardHomeViewModelAsync(int staffId)
        {
            var staff = await _data.GetStaffByIdAsync(staffId);
            var position = staff?.Position ?? GetCurrentStaffPosition() ?? "Unknown";
            var matchingRequests = await GetRequestsForPositionAsync(position);

            var pendingCount = matchingRequests.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            var completedCount = matchingRequests.Count(r => string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            return new StaffDashboardHomeViewModel
            {
                StaffName = staff?.FullName ?? GetCurrentUserName() ?? "Staff",
                Position = position,
                ProfileImageUrl = string.IsNullOrWhiteSpace(staff?.ProfileImageUrl) ? DefaultProfileImagePath : staff!.ProfileImageUrl!,
                PendingCount = pendingCount,
                CompletedCount = completedCount,
                TotalCount = matchingRequests.Count
            };
        }

        private async Task<StaffTaskManagementViewModel> BuildTaskManagementViewModelAsync(int staffId)
        {
            var staff = await _data.GetStaffByIdAsync(staffId);
            var position = staff?.Position ?? GetCurrentStaffPosition() ?? "Unknown";
            var matchingRequests = await GetRequestsForPositionAsync(position);

            return new StaffTaskManagementViewModel
            {
                StaffName = staff?.FullName ?? GetCurrentUserName() ?? "Staff",
                Position = position,
                PendingRequests = matchingRequests
                    .Where(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList(),
                CompletedRequests = matchingRequests
                    .Where(r => string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.CompletedAt ?? r.CreatedAt)
                    .ToList()
            };
        }

        private async Task<List<ServiceRequest>> GetRequestsForPositionAsync(string? position)
        {
            if (string.IsNullOrWhiteSpace(position))
            {
                return new List<ServiceRequest>();
            }

            var allRequests = await _data.GetServiceRequestsAsync();
            return allRequests
                .Where(r => string.Equals(r.Category, position, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private async Task<StaffProfileViewModel> BuildProfileViewModelAsync(int staffId)
        {
            var staff = await _data.GetStaffByIdAsync(staffId);

            return new StaffProfileViewModel
            {
                StaffId = staffId,
                FullName = staff?.FullName ?? string.Empty,
                Email = staff?.Email ?? string.Empty,
                PhoneNumber = staff?.PhoneNumber ?? string.Empty,
                Position = staff?.Position ?? GetCurrentStaffPosition() ?? "Unknown",
                IsActive = staff?.IsActive ?? false,
                CreatedAt = staff?.CreatedAt ?? DateTime.UtcNow,
                ProfileImageUrl = string.IsNullOrWhiteSpace(staff?.ProfileImageUrl) ? DefaultProfileImagePath : staff.ProfileImageUrl!,
                IsDevelopment = _hostingEnvironment.IsDevelopment()
            };
        }

        private async Task RefreshStaffPrincipalAsync(Staff staff)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, staff.FullName ?? "Staff"),
                new(ClaimTypes.Email, staff.Email ?? string.Empty),
                new(ClaimTypes.Role, "Staff"),
                new("StaffID", staff.StaffID.ToString()),
                new("Position", staff.Position ?? string.Empty)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            HttpContext.Session.SetInt32("StaffID", staff.StaffID);
            HttpContext.Session.SetString("StaffName", staff.FullName ?? string.Empty);
            HttpContext.Session.SetString("StaffEmail", staff.Email ?? string.Empty);
            HttpContext.Session.SetString("StaffRole", staff.Position ?? string.Empty);
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
