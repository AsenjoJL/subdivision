namespace HOMEOWNER.Models.ViewModels
{
    public class ReservationReportRequestViewModel
    {
        public string ReportType { get; set; } = "summary";

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int? FacilityId { get; set; }
    }
}
