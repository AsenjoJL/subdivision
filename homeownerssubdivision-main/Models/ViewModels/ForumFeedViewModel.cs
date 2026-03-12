using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class ForumFeedViewModel
    {
        public List<ForumPost> Posts { get; set; } = new();
        public bool CanParticipate { get; set; }
    }
}
