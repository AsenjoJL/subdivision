namespace HOMEOWNER.Models.ViewModels
{
    public class ReservedFacilitySlotViewModel
    {
        public int FacilityId { get; set; }
        public DateTime Date { get; set; }
        public string Start { get; set; } = "00:00";
        public string End { get; set; } = "00:00";
    }
}
