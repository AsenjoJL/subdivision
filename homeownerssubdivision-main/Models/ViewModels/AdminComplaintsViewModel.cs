using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminComplaintsViewModel
    {
        public IReadOnlyList<Complaint> Complaints { get; set; } = Array.Empty<Complaint>();

        public int TotalCount => Complaints.Count;

        public int OpenCount => Complaints.Count(complaint =>
            !string.Equals(complaint.Status, "Resolved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(complaint.Status, "Closed", StringComparison.OrdinalIgnoreCase));

        public int HighPriorityCount => Complaints.Count(complaint => complaint.Priority >= 3);
    }
}
