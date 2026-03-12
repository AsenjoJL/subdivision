namespace HOMEOWNER.Models.ViewModels
{
    public class AdminServiceRequestsViewModel
    {
        public List<ServiceRequest> Requests { get; set; } = new();
        public List<Staff> StaffList { get; set; } = new();
        public int TotalRequests { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public string BusiestCategory { get; set; } = "No data";
        public int BusiestCategoryCount { get; set; }
        public bool HasLoadError { get; set; }
        public string LoadErrorMessage { get; set; } = string.Empty;
    }
}
