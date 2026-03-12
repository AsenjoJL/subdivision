using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminAnnouncementsViewModel
    {
        public List<Announcement> Announcements { get; set; } = new();
        public int TotalAnnouncements => Announcements.Count;
        public int UrgentAnnouncements => Announcements.Count(a => a.IsUrgent);
        public int ThisWeekAnnouncements => Announcements.Count(a => a.PostedAt >= DateTime.UtcNow.AddDays(-7));
    }
}
