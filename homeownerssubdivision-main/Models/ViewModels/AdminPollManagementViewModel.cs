using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminPollManagementViewModel
    {
        public List<Poll> Polls { get; set; } = new();
        public List<string> Statuses { get; set; } = new() { "All", "Draft", "Active", "Closed" };
        public int TotalPolls { get; set; }
        public int ActivePolls { get; set; }
        public int TotalVotes { get; set; }
    }
}
