using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerAnnouncementsViewModel
    {
        public List<Announcement> Announcements { get; set; } = new();
        public int TotalAnnouncements => Announcements.Count;
        public int UrgentAnnouncements => Announcements.Count(a => a.IsUrgent);
        public Announcement? LatestAnnouncement => Announcements
            .OrderByDescending(a => a.PostedAt)
            .FirstOrDefault();
    }
}
