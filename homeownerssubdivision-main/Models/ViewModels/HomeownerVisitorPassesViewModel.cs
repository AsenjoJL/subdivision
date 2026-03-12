using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerVisitorPassesViewModel
    {
        public VisitorPassViewModel NewPass { get; set; } = new();
        public List<VisitorPass> Passes { get; set; } = new();
        public int TotalPasses { get; set; }
        public int PendingPasses { get; set; }
        public int ApprovedPasses { get; set; }
        public int CompletedPasses { get; set; }
    }
}
