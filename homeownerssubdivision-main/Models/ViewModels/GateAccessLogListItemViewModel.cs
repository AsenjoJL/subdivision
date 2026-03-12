namespace HOMEOWNER.Models.ViewModels
{
    public class GateAccessLogListItemViewModel
    {
        public int LogID { get; set; }
        public int? HomeownerID { get; set; }
        public string DisplayName { get; set; } = "N/A";
        public string UserType { get; set; } = string.Empty;
        public string AccessType { get; set; } = string.Empty;
        public DateTime AccessTime { get; set; }
        public string? PlateNumber { get; set; }
        public string? GateLocation { get; set; }
        public string? VerifiedBy { get; set; }
        public string? Notes { get; set; }
        public string? VisitorName { get; set; }
    }
}
