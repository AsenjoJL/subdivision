namespace HOMEOWNER.Models.ViewModels
{
    public class AdminDashboardOverviewViewModel
    {
        public int HomeownerCount { get; set; }
        public int StaffCount { get; set; }
        public int ReservationCount { get; set; }
        public int NotificationCount { get; set; }
        public List<DashboardChartPointViewModel> FacilityUsage { get; set; } = new();
        public List<DashboardActivityItemViewModel> RecentActivity { get; set; } = new();
    }

    public class DashboardChartPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class DashboardActivityItemViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }
}
