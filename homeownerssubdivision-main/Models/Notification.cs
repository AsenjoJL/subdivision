using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace HOMEOWNER.Models
{
    [FirestoreData]
    public class Notification
    {
        [FirestoreProperty]
        [Key]
        public int NotificationID { get; set; }

        [FirestoreProperty]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Message { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Category { get; set; } = string.Empty;

        [FirestoreProperty]
        public string RecipientRole { get; set; } = string.Empty;

        [FirestoreProperty]
        public int? RecipientUserId { get; set; }

        [FirestoreProperty]
        public string? RecipientName { get; set; }

        [FirestoreProperty]
        public string? RecipientEmail { get; set; }

        [FirestoreProperty]
        public string? RecipientPhoneNumber { get; set; }

        [FirestoreProperty]
        public bool IsRead { get; set; }

        [FirestoreProperty]
        public string DeliveryStatus { get; set; } = "Queued";

        [FirestoreProperty]
        public bool EmailAttempted { get; set; }

        [FirestoreProperty]
        public bool EmailDelivered { get; set; }

        [FirestoreProperty]
        public bool SmsAttempted { get; set; }

        [FirestoreProperty]
        public bool SmsDelivered { get; set; }

        [FirestoreProperty]
        public string? RelatedEntityType { get; set; }

        [FirestoreProperty]
        public int? RelatedEntityId { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
