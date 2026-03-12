using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class ReservationIndexViewModel
    {
        public List<Facility> Facilities { get; set; } = new();
        public List<ReservedFacilitySlotViewModel> ReservedSlots { get; set; } = new();
        public int ActivityCount { get; set; }
        public bool IsEmbedded { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
