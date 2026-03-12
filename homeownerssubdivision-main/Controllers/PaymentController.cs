using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Homeowner")]
    public class PaymentController : BaseController
    {
        private readonly ICommunityNotificationService _notificationService;
        private readonly IAppFileStorageService _fileStorageService;

        public PaymentController(
            IDataService data,
            ICommunityNotificationService notificationService,
            IAppFileStorageService fileStorageService) : base(data)
        {
            _notificationService = notificationService;
            _fileStorageService = fileStorageService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildHomeownerBillingViewModelAsync(homeownerId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Index", model);
            }

            return View("Index", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitPayment(SubmitPaymentViewModel model, IFormFile? proofFile)
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Json(new { success = false, message = "You must be logged in to submit a payment." });
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

            var billing = await _data.GetBillingByIdAsync(model.BillingID);
            if (billing == null || billing.HomeownerID != homeownerId)
            {
                return Json(new { success = false, message = "Billing record not found." });
            }

            if (string.Equals(billing.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "This bill has already been paid." });
            }

            var normalizedMethod = model.PaymentMethod.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMethod))
            {
                return Json(new { success = false, message = "Payment method is required.", field = nameof(model.PaymentMethod) });
            }

            var normalizedReference = model.ReferenceNumber?.Trim();
            if (proofFile == null && string.IsNullOrWhiteSpace(normalizedReference))
            {
                return Json(new
                {
                    success = false,
                    message = "Add a proof file or a payment reference number before submitting.",
                    field = nameof(model.ReferenceNumber)
                });
            }

            if (proofFile != null && proofFile.Length > 0)
            {
                const int maxProofSize = 10 * 1024 * 1024;
                if (proofFile.Length > maxProofSize)
                {
                    return Json(new { success = false, message = "Payment proof exceeds the 10MB limit.", field = "ProofFile" });
                }

                billing.PaymentProofUrl = await UploadProofAsync(proofFile);
            }

            billing.PaymentSubmissionStatus = "Submitted";
            billing.PaymentSubmittedAt = DateTime.UtcNow;
            billing.SubmittedAmount = model.SubmittedAmount;
            billing.SubmittedPaymentMethod = normalizedMethod;
            billing.SubmittedReferenceNumber = normalizedReference;
            billing.PaymentSubmissionNotes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            billing.PaymentReviewedAt = null;
            billing.PaymentReviewedBy = null;
            billing.PaymentReviewNotes = null;

            await _data.UpdateBillingAsync(billing);
            var homeowner = await _data.GetHomeownerByIdAsync(homeownerId);
            if (homeowner != null)
            {
                await _notificationService.NotifyBillingSubmissionReceivedAsync(billing, homeowner);
            }

            return Json(new { success = true, message = "Payment submitted successfully. The admin team will review it shortly." });
        }

        private async Task<HomeownerBillingViewModel> BuildHomeownerBillingViewModelAsync(int homeownerId)
        {
            var homeownerTask = _data.GetHomeownerByIdAsync(homeownerId);
            var billingsTask = _data.GetBillingsByHomeownerIdAsync(homeownerId);

            await Task.WhenAll(homeownerTask, billingsTask);

            var homeowner = homeownerTask.Result;
            var nowUtc = DateTime.UtcNow;
            var billingItems = billingsTask.Result
                .Select(b => MapBilling(b, homeowner?.FullName ?? "Homeowner", homeowner?.Email ?? "-", nowUtc))
                .OrderByDescending(b => b.CreatedAt)
                .ToList();

            return new HomeownerBillingViewModel
            {
                HomeownerName = homeowner?.FullName ?? GetCurrentUserName() ?? "Homeowner",
                Billings = billingItems,
                OutstandingBalance = billingItems
                    .Where(b => !string.Equals(b.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(b => b.Amount),
                PendingBills = billingItems.Count(b => string.Equals(b.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
                OverdueBills = billingItems.Count(b => string.Equals(b.Status, "Overdue", StringComparison.OrdinalIgnoreCase)),
                PaidBills = billingItems.Count(b => string.Equals(b.Status, "Paid", StringComparison.OrdinalIgnoreCase)),
                SubmittedBills = billingItems.Count(b => string.Equals(b.PaymentSubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
            };
        }

        private BillingListItemViewModel MapBilling(Billing billing, string homeownerName, string homeownerEmail, DateTime nowUtc)
        {
            var isOverdue = !string.Equals(billing.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                && billing.DueDate.Date < nowUtc.Date;

            return new BillingListItemViewModel
            {
                BillingID = billing.BillingID,
                HomeownerID = billing.HomeownerID,
                HomeownerName = homeownerName,
                HomeownerEmail = homeownerEmail,
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

        private async Task<string> UploadProofAsync(IFormFile file)
        {
            return await _fileStorageService.UploadPaymentProofAsync(file, HttpContext.RequestAborted);
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
