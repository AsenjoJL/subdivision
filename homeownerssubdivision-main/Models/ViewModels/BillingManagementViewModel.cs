using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class BillingManagementViewModel
    {
        public List<BillingListItemViewModel> Billings { get; set; } = new();
        public List<BillingHomeownerOptionViewModel> Homeowners { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public int TotalBills { get; set; }
        public int PaidBills { get; set; }
        public int PendingBills { get; set; }
        public int OverdueBills { get; set; }
        public int SubmittedPayments { get; set; }
    }

    public class BillingListItemViewModel
    {
        public int BillingID { get; set; }
        public int HomeownerID { get; set; }
        public string HomeownerName { get; set; } = "Unknown homeowner";
        public string HomeownerEmail { get; set; } = "-";
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string BillType { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public bool IsOverdue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionID { get; set; }
        public string PaymentSubmissionStatus { get; set; } = "None";
        public DateTime? PaymentSubmittedAt { get; set; }
        public decimal? SubmittedAmount { get; set; }
        public string? SubmittedPaymentMethod { get; set; }
        public string? SubmittedReferenceNumber { get; set; }
        public string? PaymentSubmissionNotes { get; set; }
        public string? PaymentProofUrl { get; set; }
        public DateTime? PaymentReviewedAt { get; set; }
        public string? PaymentReviewedBy { get; set; }
        public string? PaymentReviewNotes { get; set; }
    }

    public class BillingHomeownerOptionViewModel
    {
        public int HomeownerID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Label => $"{FullName} ({Email})";
    }

    public class CreateBillingViewModel
    {
        [Required]
        public int HomeownerID { get; set; }

        [Required]
        [StringLength(255)]
        public string Description { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        [StringLength(50)]
        public string BillType { get; set; } = string.Empty;
    }

    public class UpdateBillingStatusViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty;

        public string? PaymentMethod { get; set; }
        public string? TransactionID { get; set; }
    }

    public class SubmitPaymentViewModel
    {
        [Required]
        public int BillingID { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal SubmittedAmount { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class ReviewPaymentSubmissionViewModel
    {
        [Required]
        public int BillingID { get; set; }

        [Required]
        [StringLength(20)]
        public string Action { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ReviewNotes { get; set; }
    }

    public class HomeownerBillingViewModel
    {
        public string HomeownerName { get; set; } = string.Empty;
        public List<BillingListItemViewModel> Billings { get; set; } = new();
        public decimal OutstandingBalance { get; set; }
        public int PendingBills { get; set; }
        public int OverdueBills { get; set; }
        public int PaidBills { get; set; }
        public int SubmittedBills { get; set; }
    }
}
