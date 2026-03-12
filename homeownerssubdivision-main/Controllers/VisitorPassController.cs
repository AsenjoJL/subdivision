using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize]
    public class VisitorPassController : BaseController
    {
        private static readonly HashSet<string> ManageableStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Approved",
            "Rejected"
        };

        public VisitorPassController(IDataService data) : base(data)
        {
        }

        // Homeowner: Request Visitor Pass (Alias for dashboard link)
        [Authorize(Roles = "Homeowner")]
        [HttpGet]
        public new async Task<IActionResult> Request()
        {
            return PartialView("Request", await BuildHomeownerViewModelAsync());
        }

        // Homeowner: Request Visitor Pass
        [Authorize(Roles = "Homeowner")]
        [HttpGet]
        public async Task<IActionResult> RequestPass()
        {
            return PartialView("Request", await BuildHomeownerViewModelAsync());
        }

        [Authorize(Roles = "Homeowner")]
        [HttpPost]
        public async Task<IActionResult> RequestPass(VisitorPassViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid data provided.",
                    validationErrors = GetValidationErrors()
                });
            }

            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Json(new { success = false, message = "Homeowner not found." });
            }

            var visitorPass = new VisitorPass
            {
                HomeownerID = homeownerId,
                VisitorName = model.VisitorName,
                VisitorPhone = model.VisitorPhone,
                VisitorIDNumber = model.VisitorIDNumber,
                VehiclePlateNumber = model.VehiclePlateNumber,
                VehicleType = model.VehicleType,
                VisitDate = model.VisitDate,
                ExpectedArrivalTime = model.ExpectedArrivalTime,
                Purpose = model.Purpose,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
            };

            await _data.AddVisitorPassAsync(visitorPass);

            return Json(new { success = true, message = "Visitor pass requested successfully! Awaiting admin approval.", passId = visitorPass.VisitorPassID });
        }

        // Homeowner: View My Visitor Passes
        [Authorize(Roles = "Homeowner")]
        public async Task<IActionResult> MyPasses()
        {
            return PartialView("Request", await BuildHomeownerViewModelAsync());
        }

        [Authorize(Roles = "Homeowner")]
        [HttpGet]
        public async Task<IActionResult> LoadMyPassCards()
        {
            var homeownerId = GetCurrentHomeownerId();
            var passes = await GetOrderedHomeownerPassesAsync(homeownerId);
            return PartialView("_HomeownerVisitorPassCards", passes);
        }

        // Admin: Manage Visitor Passes
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            return PartialView("Manage", await BuildAdminViewModelAsync());
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> LoadAdminPassCards()
        {
            var passes = await GetOrderedAdminPassesAsync();
            return PartialView("_AdminVisitorPassCards", passes);
        }

        // Admin: Approve/Reject Visitor Pass
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(status) || !ManageableStatuses.Contains(status))
            {
                return Json(new { success = false, message = "Invalid visitor pass status." });
            }

            var pass = await _data.GetVisitorPassByIdAsync(id);
            if (pass == null)
            {
                return Json(new { success = false, message = "Visitor pass not found." });
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            var admin = await _data.GetAdminByEmailAsync(email ?? "");

            pass.Status = status.Trim();
            pass.AdminNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            
            if (pass.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                pass.ApprovedAt = DateTime.UtcNow;
                pass.ApprovedByAdminID = admin?.AdminID ?? 1;
            }
            else
            {
                pass.ApprovedAt = null;
                pass.ApprovedByAdminID = null;
            }

            await _data.UpdateVisitorPassAsync(pass);

            return Json(new { success = true, message = $"Visitor pass {pass.Status.ToLower()} successfully!" });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetPassDetails(int id)
        {
            var pass = await _data.GetVisitorPassByIdAsync(id);
            if (pass == null)
            {
                return Json(new { success = false, message = "Visitor pass not found." });
            }

            var homeowner = await _data.GetHomeownerByIdAsync(pass.HomeownerID);

            return Json(new
            {
                success = true,
                pass = new
                {
                    id = pass.VisitorPassID,
                    visitorName = pass.VisitorName,
                    visitorPhone = pass.VisitorPhone,
                    visitorIdNumber = pass.VisitorIDNumber,
                    vehiclePlateNumber = pass.VehiclePlateNumber,
                    vehicleType = pass.VehicleType,
                    visitDate = pass.VisitDate.ToLocalTime().ToString("MMM dd, yyyy"),
                    expectedArrivalTime = pass.ExpectedArrivalTime?.ToString(@"hh\:mm") ?? "-",
                    purpose = pass.Purpose,
                    status = pass.Status,
                    adminNotes = pass.AdminNotes,
                    requestedAt = pass.RequestedAt.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt"),
                    approvedAt = pass.ApprovedAt?.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt") ?? "-",
                    checkedInAt = pass.CheckedInAt?.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt") ?? "-",
                    checkedOutAt = pass.CheckedOutAt?.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt") ?? "-",
                    homeownerName = homeowner?.FullName ?? "Unknown homeowner",
                    homeownerBlockLot = homeowner?.BlockLotNumber ?? "-"
                }
            });
        }

        // Admin: Check In/Out Visitor
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CheckIn(int id)
        {
            var pass = await _data.GetVisitorPassByIdAsync(id);
            if (pass == null)
            {
                return Json(new { success = false, message = "Visitor pass not found." });
            }

            pass.CheckedInAt = DateTime.UtcNow;
            await _data.UpdateVisitorPassAsync(pass);

            // Log gate access
            var log = new GateAccessLog
            {
                HomeownerID = pass.HomeownerID,
                VisitorName = pass.VisitorName,
                PlateNumber = pass.VehiclePlateNumber,
                AccessType = "Entry",
                UserType = "Visitor",
                AccessTime = DateTime.UtcNow,
                GateLocation = "Main Gate"
            };
            await _data.AddGateAccessLogAsync(log);

            return Json(new { success = true, message = "Visitor checked in successfully!" });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CheckOut(int id)
        {
            var pass = await _data.GetVisitorPassByIdAsync(id);
            if (pass == null)
            {
                return Json(new { success = false, message = "Visitor pass not found." });
            }

            pass.CheckedOutAt = DateTime.UtcNow;
            pass.Status = "Completed";
            await _data.UpdateVisitorPassAsync(pass);

            // Log gate access
            var log = new GateAccessLog
            {
                HomeownerID = pass.HomeownerID,
                VisitorName = pass.VisitorName,
                PlateNumber = pass.VehiclePlateNumber,
                AccessType = "Exit",
                UserType = "Visitor",
                AccessTime = DateTime.UtcNow,
                GateLocation = "Main Gate"
            };
            await _data.AddGateAccessLogAsync(log);

            return Json(new { success = true, message = "Visitor checked out successfully!" });
        }

        private async Task<HomeownerVisitorPassesViewModel> BuildHomeownerViewModelAsync()
        {
            var homeownerId = GetCurrentHomeownerId();
            var passes = await GetOrderedHomeownerPassesAsync(homeownerId);

            return new HomeownerVisitorPassesViewModel
            {
                NewPass = new VisitorPassViewModel
                {
                    VisitDate = DateTime.Today.AddDays(1)
                },
                Passes = passes,
                TotalPasses = passes.Count,
                PendingPasses = passes.Count(pass => pass.Status == "Pending"),
                ApprovedPasses = passes.Count(pass => pass.Status == "Approved"),
                CompletedPasses = passes.Count(pass => pass.Status == "Completed")
            };
        }

        private async Task<AdminVisitorPassManagementViewModel> BuildAdminViewModelAsync()
        {
            var passes = await GetOrderedAdminPassesAsync();

            return new AdminVisitorPassManagementViewModel
            {
                Passes = passes,
                TotalPasses = passes.Count,
                PendingPasses = passes.Count(pass => pass.Status == "Pending"),
                ApprovedPasses = passes.Count(pass => pass.Status == "Approved"),
                ActiveVisits = passes.Count(pass => pass.CheckedInAt != null && pass.CheckedOutAt == null),
                CompletedVisits = passes.Count(pass => pass.Status == "Completed"),
                Statuses = new List<string> { "All", "Pending", "Approved", "Rejected", "Completed" }
            };
        }

        private async Task<List<VisitorPass>> GetOrderedHomeownerPassesAsync(int homeownerId)
        {
            return (await _data.GetVisitorPassesByHomeownerIdAsync(homeownerId))
                .OrderByDescending(pass => pass.RequestedAt)
                .ToList();
        }

        private async Task<List<VisitorPass>> GetOrderedAdminPassesAsync()
        {
            var passes = (await _data.GetVisitorPassesAsync())
                .OrderByDescending(pass => pass.RequestedAt)
                .ToList();

            var homeownerIds = passes.Select(pass => pass.HomeownerID).Distinct().ToHashSet();
            var homeowners = (await _data.GetHomeownersAsync())
                .Where(homeowner => homeownerIds.Contains(homeowner.HomeownerID))
                .ToDictionary(homeowner => homeowner.HomeownerID);

            foreach (var pass in passes)
            {
                homeowners.TryGetValue(pass.HomeownerID, out var homeowner);
                pass.Homeowner = homeowner;
            }

            return passes;
        }

        private Dictionary<string, string[]> GetValidationErrors()
        {
            return ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());
        }
    }
}
