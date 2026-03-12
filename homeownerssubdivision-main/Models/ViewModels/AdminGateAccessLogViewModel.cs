namespace HOMEOWNER.Models.ViewModels
{
    public class AdminGateAccessLogViewModel
    {
        public List<GateAccessLogListItemViewModel> Logs { get; set; } = new();
        public GateAccessStatistics Statistics { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SearchQuery { get; set; } = string.Empty;
        public string SelectedUserType { get; set; } = string.Empty;
        public string SelectedAccessType { get; set; } = string.Empty;
        public List<string> UserTypes { get; set; } = new();
        public List<string> AccessTypes { get; set; } = new();
    }
}
