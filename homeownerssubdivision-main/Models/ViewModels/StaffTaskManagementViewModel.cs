using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class StaffTaskManagementViewModel
    {
        public string StaffName { get; set; } = "Staff";
        public string Position { get; set; } = "Unknown";
        public List<ServiceRequest> PendingRequests { get; set; } = new();
        public List<ServiceRequest> CompletedRequests { get; set; } = new();
    }
}
