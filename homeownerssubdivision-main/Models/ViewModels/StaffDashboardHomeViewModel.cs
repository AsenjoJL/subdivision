namespace HOMEOWNER.Models.ViewModels
{
    public class StaffDashboardHomeViewModel
    {
        public string StaffName { get; set; } = "Staff";
        public string Position { get; set; } = "Unknown";
        public string ProfileImageUrl { get; set; } = "/restnesthome.png";
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
    }
}
