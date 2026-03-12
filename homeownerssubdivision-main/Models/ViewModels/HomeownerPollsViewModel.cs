using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerPollsViewModel
    {
        public List<Poll> Polls { get; set; } = new();
        public List<int> VotedPollIds { get; set; } = new();
        public int ActivePolls { get; set; }
        public int TotalVotes { get; set; }
        public int ClosingSoon { get; set; }
    }
}
