using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerComplaintsViewModel
    {
        public ComplaintViewModel NewComplaint { get; set; } = new();

        public IReadOnlyList<Complaint> Complaints { get; set; } = Array.Empty<Complaint>();

        public int OpenCount => Complaints.Count(complaint =>
            !string.Equals(complaint.Status, "Resolved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(complaint.Status, "Closed", StringComparison.OrdinalIgnoreCase));
    }
}
