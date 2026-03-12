using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;




using Microsoft.AspNetCore.Authorization;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private const string DefaultProfileImagePath = "/restnest.png";
        private static readonly List<KeyValuePair<string, string>> WorkspaceSectionOptions = new()
        {
            new("dashboard", "Dashboard"),
            new("homeowners", "Homeowners"),
            new("staff", "Staff"),
            new("serviceRequests", "Service requests"),
            new("reservations", "Reservations"),
            new("billing", "Billing & payments"),
            new("analytics", "Analytics dashboard")
        };
        private readonly IUserIdentityService _userIdentityService;
        private readonly ICommunityNotificationService _notificationService;
        private readonly IAppFileStorageService _fileStorageService;
        private readonly ILogger<AdminController> _logger;


        public AdminController(
            IDataService data,
            IUserIdentityService userIdentityService,
            ICommunityNotificationService notificationService,
            IAppFileStorageService fileStorageService,
            ILogger<AdminController> logger) : base(data)
        {
            _userIdentityService = userIdentityService;
            _notificationService = notificationService;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard()
        {
            var workspaceSettings = await BuildAdminWorkspaceSettingsViewModelAsync();
            ViewBag.AdminWorkspaceName = workspaceSettings.WorkspaceName;
            ViewBag.AdminDefaultLandingSection = workspaceSettings.DefaultLandingSection;
            ViewBag.AdminUseCompactTables = workspaceSettings.UseCompactTables;
            ViewBag.AdminEnableSectionPrefetch = workspaceSettings.EnableSectionPrefetch;
            return View();
        }

        private async Task<Admin?> GetCurrentAdminRecordAsync()
        {
            var adminId = GetCurrentAdminId();
            if (adminId > 0)
            {
                var admin = await _data.GetAdminByIdAsync(adminId);
                if (admin != null)
                {
                    return admin;
                }
            }

            var email = GetCurrentUserEmail();
            if (!string.IsNullOrWhiteSpace(email))
            {
                return await _data.GetAdminByEmailAsync(email);
            }

            return null;
        }

        private async Task<AdminWorkspaceSettings> GetOrCreateAdminWorkspaceSettingsAsync()
        {
            var settings = await _data.GetAdminWorkspaceSettingsAsync();
            return settings ?? new AdminWorkspaceSettings
            {
                WorkspaceName = "Admin workspace",
                DefaultLandingSection = "dashboard",
                UseCompactTables = false,
                EnableSectionPrefetch = true,
                LastUpdated = DateTime.UtcNow
            };
        }

        private async Task<AdminWorkspaceSettingsViewModel> BuildAdminWorkspaceSettingsViewModelAsync()
        {
            var settings = await GetOrCreateAdminWorkspaceSettingsAsync();
            return new AdminWorkspaceSettingsViewModel
            {
                WorkspaceName = string.IsNullOrWhiteSpace(settings.WorkspaceName) ? "Admin workspace" : settings.WorkspaceName,
                DefaultLandingSection = string.IsNullOrWhiteSpace(settings.DefaultLandingSection) ? "dashboard" : settings.DefaultLandingSection,
                UseCompactTables = settings.UseCompactTables,
                EnableSectionPrefetch = settings.EnableSectionPrefetch,
                AvailableSections = WorkspaceSectionOptions
            };
        }

        [HttpGet]
        public async Task<IActionResult> AdminProfile()
        {
            var admin = await GetCurrentAdminRecordAsync();
            if (admin == null)
            {
                return PartialView("_AdminModuleError", "Unable to load the current admin profile.");
            }

            var viewModel = new AdminProfileSettingsViewModel
            {
                AdminID = admin.AdminID,
                FullName = admin.FullName,
                Email = admin.Email,
                OfficeLocation = admin.OfficeLocation,
                Status = admin.Status
            };

            return PartialView(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAdminProfile(AdminProfileSettingsUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage).ToArray());

                return Json(new { success = false, message = "Please correct the highlighted fields.", validationErrors });
            }

            var admin = await GetCurrentAdminRecordAsync();
            if (admin == null || admin.AdminID != model.AdminID)
            {
                return Json(new { success = false, message = "Unable to update the selected admin profile." });
            }

            var fullName = model.FullName.Trim();
            var email = model.Email.Trim();
            var officeLocation = model.OfficeLocation.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Json(new { success = false, message = "Full name is required.", field = nameof(model.FullName) });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Email is required.", field = nameof(model.Email) });
            }

            if (string.IsNullOrWhiteSpace(officeLocation))
            {
                return Json(new { success = false, message = "Office location is required.", field = nameof(model.OfficeLocation) });
            }

            var adminsTask = _data.GetAdminsAsync();
            var homeownerTask = _data.GetHomeownerByEmailAsync(email);
            var staffTask = _data.GetStaffByEmailAsync(email);
            await Task.WhenAll(adminsTask, homeownerTask, staffTask);

            if (adminsTask.Result.Any(existing => existing.AdminID != admin.AdminID && string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, message = "This email is already registered.", field = nameof(model.Email) });
            }

            if (homeownerTask.Result != null || staffTask.Result != null)
            {
                return Json(new { success = false, message = "This email is already used by another account.", field = nameof(model.Email) });
            }

            admin.FullName = fullName;
            admin.Email = email;
            admin.OfficeLocation = officeLocation;
            admin.FirebaseUid = await _userIdentityService.SyncUserProfileAsync(new UserIdentityProfile
            {
                ExistingUid = admin.FirebaseUid,
                Email = admin.Email,
                DisplayName = admin.FullName,
                IsEnabled = string.Equals(admin.Status, "Active", StringComparison.OrdinalIgnoreCase)
            }) ?? admin.FirebaseUid;

            await _data.UpdateAdminAsync(admin);

            return Json(new
            {
                success = true,
                message = "Admin profile updated successfully!",
                adminName = admin.FullName,
                email = admin.Email,
                officeLocation = admin.OfficeLocation
            });
        }

        [HttpGet]
        public async Task<IActionResult> WorkspaceSettings()
        {
            var viewModel = await BuildAdminWorkspaceSettingsViewModelAsync();
            return PartialView(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateWorkspaceSettings(AdminWorkspaceSettingsUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage).ToArray());

                return Json(new { success = false, message = "Please correct the highlighted fields.", validationErrors });
            }

            var workspaceName = model.WorkspaceName.Trim();
            if (string.IsNullOrWhiteSpace(workspaceName))
            {
                return Json(new { success = false, message = "Workspace name is required.", field = nameof(model.WorkspaceName) });
            }

            if (!WorkspaceSectionOptions.Any(option => string.Equals(option.Key, model.DefaultLandingSection, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, message = "Select a valid default landing section.", field = nameof(model.DefaultLandingSection) });
            }

            var settings = await GetOrCreateAdminWorkspaceSettingsAsync();
            settings.WorkspaceName = workspaceName;
            settings.DefaultLandingSection = model.DefaultLandingSection;
            settings.UseCompactTables = model.UseCompactTables;
            settings.EnableSectionPrefetch = model.EnableSectionPrefetch;
            settings.LastUpdated = DateTime.UtcNow;

            await _data.AddOrUpdateAdminWorkspaceSettingsAsync(settings);

            return Json(new
            {
                success = true,
                message = "Workspace settings updated successfully!",
                workspaceSettings = new
                {
                    workspaceName = settings.WorkspaceName,
                    defaultLandingSection = settings.DefaultLandingSection,
                    useCompactTables = settings.UseCompactTables,
                    enableSectionPrefetch = settings.EnableSectionPrefetch
                }
            });
        }

        private async Task<List<AdminHomeownerListItemViewModel>> BuildAdminHomeownerListItemsAsync()
        {
            var homeownersTask = _data.GetHomeownersAsync();
            var profileImagesTask = _data.GetHomeownerProfileImagesAsync();

            await Task.WhenAll(homeownersTask, profileImagesTask);

            var profileImageMap = profileImagesTask.Result
                .GroupBy(image => image.HomeownerID)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(image => image.UploadedAt)
                        .Select(image => image.ImagePath)
                        .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? DefaultProfileImagePath);

            return homeownersTask.Result
                .Select(homeowner => new AdminHomeownerListItemViewModel
                {
                    HomeownerID = homeowner.HomeownerID,
                    FullName = homeowner.FullName ?? string.Empty,
                    Email = homeowner.Email ?? string.Empty,
                    ContactNumber = homeowner.ContactNumber ?? string.Empty,
                    Address = homeowner.Address ?? string.Empty,
                    BlockLotNumber = homeowner.BlockLotNumber ?? string.Empty,
                    Role = homeowner.Role ?? "Homeowner",
                    ProfileImageUrl = profileImageMap.TryGetValue(homeowner.HomeownerID, out var imagePath) && !string.IsNullOrWhiteSpace(imagePath)
                        ? imagePath
                        : DefaultProfileImagePath
                })
                .ToList();
        }

        private async Task<List<AdminStaffListItemViewModel>> BuildAdminStaffListItemsAsync()
        {
            var staffList = await _data.GetStaffAsync();

            return staffList
                .Select(staff => new AdminStaffListItemViewModel
                {
                    StaffID = staff.StaffID,
                    FullName = staff.FullName ?? string.Empty,
                    Email = staff.Email ?? string.Empty,
                    PhoneNumber = staff.PhoneNumber ?? string.Empty,
                    Position = staff.Position ?? string.Empty,
                    ProfileImageUrl = staff.ProfileImageUrl ?? string.Empty
                })
                .ToList();
        }

        [HttpGet]
        public async Task<IActionResult> DashboardStats()
        {
            var homeownerCountTask = _data.GetHomeownerCountAsync("Homeowner");
            var staffCountTask = _data.GetStaffCountAsync();
            var reservationCountTask = _data.GetReservationCountByStatusAsync("Approved");
            var notificationCountTask = _data.GetNotificationCountAsync();

            await Task.WhenAll(homeownerCountTask, staffCountTask, reservationCountTask, notificationCountTask);

            return Json(new
            {
                homeownerCount = homeownerCountTask.Result,
                staffCount = staffCountTask.Result,
                reservationCount = reservationCountTask.Result,
                notificationCount = notificationCountTask.Result
            });
        }

        [HttpGet]
        public async Task<IActionResult> DashboardOverviewData()
        {
            var overview = await BuildDashboardOverviewAsync();
            return Json(overview);
        }





        public async Task<IActionResult> ManageOwners()
        {
            var homeowners = await BuildAdminHomeownerListItemsAsync();
            return PartialView("_ManageOwners", homeowners);
        }

        [HttpGet]
        public async Task<IActionResult> LoadHomeownersList()
        {
            var homeowners = await BuildAdminHomeownerListItemsAsync();
            return PartialView("_HomeownersTableRows", homeowners);
        }

        [HttpGet]
        public async Task<IActionResult> EditOwner(int id)
        {
            var homeowner = await _data.GetHomeownerByIdAsync(id);
            if (homeowner == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    homeowner = new
                    {
                        homeownerID = homeowner.HomeownerID,
                        fullName = homeowner.FullName,
                        email = homeowner.Email,
                        contactNumber = homeowner.ContactNumber,
                        address = homeowner.Address,
                        blockLotNumber = homeowner.BlockLotNumber
                    }
                });
            }

            return RedirectToAction(nameof(ManageOwners));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOwner(EditHomeownerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors
                            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)
                            .ToArray());

                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors
                });
            }

            var homeowner = await _data.GetHomeownerByIdAsync(model.HomeownerID);
            if (homeowner == null)
            {
                return NotFound();
            }

            var fullName = model.FullName.Trim();
            var email = model.Email.Trim();
            var address = model.Address.Trim();
            var blockLotNumber = model.BlockLotNumber.Trim();
            var contactNumber = model.ContactNumber?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Json(new { success = false, message = "Full name is required.", field = nameof(model.FullName) });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Email is required.", field = nameof(model.Email) });
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return Json(new { success = false, message = "Address is required.", field = nameof(model.Address) });
            }

            if (string.IsNullOrWhiteSpace(blockLotNumber))
            {
                return Json(new { success = false, message = "Block/Lot Number is required.", field = nameof(model.BlockLotNumber) });
            }

            var homeownersTask = _data.GetHomeownersAsync();
            var adminTask = _data.GetAdminByEmailAsync(email);
            var staffTask = _data.GetStaffByEmailAsync(email);

            await Task.WhenAll(homeownersTask, adminTask, staffTask);

            if (homeownersTask.Result.Any(h =>
                h.HomeownerID != model.HomeownerID &&
                string.Equals(h.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, message = "This email is already registered.", field = nameof(model.Email) });
            }

            if (adminTask.Result != null || staffTask.Result != null)
            {
                return Json(new { success = false, message = "This email is already used by another account.", field = nameof(model.Email) });
            }

            if (homeownersTask.Result.Any(h =>
                h.HomeownerID != model.HomeownerID &&
                string.Equals(h.BlockLotNumber, blockLotNumber, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, message = "This Block & Lot Number is already taken.", field = nameof(model.BlockLotNumber) });
            }

            homeowner.FullName = fullName;
            homeowner.Email = email;
            homeowner.Address = address;
            homeowner.BlockLotNumber = blockLotNumber;
            homeowner.ContactNumber = contactNumber;

            homeowner.FirebaseUid = await _userIdentityService.SyncUserProfileAsync(new UserIdentityProfile
            {
                ExistingUid = homeowner.FirebaseUid,
                Email = homeowner.Email ?? string.Empty,
                DisplayName = homeowner.FullName ?? string.Empty,
                IsEnabled = homeowner.IsActive
            }) ?? homeowner.FirebaseUid;

            await _data.UpdateHomeownerAsync(homeowner);

            return Json(new
            {
                success = true,
                message = "Homeowner updated successfully!",
                homeowner = new
                {
                    homeownerID = homeowner.HomeownerID,
                    fullName = homeowner.FullName,
                    email = homeowner.Email,
                    contactNumber = homeowner.ContactNumber,
                    address = homeowner.Address,
                    blockLotNumber = homeowner.BlockLotNumber,
                    role = homeowner.Role
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOwner(int id)
        {
            var homeowner = await _data.GetHomeownerByIdAsync(id);
            if (homeowner == null)
            {
                return Json(new { success = false, message = "Homeowner not found." });
            }

            await _userIdentityService.DeleteUserAsync(homeowner.FirebaseUid, homeowner.Email);
            await _data.DeleteHomeownerAsync(id);

            return Json(new { success = true, message = "Homeowner deleted successfully." });
        }


        public IActionResult AddOwner()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOwner(AddHomeownerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors
                            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)
                            .ToArray());

                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors
                });
            }

            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                return Json(new { success = false, message = "Full name is required." });
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                return Json(new { success = false, message = "Email is required." });
            }

            if (string.IsNullOrWhiteSpace(model.Address))
            {
                return Json(new { success = false, message = "Address is required." });
            }

            if (string.IsNullOrWhiteSpace(model.BlockLotNumber))
            {
                return Json(new { success = false, message = "Block/Lot Number is required." });
            }

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, message = "Password is required." });
            }

            try
            {
                var fullName = model.FullName.Trim();
                var email = model.Email.Trim();
                var address = model.Address.Trim();
                var blockLotNumber = model.BlockLotNumber.Trim();
                var contactNumber = model.ContactNumber?.Trim() ?? string.Empty;
                var plainPassword = model.Password;

                var homeownersTask = _data.GetHomeownersAsync();
                var adminTask = _data.GetAdminByEmailAsync(email);
                var staffTask = _data.GetStaffByEmailAsync(email);

                await Task.WhenAll(homeownersTask, adminTask, staffTask);
                var homeowners = homeownersTask.Result;

                if (homeowners.Any(h => string.Equals(h.Email, email, StringComparison.OrdinalIgnoreCase)))
                {
                    return Json(new
                    {
                        success = false,
                        message = "This email is already registered.",
                        field = nameof(model.Email)
                    });
                }

                if (adminTask.Result != null || staffTask.Result != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This email is already used by another account.",
                        field = nameof(model.Email)
                    });
                }

                if (homeowners.Any(h => string.Equals(h.BlockLotNumber, blockLotNumber, StringComparison.OrdinalIgnoreCase)))
                {
                    return Json(new
                    {
                        success = false,
                        message = "This Block & Lot Number is already taken.",
                        field = nameof(model.BlockLotNumber)
                    });
                }

                var currentAdminId = GetCurrentAdminID();
                if (currentAdminId == 0)
                {
                    return Json(new { success = false, message = "Unable to resolve the current admin account." });
                }

                var homeowner = new Homeowner
                {
                    FullName = fullName,
                    Email = email,
                    Address = address,
                    BlockLotNumber = blockLotNumber,
                    ContactNumber = contactNumber,
                    PasswordHash = string.Empty,
                    AdminID = currentAdminId,
                    Role = "Homeowner",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                homeowner.FirebaseUid = await _userIdentityService.EnsureUserAsync(new UserIdentityProfile
                {
                    Email = homeowner.Email,
                    Password = plainPassword,
                    DisplayName = homeowner.FullName,
                    IsEnabled = homeowner.IsActive
                });

                await _data.AddHomeownerAsync(homeowner);

                try
                {
                    var loginUrl = Url.Action("Login", "Account", values: null, protocol: Request.Scheme)
                        ?? "/Account/Login";

                    await _notificationService.NotifyHomeownerAccountCreatedAsync(
                        homeowner,
                        plainPassword,
                        loginUrl,
                        HttpContext.RequestAborted);
                }
                catch (Exception notificationEx)
                {
                    _logger.LogWarning(
                        notificationEx,
                        "Homeowner {HomeownerId} was created, but onboarding email/SMS delivery failed.",
                        homeowner.HomeownerID);
                }

                return Json(new
                {
                    success = true,
                    message = "Homeowner added successfully! An email/SMS notification was queued if messaging is configured.",
                    homeowner = new
                    {
                        id = homeowner.HomeownerID,
                        homeownerID = homeowner.HomeownerID,
                        fullName = homeowner.FullName,
                        email = homeowner.Email,
                        address = homeowner.Address,
                        contactNumber = homeowner.ContactNumber,
                        blockLotNumber = homeowner.BlockLotNumber,
                        role = homeowner.Role
                    }
                });
            }
            catch (Exception ex)
            {
                var normalizedMessage = ex.Message;
                var targetField = string.Empty;

                if (normalizedMessage.Contains("email", StringComparison.OrdinalIgnoreCase) &&
                    normalizedMessage.Contains("exist", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedMessage = "This email is already used by another account.";
                    targetField = nameof(model.Email);
                }

                return Json(new
                {
                    success = false,
                    message = $"Error adding homeowner: {normalizedMessage}",
                    field = targetField
                });
            }
        }



        public async Task<IActionResult> ManageStaff()
        {
            var staffList = await BuildAdminStaffListItemsAsync();
            return PartialView("ManageStaff", staffList);
        }

        public async Task<IActionResult> LoadStaffList()
        {
            var allStaff = await BuildAdminStaffListItemsAsync();
            return PartialView("_StaffList", allStaff);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStaff(AddStaffViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors
                            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)
                            .ToArray());

                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors
                });
            }

            if (string.IsNullOrWhiteSpace(model?.FullName))
            {
                return Json(new { success = false, message = "Full Name is required." });
            }
            if (string.IsNullOrWhiteSpace(model?.Email))
            {
                return Json(new { success = false, message = "Email is required." });
            }
            if (string.IsNullOrWhiteSpace(model?.PhoneNumber))
            {
                return Json(new { success = false, message = "Phone Number is required." });
            }
            if (string.IsNullOrWhiteSpace(model?.Position))
            {
                return Json(new { success = false, message = "Position is required." });
            }
            if (string.IsNullOrWhiteSpace(model?.Password))
            {
                return Json(new { success = false, message = "Password is required." });
            }

            try
            {
                var fullName = model.FullName.Trim();
                var email = model.Email.Trim();
                var phoneNumber = model.PhoneNumber.Trim();
                var position = model.Position.Trim();
                var plainPassword = model.Password;

                var currentAdminId = GetCurrentAdminID();
                if (currentAdminId == 0)
                {
                    return Json(new { success = false, message = "Unable to resolve the current admin account." });
                }

                var existingStaffTask = _data.GetStaffByEmailAsync(email);
                var homeownerTask = _data.GetHomeownerByEmailAsync(email);
                var adminTask = _data.GetAdminByEmailAsync(email);

                await Task.WhenAll(existingStaffTask, homeownerTask, adminTask);

                if (existingStaffTask.Result != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This email is already registered.",
                        field = nameof(model.Email)
                    });
                }

                if (homeownerTask.Result != null || adminTask.Result != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This email is already used by another account.",
                        field = nameof(model.Email)
                    });
                }

                var staff = new Staff
                {
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = phoneNumber,
                    Position = position,
                    PasswordHash = string.Empty,
                    AdminID = currentAdminId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                staff.FirebaseUid = await _userIdentityService.EnsureUserAsync(new UserIdentityProfile
                {
                    Email = staff.Email,
                    Password = plainPassword,
                    DisplayName = staff.FullName,
                    IsEnabled = staff.IsActive
                });

                // StaffID will be generated in AddStaffAsync if it's 0
                staff.StaffID = 0;

                await _data.AddStaffAsync(staff);

                // Get the saved staff by email to verify it was saved
                var savedStaff = await _data.GetStaffByEmailAsync(staff.Email);
                if (savedStaff == null)
                {
                    return Json(new { success = false, message = "Staff was saved but could not be retrieved. Please refresh the page." });
                }

                return Json(new { success = true, message = "Staff added successfully!", staff = savedStaff });
            }
            catch (Exception ex)
            {
                var normalizedMessage = ex.Message;
                var targetField = string.Empty;

                if (normalizedMessage.Contains("email", StringComparison.OrdinalIgnoreCase) &&
                    normalizedMessage.Contains("exist", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedMessage = "This email is already used by another account.";
                    targetField = nameof(model.Email);
                }

                return Json(new
                {
                    success = false,
                    message = $"Error adding staff: {normalizedMessage}",
                    field = targetField
                });
            }
        }





        public async Task<IActionResult> EditStaff(int id)
        {
            var staff = await _data.GetStaffByIdAsync(id);
            if (staff == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    staff = new
                    {
                        staffID = staff.StaffID,
                        fullName = staff.FullName,
                        email = staff.Email,
                        phoneNumber = staff.PhoneNumber,
                        position = staff.Position
                    }
                });
            }

            return RedirectToAction(nameof(ManageStaff));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(EditStaffViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors
                            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)
                            .ToArray());

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please correct the highlighted fields.",
                        validationErrors
                    });
                }

                return RedirectToAction(nameof(ManageStaff));
            }

            var staff = await _data.GetStaffByIdAsync(model.StaffID);
            if (staff == null)
            {
                return NotFound();
            }

            var fullName = model.FullName.Trim();
            var email = model.Email.Trim();
            var phoneNumber = model.PhoneNumber.Trim();
            var position = model.Position.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Json(new { success = false, message = "Full Name is required.", field = nameof(model.FullName) });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Email is required.", field = nameof(model.Email) });
            }

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return Json(new { success = false, message = "Phone Number is required.", field = nameof(model.PhoneNumber) });
            }

            if (string.IsNullOrWhiteSpace(position))
            {
                return Json(new { success = false, message = "Position is required.", field = nameof(model.Position) });
            }

            var existingStaffTask = _data.GetStaffByEmailAsync(email);
            var homeownerTask = _data.GetHomeownerByEmailAsync(email);
            var adminTask = _data.GetAdminByEmailAsync(email);

            await Task.WhenAll(existingStaffTask, homeownerTask, adminTask);

            if (existingStaffTask.Result != null && existingStaffTask.Result.StaffID != model.StaffID)
            {
                return Json(new { success = false, message = "This email is already registered.", field = nameof(model.Email) });
            }

            if (homeownerTask.Result != null || adminTask.Result != null)
            {
                return Json(new { success = false, message = "This email is already used by another account.", field = nameof(model.Email) });
            }

            staff.FullName = fullName;
            staff.Email = email;
            staff.PhoneNumber = phoneNumber;
            staff.Position = position;

            staff.FirebaseUid = await _userIdentityService.SyncUserProfileAsync(new UserIdentityProfile
            {
                ExistingUid = staff.FirebaseUid,
                Email = staff.Email ?? string.Empty,
                DisplayName = staff.FullName ?? string.Empty,
                IsEnabled = staff.IsActive
            }) ?? staff.FirebaseUid;

            await _data.UpdateStaffAsync(staff);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    message = "Staff updated successfully!",
                    staff = new
                    {
                        staffID = staff.StaffID,
                        fullName = staff.FullName,
                        email = staff.Email,
                        phoneNumber = staff.PhoneNumber,
                        position = staff.Position
                    }
                });
            }

            return RedirectToAction(nameof(ManageStaff));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStaff(int id)
        {
            var staff = await _data.GetStaffByIdAsync(id);
            if (staff == null)
            {
                return Json(new { success = false, message = "Staff member not found." });
            }

            await _userIdentityService.DeleteUserAsync(staff.FirebaseUid, staff.Email);
            await _data.DeleteStaffAsync(id);

            return Json(new { success = true, message = "Staff member deleted successfully." });
        }

        public IActionResult FacilitiesAndReservations()
        {
            var model = BuildReservationManagementShellViewModel();
            return View("ReservationManagement", model);
        }

        [HttpGet]
        public IActionResult ReservationManagement()
        {
            var model = BuildReservationManagementShellViewModel();
            return PartialView("ReservationManagement", model);
        }

        [HttpGet]
        public async Task<IActionResult> ReservationManagementSummary()
        {
            var model = await BuildReservationManagementModuleAsync();
            return Json(new
            {
                totalReservations = model.TotalReservations,
                approvedCount = model.ApprovedCount,
                pendingCount = model.PendingCount,
                rejectedCount = model.RejectedCount,
                cancelledCount = model.CancelledCount,
                expiredCount = model.ExpiredCount,
                approvalRate = model.ApprovalRate,
                popularFacility = model.PopularFacility,
                popularFacilityCount = model.PopularFacilityCount,
                peakDay = model.PeakDay,
                peakDayCount = model.PeakDayCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> LoadFacilitiesList()
        {
            try
            {
                var facilities = await _data.GetFacilitiesAsync();
                return PartialView("_FacilityTableRows", facilities.OrderBy(f => f.FacilityName).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading facilities list for reservation management.");
                return PartialView("_FacilityTableRows", new List<Facility>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFacilitiesLookup()
        {
            var facilities = await _data.GetFacilitiesAsync();
            return Json(facilities
                .OrderBy(f => f.FacilityName)
                .Select(f => new
                {
                    facilityID = f.FacilityID,
                    facilityName = f.FacilityName ?? "Unknown facility"
                }));
        }

        [HttpGet]
        public async Task<IActionResult> LoadReservationsList()
        {
            try
            {
                var reservations = await GetActiveReservationsAsync();
                return PartialView("_ReservationTableRows", reservations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reservation list for reservation management.");
                return PartialView("_ReservationTableRows", new List<Reservation>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFacility(int facilityId)
        {
            var facility = await _data.GetFacilityByIdAsync(facilityId);
            if (facility == null)
            {
                return Json(new { success = false, message = "Facility not found." });
            }

            return Json(new
            {
                success = true,
                facility = new
                {
                    facilityID = facility.FacilityID,
                    facilityName = facility.FacilityName ?? string.Empty,
                    description = facility.Description ?? string.Empty,
                    capacity = facility.Capacity,
                    availabilityStatus = facility.AvailabilityStatus ?? "Available",
                    imageUrl = facility.ImageUrl ?? string.Empty
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFacility(FacilityFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            try
            {
                var facilities = await _data.GetFacilitiesAsync();
                var facilityName = model.FacilityName.Trim();

                if (facilities.Any(f => string.Equals(f.FacilityName, facilityName, StringComparison.OrdinalIgnoreCase)))
                {
                    return Json(new { success = false, message = "Facility name already exists.", field = nameof(model.FacilityName) });
                }

                var facility = new Facility
                {
                    FacilityName = facilityName,
                    Description = model.Description.Trim(),
                    Capacity = model.Capacity,
                    AvailabilityStatus = string.IsNullOrWhiteSpace(model.AvailabilityStatus) ? "Available" : model.AvailabilityStatus.Trim(),
                    ImageUrl = await SaveFacilityImagesAsync(model.ImageFiles)
                };

                await _data.AddFacilityAsync(facility);

                return Json(new { success = true, message = "Facility added successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding facility.");
                return Json(new { success = false, message = $"Error adding facility: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFacility(FacilityFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            var facility = await _data.GetFacilityByIdAsync(model.FacilityID);
            if (facility == null)
            {
                return Json(new { success = false, message = "Facility not found." });
            }

            try
            {
                var facilities = await _data.GetFacilitiesAsync();
                var facilityName = model.FacilityName.Trim();
                if (facilities.Any(f =>
                        f.FacilityID != model.FacilityID &&
                        string.Equals(f.FacilityName, facilityName, StringComparison.OrdinalIgnoreCase)))
                {
                    return Json(new { success = false, message = "Facility name already exists.", field = nameof(model.FacilityName) });
                }

                facility.FacilityName = facilityName;
                facility.Description = model.Description.Trim();
                facility.Capacity = model.Capacity;
                facility.AvailabilityStatus = string.IsNullOrWhiteSpace(model.AvailabilityStatus) ? "Available" : model.AvailabilityStatus.Trim();
                facility.ImageUrl = model.ExistingImageUrl?.Trim() ?? string.Empty;

                var uploadedImages = await SaveFacilityImagesAsync(model.ImageFiles);
                if (!string.IsNullOrWhiteSpace(uploadedImages))
                {
                    facility.ImageUrl = uploadedImages;
                }

                await _data.UpdateFacilityAsync(facility);

                return Json(new { success = true, message = "Facility updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating facility {FacilityId}.", model.FacilityID);
                return Json(new { success = false, message = $"Error updating facility: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFacility(int facilityId)
        {
            var facility = await _data.GetFacilityByIdAsync(facilityId);
            if (facility == null)
            {
                return Json(new { success = false, message = "Facility not found." });
            }

            var reservations = await _data.GetReservationsAsync();
            if (reservations.Any(r => r.FacilityID == facilityId && !IsClosedReservationStatus(r.Status)))
            {
                return Json(new { success = false, message = "Facility has active reservations and cannot be deleted yet." });
            }

            await _data.DeleteFacilityAsync(facilityId);
            return Json(new { success = true, message = "Facility deleted successfully." });
        }

        [HttpGet]
        public async Task<IActionResult> GetReservationDetails(int reservationId)
        {
            var reservation = await _data.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                return Json(new { success = false, message = "Reservation not found." });
            }

            return Json(new
            {
                success = true,
                reservation = new
                {
                    reservationID = reservation.ReservationID,
                    facility = reservation.Facility?.FacilityName ?? "Unknown facility",
                    homeowner = reservation.Homeowner?.FullName ?? "Unknown homeowner",
                    reservationDate = reservation.ReservationDate.ToString("MMMM dd, yyyy"),
                    startTime = reservation.StartTime.ToString(@"hh\:mm"),
                    endTime = reservation.EndTime.ToString(@"hh\:mm"),
                    purpose = reservation.Purpose ?? "No purpose provided",
                    status = reservation.Status ?? "Unknown",
                    createdAt = reservation.CreatedAt.ToString("MMMM dd, yyyy HH:mm")
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GenerateReservationReport([FromQuery] ReservationReportRequestViewModel request)
        {
            var reservations = await _data.GetReservationsAsync();
            var facilities = await _data.GetFacilitiesAsync();

            var filteredReservations = reservations.AsEnumerable();
            if (request.StartDate.HasValue)
            {
                filteredReservations = filteredReservations.Where(r => r.ReservationDate.Date >= request.StartDate.Value.Date);
            }

            if (request.EndDate.HasValue)
            {
                filteredReservations = filteredReservations.Where(r => r.ReservationDate.Date <= request.EndDate.Value.Date);
            }

            if (request.FacilityId.HasValue)
            {
                filteredReservations = filteredReservations.Where(r => r.FacilityID == request.FacilityId.Value);
            }

            var reportType = string.IsNullOrWhiteSpace(request.ReportType) ? "summary" : request.ReportType.Trim().ToLowerInvariant();
            var csv = BuildReservationReportCsv(reportType, filteredReservations.ToList(), facilities);
            var fileName = $"reservation-report-{reportType}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveReservation(int reservationId)
        {
            var reservation = await _data.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                return Json(new { success = false, message = "Reservation not found." });
            }

            reservation.Status = "Approved";
            reservation.UpdatedAt = DateTime.UtcNow;
            await _data.UpdateReservationAsync(reservation);
            if (reservation.Homeowner != null)
            {
                await _notificationService.NotifyReservationStatusAsync(reservation, reservation.Homeowner, reservation.Facility, approved: true);
            }

            return Json(new { success = true, message = "Reservation approved successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> RejectReservation(int reservationId)
        {
            var reservation = await _data.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                return Json(new { success = false, message = "Reservation not found." });
            }

            reservation.Status = "Rejected";
            reservation.UpdatedAt = DateTime.UtcNow;
            await _data.UpdateReservationAsync(reservation);
            if (reservation.Homeowner != null)
            {
                await _notificationService.NotifyReservationStatusAsync(reservation, reservation.Homeowner, reservation.Facility, approved: false);
            }

            return Json(new { success = true, message = "Reservation rejected successfully." });
        }

        private async Task<ReservationManagementViewModel> BuildReservationManagementModuleAsync()
        {
            try
            {
                return await BuildReservationManagementViewModelAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building reservation management module.");
                return new ReservationManagementViewModel
                {
                    HasLoadError = true,
                    LoadErrorMessage = "Some reservation data could not be loaded. The page is showing a safe fallback state."
                };
            }
        }

        private ReservationManagementViewModel BuildReservationManagementShellViewModel()
        {
            return new ReservationManagementViewModel
            {
                Facilities = new List<Facility>(),
                ActiveReservations = new List<Reservation>(),
                TotalReservations = 0,
                ApprovedCount = 0,
                PendingCount = 0,
                RejectedCount = 0,
                CancelledCount = 0,
                ExpiredCount = 0,
                ApprovalRate = 0,
                PopularFacility = "Loading...",
                PopularFacilityCount = 0,
                PeakDay = "Loading...",
                PeakDayCount = 0
            };
        }

        private async Task<ReservationManagementViewModel> BuildReservationManagementViewModelAsync()
        {
            var facilitiesTask = _data.GetFacilitiesAsync();
            var reservationsTask = _data.GetReservationsAsync();
            await Task.WhenAll(facilitiesTask, reservationsTask);

            var facilities = facilitiesTask.Result.OrderBy(f => f.FacilityName).ToList();
            var reservations = reservationsTask.Result.OrderByDescending(r => r.ReservationDate).ToList();
            var activeReservations = reservations
                .Where(r => !IsEffectivelyExpired(r))
                .ToList();

            var approvedCount = reservations.Count(r => string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase));
            var pendingCount = reservations.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            var rejectedCount = reservations.Count(r => string.Equals(r.Status, "Rejected", StringComparison.OrdinalIgnoreCase));
            var cancelledCount = reservations.Count(r =>
                string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Status, "Canceled", StringComparison.OrdinalIgnoreCase));
            var expiredCount = reservations.Count(IsEffectivelyExpired);

            var facilityCounts = facilities
                .Select(f => new
                {
                    Name = f.FacilityName ?? "Unknown facility",
                    Count = reservations.Count(r => r.FacilityID == f.FacilityID)
                })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            var peakDay = reservations
                .GroupBy(r => r.ReservationDate.DayOfWeek)
                .Select(group => new { Day = group.Key.ToString(), Count = group.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            return new ReservationManagementViewModel
            {
                Facilities = facilities,
                ActiveReservations = activeReservations,
                TotalReservations = reservations.Count,
                ApprovedCount = approvedCount,
                PendingCount = pendingCount,
                RejectedCount = rejectedCount,
                CancelledCount = cancelledCount,
                ExpiredCount = expiredCount,
                ApprovalRate = reservations.Count == 0 ? 0 : (int)Math.Round((double)approvedCount / reservations.Count * 100),
                PopularFacility = facilityCounts?.Name ?? "No data",
                PopularFacilityCount = facilityCounts?.Count ?? 0,
                PeakDay = peakDay?.Day ?? "No data",
                PeakDayCount = peakDay?.Count ?? 0
            };
        }

        private async Task<List<Reservation>> GetActiveReservationsAsync()
        {
            return (await _data.GetReservationsAsync())
                .Where(r => !IsEffectivelyExpired(r))
                .OrderByDescending(r => r.ReservationDate)
                .ThenBy(r => r.StartTime)
                .ToList();
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

        private async Task<string> SaveFacilityImagesAsync(List<IFormFile>? imageFiles)
        {
            if (imageFiles == null || imageFiles.Count == 0)
            {
                return string.Empty;
            }

            var imagePaths = new List<string>();
            foreach (var imageFile in imageFiles.Where(file => file.Length > 0))
            {
                var publicUrl = await _fileStorageService.UploadFacilityImageAsync(imageFile, HttpContext.RequestAborted);

                imagePaths.Add(publicUrl);
            }

            return string.Join(",", imagePaths);
        }

        private bool IsClosedReservationStatus(string? status)
        {
            return string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEffectivelyExpired(Reservation reservation)
        {
            if (string.Equals(reservation.Status, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(reservation.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var endDateTime = reservation.ReservationDate.Date + reservation.EndTime;
            return endDateTime <= DateTime.UtcNow;
        }

        private string BuildReservationReportCsv(string reportType, List<Reservation> reservations, List<Facility> facilities)
        {
            var builder = new StringBuilder();

            if (reportType == "facility")
            {
                builder.AppendLine("Facility,Reservations,Approved,Pending,Rejected,Cancelled,Expired");
                foreach (var facility in facilities.OrderBy(f => f.FacilityName))
                {
                    var facilityReservations = reservations.Where(r => r.FacilityID == facility.FacilityID).ToList();
                    builder.AppendLine(string.Join(",",
                        EscapeCsv(facility.FacilityName),
                        facilityReservations.Count,
                        facilityReservations.Count(r => string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase)),
                        facilityReservations.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
                        facilityReservations.Count(r => string.Equals(r.Status, "Rejected", StringComparison.OrdinalIgnoreCase)),
                        facilityReservations.Count(r => string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "Canceled", StringComparison.OrdinalIgnoreCase)),
                        facilityReservations.Count(r => string.Equals(r.Status, "Expired", StringComparison.OrdinalIgnoreCase))));
                }

                return builder.ToString();
            }

            if (reportType == "detailed")
            {
                builder.AppendLine("Reservation ID,Facility,Homeowner,Date,Start Time,End Time,Purpose,Status,Created At");
                foreach (var reservation in reservations.OrderByDescending(r => r.ReservationDate))
                {
                    builder.AppendLine(string.Join(",",
                        reservation.ReservationID,
                        EscapeCsv(reservation.Facility?.FacilityName),
                        EscapeCsv(reservation.Homeowner?.FullName),
                        reservation.ReservationDate.ToString("yyyy-MM-dd"),
                        reservation.StartTime.ToString(@"hh\:mm"),
                        reservation.EndTime.ToString(@"hh\:mm"),
                        EscapeCsv(reservation.Purpose),
                        EscapeCsv(reservation.Status),
                        reservation.CreatedAt.ToString("yyyy-MM-dd HH:mm")));
                }

                return builder.ToString();
            }

            builder.AppendLine("Metric,Value");
            builder.AppendLine($"Total Reservations,{reservations.Count}");
            builder.AppendLine($"Approved,{reservations.Count(r => string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase))}");
            builder.AppendLine($"Pending,{reservations.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase))}");
            builder.AppendLine($"Rejected,{reservations.Count(r => string.Equals(r.Status, "Rejected", StringComparison.OrdinalIgnoreCase))}");
            builder.AppendLine($"Cancelled,{reservations.Count(r => string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "Canceled", StringComparison.OrdinalIgnoreCase))}");
            builder.AppendLine($"Expired,{reservations.Count(r => string.Equals(r.Status, "Expired", StringComparison.OrdinalIgnoreCase))}");

            return builder.ToString();
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }


        private async Task ExpireOldReservations()
        {
            var now = DateTime.Now;

            // Step 1: Pull approved reservations into memory
            var approvedReservations = await _data.GetReservationsByStatusAsync("Approved");

            // Step 2: Expire those that are already finished
            foreach (var reservation in approvedReservations)
            {
                var endDateTime = reservation.ReservationDate.Date + reservation.EndTime;
                if (endDateTime <= now)
                {
                    reservation.Status = "Expired";
                    reservation.UpdatedAt = now;
                    await _data.UpdateReservationAsync(reservation);
                }
            }
        }










        // Manage service requests (Admin View)
        public IActionResult ManageServiceRequests()
        {
            return PartialView("ManageServiceRequests", BuildServiceRequestsShellViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> ServiceRequestsSummary()
        {
            var model = await BuildServiceRequestsModuleAsync();
            return Json(new
            {
                totalRequests = model.TotalRequests,
                pendingCount = model.PendingCount,
                inProgressCount = model.InProgressCount,
                completedCount = model.CompletedCount,
                cancelledCount = model.CancelledCount,
                busiestCategory = model.BusiestCategory,
                busiestCategoryCount = model.BusiestCategoryCount,
                hasLoadError = model.HasLoadError,
                loadErrorMessage = model.LoadErrorMessage
            });
        }

        [HttpGet]
        public async Task<IActionResult> LoadServiceRequestsList(string statusFilter = "All")
        {
            try
            {
                var requests = await _data.GetServiceRequestsAsync();
                var filtered = FilterServiceRequests(requests, statusFilter);
                return PartialView("_ServiceRequestTableRows", filtered);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin service request list.");
                return PartialView("_ServiceRequestTableRows", new List<ServiceRequest>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceRequestDetails(int requestId)
        {
            var request = await _data.GetServiceRequestByIdAsync(requestId);
            if (request == null)
            {
                return Json(new { success = false, message = "Service request not found." });
            }

            return Json(new
            {
                success = true,
                request = new
                {
                    requestID = request.RequestID,
                    category = request.Category ?? "General",
                    priority = request.Priority ?? "Low",
                    description = request.Description ?? "No description provided",
                    status = request.Status ?? "Pending",
                    createdAt = request.CreatedAt.ToString("MMMM dd, yyyy HH:mm"),
                    homeowner = request.Homeowner?.FullName ?? "Unknown homeowner",
                    homeownerEmail = request.Homeowner?.Email ?? string.Empty,
                    assignedStaffID = request.AssignedStaffID,
                    assignedStaff = request.AssignedStaff?.FullName ?? "Unassigned",
                    completedAt = request.CompletedAt?.ToString("MMMM dd, yyyy HH:mm") ?? string.Empty
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateServiceRequestAdmin(int requestId, string status, int? assignedStaffId)
        {
            var request = await _data.GetServiceRequestByIdAsync(requestId);
            if (request == null)
            {
                return Json(new { success = false, message = "Service request not found." });
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "Pending" : status.Trim();
            var allowedStatuses = new[] { "Pending", "In Progress", "Completed", "Cancelled" };
            if (!allowedStatuses.Any(value => string.Equals(value, normalizedStatus, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, message = "Unsupported service request status." });
            }

            if (assignedStaffId.HasValue)
            {
                var staff = await _data.GetStaffByIdAsync(assignedStaffId.Value);
                if (staff == null)
                {
                    return Json(new { success = false, message = "Assigned staff member was not found." });
                }
            }

            request.Status = allowedStatuses.First(value => string.Equals(value, normalizedStatus, StringComparison.OrdinalIgnoreCase));
            request.AssignedStaffID = assignedStaffId;
            request.CompletedAt = string.Equals(request.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                ? DateTime.UtcNow
                : null;

            await _data.UpdateServiceRequestAsync(request);
            return Json(new { success = true, message = "Service request updated successfully." });
        }

        // Helper method to return a list of categories
        private List<string> GetEventCategories()
        {
            return new List<string> { "General", "Meeting", "Conference", "Workshop", "Webinar", "Party", "Training" };
        }

        // Event List View
        [HttpGet]
        public async Task<IActionResult> EventCalendar()
        {
            var viewModel = await BuildAdminEventsViewModelAsync();
            return PartialView("EventCalendar", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> LoadEventCards()
        {
            var events = (await _data.GetEventsAsync())
                .OrderBy(e => e.EventDate)
                .ToList();

            return PartialView("_AdminEventCards", events);
        }

        [HttpGet]
        public async Task<IActionResult> GetEventDetails(int id)
        {
            var eventModel = await _data.GetEventByIdAsync(id);
            if (eventModel == null)
            {
                return Json(new { success = false, message = "Event not found." });
            }

            return Json(new
            {
                success = true,
                eventItem = new
                {
                    eventModel.EventID,
                    title = eventModel.Title,
                    description = eventModel.Description,
                    eventDate = eventModel.EventDate.ToString("yyyy-MM-ddTHH:mm"),
                    category = eventModel.Category,
                    location = eventModel.Location
                }
            });
        }

        [HttpGet]
        public IActionResult AddEvent()
        {
            ViewBag.Categories = GetEventCategories();
            return PartialView("_AddEditEventPartial", new EventModel());
        }

        // Handle Add Event Submission (POST)
        [HttpPost]
        public async Task<IActionResult> AddEvent(EventFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            if (model.EventDate < new DateTime(1753, 1, 1))
            {
                ModelState.AddModelError(nameof(model.EventDate), "Please select a valid event date.");
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            try
            {
                var eventModel = MapEventModel(model);
                eventModel.CreatedBy = GetCurrentAdminID();

                await _data.AddEventAsync(eventModel);

                return Json(new { success = true, message = "Event added successfully!" });
            }
            catch (Exception)
            {
                _logger.LogError("Failed to save event for admin {AdminId}.", GetCurrentAdminID());
                return Json(new { success = false, message = "An error occurred while saving the event." });
            }
        }



        // GET: Edit Event
        [HttpGet]
        public async Task<IActionResult> EditEvent(int id)
        {
            var eventModel = await _data.GetEventByIdAsync(id);
            
            if (eventModel == null)
                return NotFound();

            ViewBag.Categories = GetEventCategories();
            return PartialView("_AddEditEventPartial", eventModel);
        }

        // POST: Edit Event
        [HttpPost]
        public async Task<IActionResult> EditEvent(EventFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            if (model.EventDate < new DateTime(1753, 1, 1))
            {
                ModelState.AddModelError(nameof(model.EventDate), "Please select a valid event date.");
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            try
            {
                var existingEvent = await _data.GetEventByIdAsync(model.EventID);
                if (existingEvent == null)
                {
                    return Json(new { success = false, message = "Event not found." });
                }

                var eventModel = MapEventModel(model);
                eventModel.CreatedBy = existingEvent.CreatedBy;

                await _data.UpdateEventAsync(eventModel);
                return Json(new { success = true, message = "Event updated successfully!" });
            }
            catch (Exception)
            {
                _logger.LogError("Failed to update event {EventId}.", model.EventID);
                return Json(new { success = false, message = "Error updating the event. Please try again." });
            }
        }


        // POST: Delete Event
        [HttpPost]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            try
            {
                await _data.DeleteEventAsync(id);
                return Json(new { success = true, message = "Event deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error deleting event: " + ex.Message);
                return Json(new { success = false, message = "Error deleting event. Please try again." });
            }
        }


        // Helper: Get Logged-in Admin ID
        private int GetCurrentAdminID()
        {
            return GetCurrentAdminId();
        }

        private async Task<AdminEventsViewModel> BuildAdminEventsViewModelAsync()
        {
            var events = (await _data.GetEventsAsync())
                .OrderBy(e => e.EventDate)
                .ToList();

            var categories = GetEventCategories();
            categories.Insert(0, "All");

            return new AdminEventsViewModel
            {
                Events = events,
                Categories = categories
            };
        }

        private static EventModel MapEventModel(EventFormViewModel model)
        {
            return new EventModel
            {
                EventID = model.EventID,
                Title = model.Title.Trim(),
                Description = model.Description.Trim(),
                EventDate = model.EventDate,
                Category = model.Category.Trim(),
                Location = model.Location.Trim()
            };
        }

        // GET: Show the form for creating an announcement and the list
        public async Task<IActionResult> AnnouncementList()
        {
            var model = await BuildAdminAnnouncementsViewModelAsync();
            return PartialView("AnnouncementList", model);
        }

        [HttpGet]
        public async Task<IActionResult> LoadAnnouncementCards()
        {
            var announcements = await GetOrderedAnnouncementsAsync();
            return PartialView("_AnnouncementListPartial", announcements);
        }

        [HttpGet]
        public async Task<IActionResult> GetAnnouncementDetails(int id)
        {
            var announcement = await _data.GetAnnouncementByIdAsync(id);
            if (announcement == null)
            {
                return Json(new { success = false, message = "Announcement not found." });
            }

            return Json(new
            {
                success = true,
                announcement = new
                {
                    announcement.AnnouncementID,
                    title = announcement.Title,
                    content = announcement.Content,
                    isUrgent = announcement.IsUrgent,
                    postedAt = announcement.PostedAt.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss")
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> AnnouncementList(AnnouncementFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            try
            {
                var announcement = new Announcement
                {
                    Title = model.Title.Trim(),
                    Content = model.Content.Trim(),
                    PostedAt = DateTime.UtcNow,
                    IsUrgent = model.IsUrgent
                };

                await _data.AddAnnouncementAsync(announcement);
                await _notificationService.NotifyAllHomeownersAnnouncementAsync(announcement);

                return Json(new { success = true, message = "Announcement posted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create announcement.");
                return Json(new { success = false, message = "An error occurred while saving the announcement." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditAnnouncement(AnnouncementFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            try
            {
                var announcement = await _data.GetAnnouncementByIdAsync(model.AnnouncementID);
                if (announcement == null)
                {
                    return Json(new { success = false, message = "Announcement not found." });
                }

                announcement.Title = model.Title.Trim();
                announcement.Content = model.Content.Trim();
                announcement.IsUrgent = model.IsUrgent;

                await _data.UpdateAnnouncementAsync(announcement);
                return Json(new { success = true, message = "Announcement updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update announcement {AnnouncementId}.", model.AnnouncementID);
                return Json(new { success = false, message = "An error occurred while updating the announcement." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            try
            {
                await _data.DeleteAnnouncementAsync(id);
                return Json(new { success = true, message = "Announcement deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete announcement {AnnouncementId}.", id);
                return Json(new { success = false, message = "An error occurred while deleting the announcement." });
            }
        }

        private async Task<AdminAnnouncementsViewModel> BuildAdminAnnouncementsViewModelAsync()
        {
            return new AdminAnnouncementsViewModel
            {
                Announcements = await GetOrderedAnnouncementsAsync()
            };
        }

        private async Task<List<Announcement>> GetOrderedAnnouncementsAsync()
        {
            return (await _data.GetAnnouncementsAsync())
                .OrderByDescending(a => a.PostedAt)
                .ToList();
        }


        public async Task<IActionResult> Analytics()
        {
            var model = await BuildAnalyticsViewModelAsync();
            return PartialView("Analytics", model);
        }

        public async Task<IActionResult> CreateBilling()
        {
            var model = await BuildBillingManagementViewModelAsync();
            return PartialView("CreateBilling", model);
        }

        [HttpGet]
        public async Task<IActionResult> LoadBillingList()
        {
            var model = await BuildBillingManagementViewModelAsync();
            return PartialView("_BillingTableRows", model.Billings);
        }

        [HttpGet]
        public async Task<IActionResult> BillingSummary()
        {
            var model = await BuildBillingManagementViewModelAsync();
            return Json(new
            {
                totalRevenue = model.TotalRevenue,
                totalBills = model.TotalBills,
                paidBills = model.PaidBills,
                pendingBills = model.PendingBills,
                overdueBills = model.OverdueBills,
                submittedPayments = model.SubmittedPayments
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBilling(CreateBillingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please correct the highlighted fields.",
                    validationErrors = BuildValidationErrors()
                });
            }

            var homeowner = await _data.GetHomeownerByIdAsync(model.HomeownerID);
            if (homeowner == null)
            {
                return Json(new { success = false, message = "Selected homeowner was not found.", field = nameof(model.HomeownerID) });
            }

            if (model.DueDate.Date < DateTime.UtcNow.Date)
            {
                return Json(new { success = false, message = "Due date cannot be in the past.", field = nameof(model.DueDate) });
            }

            var billing = new Billing
            {
                HomeownerID = model.HomeownerID,
                Description = model.Description.Trim(),
                Amount = model.Amount,
                DueDate = model.DueDate.Date,
                BillType = model.BillType.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _data.AddBillingAsync(billing);
            await _notificationService.NotifyBillingCreatedAsync(billing, homeowner);

            return Json(new { success = true, message = "Bill created successfully!", billingId = billing.BillingID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBillingStatus(UpdateBillingStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid billing update request." });
            }

            var normalizedStatus = model.Status.Trim();
            var allowedStatuses = new[] { "Pending", "Paid", "Overdue" };
            if (!allowedStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Unsupported billing status." });
            }

            var billing = await _data.GetBillingByIdAsync(model.Id);
            if (billing == null)
            {
                return Json(new { success = false, message = "Billing record not found." });
            }

            billing.Status = normalizedStatus;
            if (string.Equals(normalizedStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                billing.PaidAt = DateTime.UtcNow;
                billing.PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Admin Manual" : model.PaymentMethod.Trim();
                billing.TransactionID = string.IsNullOrWhiteSpace(model.TransactionID) ? null : model.TransactionID.Trim();
            }
            else
            {
                billing.PaidAt = null;
                billing.PaymentMethod = null;
                billing.TransactionID = null;
            }

            await _data.UpdateBillingAsync(billing);

            return Json(new { success = true, message = "Billing status updated successfully!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetBillingDetails(int id)
        {
            var billingTask = _data.GetBillingByIdAsync(id);
            var homeownersTask = _data.GetHomeownersAsync();

            await Task.WhenAll(billingTask, homeownersTask);

            var billing = billingTask.Result;
            if (billing == null)
            {
                return Json(new { success = false, message = "Billing record not found." });
            }

            var homeowner = homeownersTask.Result.FirstOrDefault(h => h.HomeownerID == billing.HomeownerID);
            var item = MapBillingListItem(billing, homeowner, DateTime.UtcNow);

            return Json(new { success = true, billing = item });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewBillingSubmission(ReviewPaymentSubmissionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid review request.",
                    validationErrors = BuildValidationErrors()
                });
            }

            var normalizedAction = model.Action.Trim();
            if (!string.Equals(normalizedAction, "Approve", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedAction, "Reject", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Unsupported review action." });
            }

            var billing = await _data.GetBillingByIdAsync(model.BillingID);
            if (billing == null)
            {
                return Json(new { success = false, message = "Billing record not found." });
            }

            if (!string.Equals(billing.PaymentSubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "This billing record does not have a pending payment submission." });
            }

            billing.PaymentReviewedAt = DateTime.UtcNow;
            billing.PaymentReviewedBy = User.Identity?.Name ?? "Administrator";
            billing.PaymentReviewNotes = string.IsNullOrWhiteSpace(model.ReviewNotes) ? null : model.ReviewNotes.Trim();

            if (string.Equals(normalizedAction, "Approve", StringComparison.OrdinalIgnoreCase))
            {
                billing.PaymentSubmissionStatus = "Approved";
                billing.Status = "Paid";
                billing.PaidAt = DateTime.UtcNow;
                billing.PaymentMethod = string.IsNullOrWhiteSpace(billing.SubmittedPaymentMethod) ? "Homeowner Submission" : billing.SubmittedPaymentMethod;
                billing.TransactionID = string.IsNullOrWhiteSpace(billing.SubmittedReferenceNumber) ? billing.TransactionID : billing.SubmittedReferenceNumber;
            }
            else
            {
                billing.PaymentSubmissionStatus = "Rejected";
                billing.Status = billing.DueDate.Date < DateTime.UtcNow.Date ? "Overdue" : "Pending";
                billing.PaidAt = null;
                billing.PaymentMethod = null;
                billing.TransactionID = null;
            }

            await _data.UpdateBillingAsync(billing);
            var homeowner = await _data.GetHomeownerByIdAsync(billing.HomeownerID);
            if (homeowner != null)
            {
                await _notificationService.NotifyBillingSubmissionReviewedAsync(
                    billing,
                    homeowner,
                    approved: string.Equals(normalizedAction, "Approve", StringComparison.OrdinalIgnoreCase));
            }

            return Json(new
            {
                success = true,
                message = string.Equals(normalizedAction, "Approve", StringComparison.OrdinalIgnoreCase)
                    ? "Payment submission approved successfully."
                    : "Payment submission rejected successfully."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBilling(int id)
        {
            var billing = await _data.GetBillingByIdAsync(id);
            if (billing == null)
            {
                return Json(new { success = false, message = "Billing record not found." });
            }

            await _data.DeleteBillingAsync(id);

            return Json(new { success = true, message = "Billing record deleted successfully!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetHomeowners()
        {
            var homeowners = (await _data.GetHomeownersAsync()).Select(h => new {
                h.HomeownerID, 
                h.FullName, 
                h.Email 
            }).ToList();
            return Json(homeowners);
        }

        private async Task<BillingManagementViewModel> BuildBillingManagementViewModelAsync()
        {
            var billingsTask = _data.GetBillingsAsync();
            var homeownersTask = _data.GetHomeownersAsync();

            await Task.WhenAll(billingsTask, homeownersTask);

            var homeowners = homeownersTask.Result;
            var homeownerLookup = homeowners.ToDictionary(h => h.HomeownerID);
            var nowUtc = DateTime.UtcNow;

            var billingItems = billingsTask.Result
                .Select(billing =>
                {
                    homeownerLookup.TryGetValue(billing.HomeownerID, out var homeowner);
                    return MapBillingListItem(billing, homeowner, nowUtc);
                })
                .OrderByDescending(item => item.CreatedAt)
                .ToList();

            return new BillingManagementViewModel
            {
                Billings = billingItems,
                Homeowners = homeowners
                    .OrderBy(h => h.FullName)
                    .Select(h => new BillingHomeownerOptionViewModel
                    {
                        HomeownerID = h.HomeownerID,
                        FullName = h.FullName ?? "Homeowner",
                        Email = h.Email ?? "-"
                    })
                    .ToList(),
                TotalRevenue = billingItems
                    .Where(item => string.Equals(item.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.Amount),
                TotalBills = billingItems.Count,
                PaidBills = billingItems.Count(item => string.Equals(item.Status, "Paid", StringComparison.OrdinalIgnoreCase)),
                PendingBills = billingItems.Count(item => string.Equals(item.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
                OverdueBills = billingItems.Count(item => string.Equals(item.Status, "Overdue", StringComparison.OrdinalIgnoreCase)),
                SubmittedPayments = billingItems.Count(item => string.Equals(item.PaymentSubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
            };
        }

        private static BillingListItemViewModel MapBillingListItem(Billing billing, Homeowner? homeowner, DateTime nowUtc)
        {
            var isOverdue = !string.Equals(billing.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                && billing.DueDate.Date < nowUtc.Date;

            return new BillingListItemViewModel
            {
                BillingID = billing.BillingID,
                HomeownerID = billing.HomeownerID,
                HomeownerName = homeowner?.FullName ?? "Unknown homeowner",
                HomeownerEmail = homeowner?.Email ?? "-",
                Description = billing.Description,
                Amount = billing.Amount,
                DueDate = billing.DueDate,
                BillType = billing.BillType,
                Status = isOverdue ? "Overdue" : (billing.Status ?? "Pending"),
                IsOverdue = isOverdue,
                CreatedAt = billing.CreatedAt,
                PaidAt = billing.PaidAt,
                PaymentMethod = billing.PaymentMethod,
                TransactionID = billing.TransactionID,
                PaymentSubmissionStatus = string.IsNullOrWhiteSpace(billing.PaymentSubmissionStatus) ? "None" : billing.PaymentSubmissionStatus,
                PaymentSubmittedAt = billing.PaymentSubmittedAt,
                SubmittedAmount = billing.SubmittedAmount,
                SubmittedPaymentMethod = billing.SubmittedPaymentMethod,
                SubmittedReferenceNumber = billing.SubmittedReferenceNumber,
                PaymentSubmissionNotes = billing.PaymentSubmissionNotes,
                PaymentProofUrl = billing.PaymentProofUrl,
                PaymentReviewedAt = billing.PaymentReviewedAt,
                PaymentReviewedBy = billing.PaymentReviewedBy,
                PaymentReviewNotes = billing.PaymentReviewNotes
            };
        }

        private async Task<AdminDashboardOverviewViewModel> BuildDashboardOverviewAsync()
        {
            var homeownerCountTask = _data.GetHomeownerCountAsync("Homeowner");
            var staffCountTask = _data.GetStaffCountAsync();
            var reservationCountTask = _data.GetReservationCountByStatusAsync("Approved");
            var notificationCountTask = _data.GetNotificationCountAsync();

            var homeownersTask = _data.GetHomeownersAsync();
            var staffTask = _data.GetStaffAsync();
            var reservationsTask = _data.GetReservationsAsync();
            var facilitiesTask = _data.GetFacilitiesAsync();
            var announcementsTask = _data.GetAnnouncementsAsync();
            var serviceRequestsTask = _data.GetServiceRequestsAsync();
            var complaintsTask = _data.GetComplaintsAsync();

            await Task.WhenAll(
                homeownerCountTask,
                staffCountTask,
                reservationCountTask,
                notificationCountTask,
                homeownersTask,
                staffTask,
                reservationsTask,
                facilitiesTask,
                announcementsTask,
                serviceRequestsTask,
                complaintsTask);

            var homeowners = homeownersTask.Result;
            var staff = staffTask.Result;
            var reservations = reservationsTask.Result;
            var facilities = facilitiesTask.Result;
            var announcements = announcementsTask.Result;
            var serviceRequests = serviceRequestsTask.Result;
            var complaints = complaintsTask.Result;

            return new AdminDashboardOverviewViewModel
            {
                HomeownerCount = homeownerCountTask.Result,
                StaffCount = staffCountTask.Result,
                ReservationCount = reservationCountTask.Result,
                NotificationCount = notificationCountTask.Result,
                FacilityUsage = BuildFacilityUsage(reservations, facilities),
                RecentActivity = BuildRecentActivity(homeowners, staff, reservations, announcements, serviceRequests)
            };
        }

        private static List<DashboardChartPointViewModel> BuildFacilityUsage(
            IEnumerable<Reservation> reservations,
            IEnumerable<Facility> facilities)
        {
            var facilitiesById = facilities.ToDictionary(f => f.FacilityID);

            var usage = reservations
                .Where(r => HasStatus(r.Status, "Approved", "Completed"))
                .Select(r =>
                {
                    if (!string.IsNullOrWhiteSpace(r.Facility?.FacilityName))
                    {
                        return r.Facility.FacilityName!;
                    }

                    if (facilitiesById.TryGetValue(r.FacilityID, out var facility) &&
                        !string.IsNullOrWhiteSpace(facility.FacilityName))
                    {
                        return facility.FacilityName!;
                    }

                    return "Unknown facility";
                })
                .GroupBy(name => name)
                .Select(group => new DashboardChartPointViewModel
                {
                    Label = group.Key,
                    Value = group.Count()
                })
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Label)
                .Take(5)
                .ToList();

            if (usage.Count > 0)
            {
                return usage;
            }

            return facilities
                .Where(f => !string.IsNullOrWhiteSpace(f.FacilityName))
                .Take(3)
                .Select(f => new DashboardChartPointViewModel
                {
                    Label = f.FacilityName!,
                    Value = 0
                })
                .ToList();
        }

        private static List<DashboardActivityItemViewModel> BuildRecentActivity(
            IEnumerable<Homeowner> homeowners,
            IEnumerable<Staff> staff,
            IEnumerable<Reservation> reservations,
            IEnumerable<Announcement> announcements,
            IEnumerable<ServiceRequest> serviceRequests)
        {
            var items = new List<DashboardActivityItemViewModel>();

            items.AddRange(homeowners
                .Where(h => IsValidDashboardDate(h.CreatedAt))
                .Select(h =>
                {
                    var occurredAt = NormalizeDashboardDate(h.CreatedAt);
                    return new DashboardActivityItemViewModel
                    {
                        Type = "homeowner",
                        Title = "New homeowner onboarded",
                        Description = $"{(string.IsNullOrWhiteSpace(h.FullName) ? "A resident" : h.FullName)} was added to the community directory.",
                        TimeAgo = FormatRelativeTime(occurredAt),
                        OccurredAt = occurredAt
                    };
                })
                .OrderByDescending(item => item.OccurredAt)
                .Take(2));

            items.AddRange(staff
                .Where(s => IsValidDashboardDate(s.CreatedAt))
                .Select(s =>
                {
                    var occurredAt = NormalizeDashboardDate(s.CreatedAt);
                    var roleName = string.IsNullOrWhiteSpace(s.Position) ? "team member" : s.Position!.ToLowerInvariant();
                    return new DashboardActivityItemViewModel
                    {
                        Type = "staff",
                        Title = "Staff account activated",
                        Description = $"{(string.IsNullOrWhiteSpace(s.FullName) ? "A staff member" : s.FullName)} joined as {roleName}.",
                        TimeAgo = FormatRelativeTime(occurredAt),
                        OccurredAt = occurredAt
                    };
                })
                .OrderByDescending(item => item.OccurredAt)
                .Take(2));

            items.AddRange(reservations
                .Where(r => IsValidDashboardDate(r.CreatedAt))
                .Select(r =>
                {
                    var occurredAt = NormalizeDashboardDate(r.CreatedAt);
                    var facilityName = string.IsNullOrWhiteSpace(r.Facility?.FacilityName) ? "facility booking" : r.Facility!.FacilityName!;
                    var homeownerName = string.IsNullOrWhiteSpace(r.Homeowner?.FullName) ? "A homeowner" : r.Homeowner!.FullName!;
                    return new DashboardActivityItemViewModel
                    {
                        Type = "reservation",
                        Title = HasStatus(r.Status, "Approved") ? "Facility reservation approved" : "Facility reservation created",
                        Description = $"{homeownerName} submitted {facilityName.ToLowerInvariant()} for {r.ReservationDate:MMM d}.",
                        TimeAgo = FormatRelativeTime(occurredAt),
                        OccurredAt = occurredAt
                    };
                })
                .OrderByDescending(item => item.OccurredAt)
                .Take(3));

            items.AddRange(serviceRequests
                .Where(r => IsValidDashboardDate(r.CreatedAt))
                .Select(r =>
                {
                    var occurredAt = NormalizeDashboardDate(r.CreatedAt);
                    var homeownerName = string.IsNullOrWhiteSpace(r.Homeowner?.FullName) ? "A homeowner" : r.Homeowner!.FullName!;
                    return new DashboardActivityItemViewModel
                    {
                        Type = "serviceRequest",
                        Title = "Service request logged",
                        Description = $"{homeownerName} opened a {((r.Category ?? "service").ToLowerInvariant())} request.",
                        TimeAgo = FormatRelativeTime(occurredAt),
                        OccurredAt = occurredAt
                    };
                })
                .OrderByDescending(item => item.OccurredAt)
                .Take(2));

            items.AddRange(announcements
                .Where(a => IsValidDashboardDate(a.PostedAt))
                .Select(a =>
                {
                    var occurredAt = NormalizeDashboardDate(a.PostedAt);
                    return new DashboardActivityItemViewModel
                    {
                        Type = a.IsUrgent ? "urgentAnnouncement" : "announcement",
                        Title = a.IsUrgent ? "Urgent announcement published" : "Announcement published",
                        Description = string.IsNullOrWhiteSpace(a.Title) ? "A community update was published." : a.Title!,
                        TimeAgo = FormatRelativeTime(occurredAt),
                        OccurredAt = occurredAt
                    };
                })
                .OrderByDescending(item => item.OccurredAt)
                .Take(2));

            return items
                .OrderByDescending(item => item.OccurredAt)
                .Take(6)
                .ToList();
        }

        private static bool HasStatus(string? status, params string[] expectedValues)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return expectedValues.Any(expected =>
                string.Equals(status, expected, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<AdminAnalyticsViewModel> BuildAnalyticsViewModelAsync()
        {
            var facilitiesTask = _data.GetFacilitiesAsync();
            var reservationsTask = _data.GetReservationsAsync();
            var billingsTask = _data.GetBillingsAsync();
            var serviceRequestsTask = _data.GetServiceRequestsAsync();
            var complaintsTask = _data.GetComplaintsAsync();
            var announcementsTask = _data.GetAnnouncementsAsync();
            var pollsTask = _data.GetPollsAsync();

            await Task.WhenAll(
                facilitiesTask,
                reservationsTask,
                billingsTask,
                serviceRequestsTask,
                complaintsTask,
                announcementsTask,
                pollsTask);

            var nowUtc = DateTime.UtcNow;
            var facilities = facilitiesTask.Result;
            var reservations = reservationsTask.Result;
            var billings = billingsTask.Result;
            var serviceRequests = serviceRequestsTask.Result;
            var complaints = complaintsTask.Result;
            var announcements = announcementsTask.Result;
            var polls = pollsTask.Result;

            var totalRevenue = billings
                .Where(b => HasStatus(b.Status, "Paid"))
                .Sum(b => b.Amount);
            var outstandingBalance = billings
                .Where(b => !HasStatus(b.Status, "Paid"))
                .Sum(b => b.Amount);

            var paidBills = billings.Count(b => HasStatus(b.Status, "Paid"));
            var collectionRate = billings.Count == 0
                ? 0
                : (int)Math.Round((double)paidBills / billings.Count * 100, MidpointRounding.AwayFromZero);

            var openServiceRequests = serviceRequests.Count(r => HasStatus(r.Status, "Pending", "In Progress"));
            var resolvedComplaints = complaints.Count(c => HasStatus(c.Status, "Resolved", "Closed"));
            var communityVotes = polls.Sum(p => p.TotalVotes);
            var activePolls = polls.Count(p => HasStatus(p.Status, "Active"));
            var announcementsThisMonth = announcements.Count(a => a.PostedAt.Year == nowUtc.Year && a.PostedAt.Month == nowUtc.Month);

            return new AdminAnalyticsViewModel
            {
                Metrics = new List<AdminAnalyticsMetricViewModel>
                {
                    new()
                    {
                        Key = "revenue",
                        Label = "Collected revenue",
                        Value = $"Php {totalRevenue:N2}",
                        Caption = $"{collectionRate}% of bills settled",
                        Icon = "fa-money-bill-wave",
                        Tone = "blue"
                    },
                    new()
                    {
                        Key = "outstanding",
                        Label = "Outstanding balance",
                        Value = $"Php {outstandingBalance:N2}",
                        Caption = $"{billings.Count(b => HasStatus(b.Status, "Pending", "Overdue"))} bills still open",
                        Icon = "fa-wallet",
                        Tone = "amber"
                    },
                    new()
                    {
                        Key = "requests",
                        Label = "Open service load",
                        Value = openServiceRequests.ToString(),
                        Caption = $"{serviceRequests.Count(r => HasStatus(r.Status, "Completed"))} requests completed",
                        Icon = "fa-screwdriver-wrench",
                        Tone = "teal"
                    },
                    new()
                    {
                        Key = "community",
                        Label = "Community engagement",
                        Value = communityVotes.ToString(),
                        Caption = $"{activePolls} active polls · {announcementsThisMonth} announcements this month",
                        Icon = "fa-chart-line",
                        Tone = "violet"
                    }
                },
                MonthlyTrend = BuildMonthlyTrend(nowUtc, reservations, serviceRequests, billings),
                FinancialTrend = BuildFinancialTrend(nowUtc, billings),
                ReservationBreakdown = BuildReservationBreakdown(reservations),
                BillingBreakdown = BuildBillingBreakdown(billings),
                ServiceRequestBreakdown = BuildServiceRequestBreakdown(serviceRequests),
                FacilityPerformance = BuildFacilityPerformance(facilities, reservations),
                ServiceCategoryPerformance = BuildServiceCategoryPerformance(serviceRequests),
                CommunitySignals = new List<AdminAnalyticsCategoryPointViewModel>
                {
                    new()
                    {
                        Label = "Resolved complaints",
                        Value = resolvedComplaints,
                        Subtitle = $"{complaints.Count} total complaint records"
                    },
                    new()
                    {
                        Label = "Announcements this month",
                        Value = announcementsThisMonth,
                        Subtitle = $"{announcements.Count(a => a.IsUrgent)} urgent posts overall"
                    },
                    new()
                    {
                        Label = "Active polls",
                        Value = activePolls,
                        Subtitle = $"{communityVotes} homeowner votes recorded"
                    }
                },
                GeneratedAt = nowUtc
            };
        }

        private static List<AdminAnalyticsTrendPointViewModel> BuildMonthlyTrend(
            DateTime nowUtc,
            IEnumerable<Reservation> reservations,
            IEnumerable<ServiceRequest> serviceRequests,
            IEnumerable<Billing> billings)
        {
            var points = new List<AdminAnalyticsTrendPointViewModel>();

            for (var i = 5; i >= 0; i--)
            {
                var month = nowUtc.AddMonths(-i);
                points.Add(new AdminAnalyticsTrendPointViewModel
                {
                    Label = month.ToString("MMM yyyy"),
                    Reservations = reservations.Count(r => r.ReservationDate.Year == month.Year && r.ReservationDate.Month == month.Month),
                    ServiceRequests = serviceRequests.Count(r => r.CreatedAt.Year == month.Year && r.CreatedAt.Month == month.Month),
                    Billings = billings.Count(b => b.CreatedAt.Year == month.Year && b.CreatedAt.Month == month.Month)
                });
            }

            return points;
        }

        private static List<AdminAnalyticsFinancialTrendPointViewModel> BuildFinancialTrend(
            DateTime nowUtc,
            IEnumerable<Billing> billings)
        {
            var points = new List<AdminAnalyticsFinancialTrendPointViewModel>();

            for (var i = 5; i >= 0; i--)
            {
                var month = nowUtc.AddMonths(-i);
                var monthBillings = billings
                    .Where(b => b.CreatedAt.Year == month.Year && b.CreatedAt.Month == month.Month)
                    .ToList();

                points.Add(new AdminAnalyticsFinancialTrendPointViewModel
                {
                    Label = month.ToString("MMM yyyy"),
                    CollectedRevenue = monthBillings
                        .Where(b => HasStatus(b.Status, "Paid"))
                        .Sum(b => b.Amount),
                    OutstandingRevenue = monthBillings
                        .Where(b => !HasStatus(b.Status, "Paid"))
                        .Sum(b => b.Amount)
                });
            }

            return points;
        }

        private static List<AdminAnalyticsBreakdownItemViewModel> BuildReservationBreakdown(IEnumerable<Reservation> reservations)
        {
            return new List<AdminAnalyticsBreakdownItemViewModel>
            {
                new() { Label = "Approved", Value = reservations.Count(r => HasStatus(r.Status, "Approved")), Tone = "blue" },
                new() { Label = "Pending", Value = reservations.Count(r => HasStatus(r.Status, "Pending")), Tone = "amber" },
                new() { Label = "Completed", Value = reservations.Count(r => HasStatus(r.Status, "Completed")), Tone = "teal" },
                new() { Label = "Cancelled", Value = reservations.Count(r => HasStatus(r.Status, "Cancelled", "Canceled")), Tone = "red" }
            };
        }

        private static List<AdminAnalyticsBreakdownItemViewModel> BuildBillingBreakdown(IEnumerable<Billing> billings)
        {
            return new List<AdminAnalyticsBreakdownItemViewModel>
            {
                new() { Label = "Paid", Value = billings.Count(b => HasStatus(b.Status, "Paid")), Tone = "teal" },
                new() { Label = "Pending", Value = billings.Count(b => HasStatus(b.Status, "Pending")), Tone = "amber" },
                new() { Label = "Overdue", Value = billings.Count(b => HasStatus(b.Status, "Overdue")), Tone = "red" },
                new() { Label = "Submitted", Value = billings.Count(b => HasStatus(b.PaymentSubmissionStatus, "Submitted")), Tone = "blue" }
            };
        }

        private static List<AdminAnalyticsBreakdownItemViewModel> BuildServiceRequestBreakdown(IEnumerable<ServiceRequest> serviceRequests)
        {
            return new List<AdminAnalyticsBreakdownItemViewModel>
            {
                new() { Label = "Pending", Value = serviceRequests.Count(r => HasStatus(r.Status, "Pending")), Tone = "amber" },
                new() { Label = "In progress", Value = serviceRequests.Count(r => HasStatus(r.Status, "In Progress")), Tone = "blue" },
                new() { Label = "Completed", Value = serviceRequests.Count(r => HasStatus(r.Status, "Completed")), Tone = "teal" },
                new() { Label = "Cancelled", Value = serviceRequests.Count(r => HasStatus(r.Status, "Cancelled", "Canceled")), Tone = "red" }
            };
        }

        private static List<AdminAnalyticsCategoryPointViewModel> BuildFacilityPerformance(
            IEnumerable<Facility> facilities,
            IEnumerable<Reservation> reservations)
        {
            var facilityLookup = facilities.ToDictionary(f => f.FacilityID);

            var points = reservations
                .Where(r => HasStatus(r.Status, "Approved", "Completed"))
                .GroupBy(r => r.FacilityID)
                .Select(group =>
                {
                    facilityLookup.TryGetValue(group.Key, out var facility);
                    return new AdminAnalyticsCategoryPointViewModel
                    {
                        Label = facility?.FacilityName ?? $"Facility #{group.Key}",
                        Value = group.Count(),
                        Subtitle = $"{group.Count(r => r.ReservationDate >= DateTime.UtcNow.AddDays(-30))} in the last 30 days"
                    };
                })
                .OrderByDescending(point => point.Value)
                .ThenBy(point => point.Label)
                .Take(5)
                .ToList();

            if (points.Count > 0)
            {
                return points;
            }

            return facilities
                .Take(4)
                .Select(f => new AdminAnalyticsCategoryPointViewModel
                {
                    Label = f.FacilityName ?? $"Facility #{f.FacilityID}",
                    Value = 0,
                    Subtitle = "No bookings yet"
                })
                .ToList();
        }

        private static List<AdminAnalyticsCategoryPointViewModel> BuildServiceCategoryPerformance(IEnumerable<ServiceRequest> serviceRequests)
        {
            var points = serviceRequests
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Uncategorized" : r.Category!)
                .Select(group => new AdminAnalyticsCategoryPointViewModel
                {
                    Label = group.Key,
                    Value = group.Count(),
                    Subtitle = $"{group.Count(r => HasStatus(r.Status, "Completed"))} completed"
                })
                .OrderByDescending(point => point.Value)
                .ThenBy(point => point.Label)
                .Take(5)
                .ToList();

            return points.Count > 0
                ? points
                : new List<AdminAnalyticsCategoryPointViewModel>
                {
                    new()
                    {
                        Label = "No categories yet",
                        Value = 0,
                        Subtitle = "Service requests will appear here."
                    }
                };
        }

        private AdminServiceRequestsViewModel BuildServiceRequestsShellViewModel()
        {
            return new AdminServiceRequestsViewModel
            {
                Requests = new List<ServiceRequest>(),
                StaffList = new List<Staff>(),
                BusiestCategory = "Loading..."
            };
        }

        private async Task<AdminServiceRequestsViewModel> BuildServiceRequestsModuleAsync()
        {
            try
            {
                var requestsTask = _data.GetServiceRequestsAsync();
                var staffTask = _data.GetStaffAsync();
                await Task.WhenAll(requestsTask, staffTask);

                var requests = requestsTask.Result
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                var busiestCategory = requests
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Uncategorized" : r.Category!)
                    .Select(group => new { Category = group.Key, Count = group.Count() })
                    .OrderByDescending(group => group.Count)
                    .FirstOrDefault();

                return new AdminServiceRequestsViewModel
                {
                    Requests = requests,
                    StaffList = staffTask.Result.OrderBy(s => s.FullName).ToList(),
                    TotalRequests = requests.Count,
                    PendingCount = requests.Count(r => HasStatus(r.Status, "Pending")),
                    InProgressCount = requests.Count(r => HasStatus(r.Status, "In Progress")),
                    CompletedCount = requests.Count(r => HasStatus(r.Status, "Completed")),
                    CancelledCount = requests.Count(r => HasStatus(r.Status, "Cancelled", "Canceled")),
                    BusiestCategory = busiestCategory?.Category ?? "No data",
                    BusiestCategoryCount = busiestCategory?.Count ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building admin service request module.");
                return new AdminServiceRequestsViewModel
                {
                    HasLoadError = true,
                    LoadErrorMessage = "Some service request data could not be loaded. The module is showing a safe fallback state."
                };
            }
        }

        private static List<ServiceRequest> FilterServiceRequests(IEnumerable<ServiceRequest> requests, string? statusFilter)
        {
            if (string.IsNullOrWhiteSpace(statusFilter) || string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                return requests.OrderByDescending(r => r.CreatedAt).ToList();
            }

            return requests
                .Where(r => string.Equals(r.Status, statusFilter, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        private static bool IsValidDashboardDate(DateTime value)
        {
            return value != default && value.Year > 2000;
        }

        private static DateTime NormalizeDashboardDate(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
        }

        private static string FormatRelativeTime(DateTime occurredAtUtc)
        {
            var elapsed = DateTime.UtcNow - occurredAtUtc;

            if (elapsed.TotalMinutes < 1)
            {
                return "Just now";
            }

            if (elapsed.TotalHours < 1)
            {
                var minutes = Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes));
                return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
            }

            if (elapsed.TotalDays < 1)
            {
                var hours = Math.Max(1, (int)Math.Floor(elapsed.TotalHours));
                return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
            }

            if (elapsed.TotalDays < 2)
            {
                return "Yesterday";
            }

            if (elapsed.TotalDays < 30)
            {
                var days = Math.Max(1, (int)Math.Floor(elapsed.TotalDays));
                return $"{days} days ago";
            }

            return occurredAtUtc.ToLocalTime().ToString("MMM d, yyyy");
        }

    }
}
