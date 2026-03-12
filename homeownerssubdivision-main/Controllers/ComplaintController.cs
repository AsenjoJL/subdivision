using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize]
    public class ComplaintController : BaseController
    {
        public ComplaintController(IDataService data) : base(data)
        {
        }

        // Homeowner: Submit Complaint
        [Authorize(Roles = "Homeowner")]
        [HttpGet]
        public async Task<IActionResult> Submit()
        {
            var model = await BuildHomeownerComplaintsViewModelAsync();
            return PartialView("Submit", model);
        }

        [Authorize(Roles = "Homeowner")]
        [HttpPost]
        public async Task<IActionResult> Submit(ComplaintViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please review the complaint details and try again.",
                    validationErrors = ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray())
                });
            }

            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Json(new { success = false, message = "Homeowner not found." });
            }

            var complaint = new Complaint
            {
                HomeownerID = homeownerId,
                Subject = model.Subject,
                Description = model.Description,
                Category = model.Category,
                Status = "Submitted",
                SubmittedAt = DateTime.UtcNow,
                Priority = model.Priority,
                IsAnonymous = model.IsAnonymous
            };

            await _data.AddComplaintAsync(complaint);

            return Json(new { success = true, message = "Complaint submitted successfully! Your complaint ID is #" + complaint.ComplaintID, complaintId = complaint.ComplaintID });
        }

        // Homeowner: View My Complaints
        [Authorize(Roles = "Homeowner")]
        public async Task<IActionResult> MyComplaints()
        {
            var model = await BuildHomeownerComplaintsViewModelAsync();
            return PartialView("MyComplaints", model);
        }

        [Authorize(Roles = "Homeowner")]
        public async Task<IActionResult> LoadHomeownerComplaints()
        {
            var homeownerId = GetCurrentHomeownerId();
            var complaints = await _data.GetComplaintsByHomeownerIdAsync(homeownerId);
            return PartialView("_HomeownerComplaintRows", complaints.OrderByDescending(c => c.SubmittedAt).ToList());
        }

        // Homeowner: View Complaint Details
        [Authorize(Roles = "Homeowner")]
        public async Task<IActionResult> Details(int id)
        {
            var complaint = await _data.GetComplaintByIdAsync(id);
            if (complaint == null)
            {
                return NotFound();
            }

            var homeownerId = GetCurrentHomeownerId();
            if (complaint.HomeownerID != homeownerId)
            {
                return Forbid();
            }

            return PartialView("Details", complaint);
        }

        // Admin: Manage Complaints
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var complaints = await GetOrderedComplaintsAsync();
            return PartialView("Manage", new AdminComplaintsViewModel
            {
                Complaints = complaints
            });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LoadComplaintRows()
        {
            var complaints = await GetOrderedComplaintsAsync();
            return PartialView("_AdminComplaintRows", complaints);
        }

        // Admin: View Complaint Details
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDetails(int id)
        {
            var complaint = await _data.GetComplaintByIdAsync(id);
            if (complaint == null)
            {
                return NotFound();
            }

            var homeowner = await _data.GetHomeownerByIdAsync(complaint.HomeownerID);
            return Json(new
            {
                success = true,
                complaint = new
                {
                    complaintId = complaint.ComplaintID,
                    homeowner = complaint.IsAnonymous ? "Anonymous homeowner" : (homeowner?.FullName ?? "Unknown homeowner"),
                    subject = complaint.Subject,
                    category = complaint.Category ?? "Other",
                    priority = GetPriorityLabel(complaint.Priority),
                    status = complaint.Status,
                    submittedAt = complaint.SubmittedAt.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt"),
                    description = complaint.Description,
                    adminResponse = complaint.AdminResponse ?? string.Empty,
                    resolutionNotes = complaint.ResolutionNotes ?? string.Empty,
                    reviewedAt = complaint.ReviewedAt?.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt") ?? "-",
                    resolvedAt = complaint.ResolvedAt?.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt") ?? "-"
                }
            });
        }

        // Admin: Update Complaint Status
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(UpdateComplaintStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please provide a valid complaint update." });
            }

            var complaint = await _data.GetComplaintByIdAsync(model.Id);
            if (complaint == null)
            {
                return Json(new { success = false, message = "Complaint not found." });
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            var admin = await _data.GetAdminByEmailAsync(email ?? "");

            complaint.Status = model.Status;
            complaint.AdminResponse = model.Response;
            complaint.ResolutionNotes = model.ResolutionNotes;

            if (string.Equals(model.Status, "Under Review", StringComparison.OrdinalIgnoreCase) && complaint.ReviewedAt == null)
            {
                complaint.ReviewedAt = DateTime.UtcNow;
                complaint.ReviewedByAdminID = admin?.AdminID ?? 1;
            }

            if (string.Equals(model.Status, "Resolved", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                complaint.ResolvedAt = DateTime.UtcNow;
            }

            await _data.UpdateComplaintAsync(complaint);

            return Json(new { success = true, message = $"Complaint status updated to {model.Status} successfully!" });
        }

        // Admin: Delete Complaint
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var complaint = await _data.GetComplaintByIdAsync(id);
            if (complaint == null)
            {
                return Json(new { success = false, message = "Complaint not found." });
            }

            await _data.DeleteComplaintAsync(id);
            return Json(new { success = true, message = "Complaint deleted successfully!" });
        }

        private async Task<HomeownerComplaintsViewModel> BuildHomeownerComplaintsViewModelAsync()
        {
            var homeownerId = GetCurrentHomeownerId();
            var complaints = await _data.GetComplaintsByHomeownerIdAsync(homeownerId);
            return new HomeownerComplaintsViewModel
            {
                Complaints = complaints.OrderByDescending(c => c.SubmittedAt).ToList()
            };
        }

        private async Task<List<Complaint>> GetOrderedComplaintsAsync()
        {
            return (await _data.GetComplaintsAsync())
                .OrderByDescending(c => c.SubmittedAt)
                .ToList();
        }

        private static string GetPriorityLabel(int? priority)
        {
            return priority switch
            {
                4 => "Urgent",
                3 => "High",
                2 => "Medium",
                _ => "Low"
            };
        }
    }
}
