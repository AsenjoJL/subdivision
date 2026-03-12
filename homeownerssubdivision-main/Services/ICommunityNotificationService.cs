using HOMEOWNER.Models;

namespace HOMEOWNER.Services
{
    public interface ICommunityNotificationService
    {
        Task NotifyAllHomeownersAnnouncementAsync(Announcement announcement, CancellationToken cancellationToken = default);
        Task NotifyBillingCreatedAsync(Billing billing, Homeowner homeowner, CancellationToken cancellationToken = default);
        Task NotifyReservationStatusAsync(Reservation reservation, Homeowner homeowner, Facility? facility, bool approved, CancellationToken cancellationToken = default);
        Task NotifyBillingSubmissionReceivedAsync(Billing billing, Homeowner homeowner, CancellationToken cancellationToken = default);
        Task NotifyBillingSubmissionReviewedAsync(Billing billing, Homeowner homeowner, bool approved, CancellationToken cancellationToken = default);
        Task NotifyHomeownerAccountCreatedAsync(Homeowner homeowner, string temporaryPassword, string loginUrl, CancellationToken cancellationToken = default);
    }
}
