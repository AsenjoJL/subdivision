using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminVisitorPassManagementViewModel
    {
        public List<VisitorPass> Passes { get; set; } = new();
        public int TotalPasses { get; set; }
        public int PendingPasses { get; set; }
        public int ApprovedPasses { get; set; }
        public int ActiveVisits { get; set; }
        public int CompletedVisits { get; set; }
        public List<string> Statuses { get; set; } = new();
    }
}
