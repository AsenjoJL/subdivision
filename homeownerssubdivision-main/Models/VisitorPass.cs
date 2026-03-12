using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace HOMEOWNER.Models
{
    [FirestoreData]
    public class VisitorPass
    {
        private string? _expectedArrivalTimeValue;

        [FirestoreProperty]
        [Key]
        public int VisitorPassID { get; set; }

        [FirestoreProperty]
        [Required]
        public int HomeownerID { get; set; }

        [FirestoreProperty]
        [Required]
        [StringLength(100)]
        public string VisitorName { get; set; } = string.Empty;

        [FirestoreProperty]
        [StringLength(20)]
        public string? VisitorPhone { get; set; }

        [FirestoreProperty]
        [StringLength(50)]
        public string? VisitorIDNumber { get; set; } // ID type and number

        [FirestoreProperty]
        [StringLength(100)]
        public string? VehiclePlateNumber { get; set; }

        [FirestoreProperty]
        [StringLength(50)]
        public string? VehicleType { get; set; } // Car, Motorcycle, etc.

        [FirestoreProperty]
        [Required]
        public DateTime VisitDate { get; set; }

        [FirestoreProperty("ExpectedArrivalTime")]
        public string? ExpectedArrivalTimeValue
        {
            get => _expectedArrivalTimeValue;
            set => _expectedArrivalTimeValue = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public TimeSpan? ExpectedArrivalTime
        {
            get => TimeSpan.TryParse(ExpectedArrivalTimeValue, out var parsed) ? parsed : null;
            set => ExpectedArrivalTimeValue = value?.ToString(@"hh\:mm\:ss");
        }

        [FirestoreProperty]
        [StringLength(500)]
        public string? Purpose { get; set; }

        [FirestoreProperty]
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Expired, Completed

        [FirestoreProperty]
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public DateTime? ApprovedAt { get; set; }

        [FirestoreProperty]
        public int? ApprovedByAdminID { get; set; }

        [FirestoreProperty]
        public DateTime? CheckedInAt { get; set; }

        [FirestoreProperty]
        public DateTime? CheckedOutAt { get; set; }

        [FirestoreProperty]
        [StringLength(500)]
        public string? AdminNotes { get; set; }

        // Navigation property
        public Homeowner? Homeowner { get; set; }
    }
}
