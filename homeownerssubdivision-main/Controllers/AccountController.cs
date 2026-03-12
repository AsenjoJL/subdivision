using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    public class AccountController : Controller
    {
        private readonly IDataService _data;
        private readonly IUserIdentityService _userIdentityService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IDataService data,
            IUserIdentityService userIdentityService,
            IWebHostEnvironment environment,
            ILogger<AccountController> logger)
        {
            _data = data;
            _userIdentityService = userIdentityService;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email ?? string.Empty;

            try
            {
                var adminTask = _data.GetAdminByEmailAsync(email);
                var homeownerTask = _data.GetHomeownerByEmailAsync(email);
                var staffTask = _data.GetStaffByEmailAsync(email);

                await Task.WhenAll(adminTask, homeownerTask, staffTask);

                var userExists =
                    adminTask.Result != null ||
                    homeownerTask.Result != null ||
                    staffTask.Result != null;

                if (userExists)
                {
                    var resetLink = await _userIdentityService.GeneratePasswordResetLinkAsync(email);
                    if (_environment.IsDevelopment() && !string.IsNullOrWhiteSpace(resetLink))
                    {
                        ViewBag.ResetLink = resetLink;
                    }
                }

                ViewBag.SuccessMessage = "If an account exists for that email, password reset instructions have been prepared.";
                ModelState.Clear();
                return View(new ForgotPasswordViewModel());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset request failed for {Email}.", email);
                ViewBag.ErrorMessage = "Unable to process the password reset request right now.";
                return View(model);
            }
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email ?? string.Empty;
            var password = model.Password ?? string.Empty;
            var admin = await _data.GetAdminByEmailAsync(email);
            if (admin != null)
            {
                var verification = await _userIdentityService.VerifyPasswordWithMigrationAsync(
                    new UserIdentityProfile
                    {
                        Email = admin.Email,
                        Password = password,
                        DisplayName = admin.FullName ?? string.Empty,
                        ExistingUid = admin.FirebaseUid,
                        IsEnabled = string.Equals(admin.Status, "Active", StringComparison.OrdinalIgnoreCase)
                    },
                    admin.PasswordHash);

                if (verification.IsAuthenticated)
                {
                    if (!string.IsNullOrWhiteSpace(verification.FirebaseUid) &&
                        !string.Equals(admin.FirebaseUid, verification.FirebaseUid, StringComparison.Ordinal))
                    {
                        admin.FirebaseUid = verification.FirebaseUid;
                    }

                    if (!string.IsNullOrWhiteSpace(admin.PasswordHash))
                    {
                        admin.PasswordHash = string.Empty;
                        await _data.UpdateAdminAsync(admin);
                    }

                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, admin.FullName ?? "Unknown Admin"),
                        new(ClaimTypes.Email, admin.Email ?? "unknown@domain.com"),
                        new(ClaimTypes.Role, "Admin"),
                        new("AdminID", admin.AdminID.ToString())
                    };

                    await SignInUser(claims);
                    HttpContext.Session.SetInt32("AdminID", admin.AdminID);
                    return RedirectToAction("Dashboard", "Admin");
                }

                ModelState.AddModelError("", "Invalid password for admin account.");
                return View(model);
            }

            var homeowner = await _data.GetHomeownerByEmailAsync(email);
            if (homeowner != null)
            {
                var verification = await _userIdentityService.VerifyPasswordWithMigrationAsync(
                    new UserIdentityProfile
                    {
                        Email = homeowner.Email ?? string.Empty,
                        Password = password,
                        DisplayName = homeowner.FullName ?? string.Empty,
                        ExistingUid = homeowner.FirebaseUid,
                        IsEnabled = homeowner.IsActive
                    },
                    homeowner.PasswordHash);

                if (!verification.IsAuthenticated)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                if (!string.IsNullOrWhiteSpace(verification.FirebaseUid) &&
                    !string.Equals(homeowner.FirebaseUid, verification.FirebaseUid, StringComparison.Ordinal))
                {
                    homeowner.FirebaseUid = verification.FirebaseUid;
                }

                if (!string.IsNullOrWhiteSpace(homeowner.PasswordHash))
                {
                    homeowner.PasswordHash = string.Empty;
                    await _data.UpdateHomeownerAsync(homeowner);
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, homeowner.FullName ?? "Unknown Homeowner"),
                    new(ClaimTypes.Email, homeowner.Email ?? "unknown@domain.com"),
                    new(ClaimTypes.Role, "Homeowner"),
                    new("HomeownerID", homeowner.HomeownerID.ToString())
                };

                await SignInUser(claims);
                HttpContext.Session.SetInt32("HomeownerID", homeowner.HomeownerID);
                return RedirectToAction("Dashboard", "Homeowner");
            }

            var staff = await _data.GetStaffByEmailAsync(email);
            if (staff != null)
            {
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
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                if (!string.IsNullOrWhiteSpace(verification.FirebaseUid) &&
                    !string.Equals(staff.FirebaseUid, verification.FirebaseUid, StringComparison.Ordinal))
                {
                    staff.FirebaseUid = verification.FirebaseUid;
                }

                if (!string.IsNullOrWhiteSpace(staff.PasswordHash))
                {
                    staff.PasswordHash = string.Empty;
                    await _data.UpdateStaffAsync(staff);
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, staff.FullName ?? "Unknown Staff"),
                    new(ClaimTypes.Email, staff.Email ?? "unknown@domain.com"),
                    new(ClaimTypes.Role, "Staff"),
                    new("Position", staff.Position ?? "Unknown"),
                    new("StaffID", staff.StaffID.ToString())
                };

                await SignInUser(claims);
                HttpContext.Session.SetString("StaffRole", staff.Position ?? "Unknown");
                HttpContext.Session.SetInt32("StaffID", staff.StaffID);
                HttpContext.Session.SetString("StaffName", staff.FullName ?? "Unknown Staff");

                return RedirectToAction("Dashboard", "Staff");
            }

            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        private async Task SignInUser(List<Claim> claims)
        {
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
