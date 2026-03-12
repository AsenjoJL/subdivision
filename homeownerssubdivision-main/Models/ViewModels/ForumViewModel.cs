using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class ForumViewModel
    {
        public List<ForumPost> Posts { get; set; } = new();
        public CommunitySettings Settings { get; set; } = new();
        public bool CanManageSettings { get; set; }
        public bool CanParticipate { get; set; }
        public bool ShowBackButton { get; set; }
        public string BackUrl { get; set; } = "/";
        public long LatestActivityTicks { get; set; }
    }
}
