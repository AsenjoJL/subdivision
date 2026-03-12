namespace HOMEOWNER.Models.ViewModels
{
    public class ReservationManagementViewModel
    {
        public List<Facility> Facilities { get; set; } = new();

        public List<Reservation> ActiveReservations { get; set; } = new();

        public int TotalReservations { get; set; }

        public int ApprovedCount { get; set; }

        public int PendingCount { get; set; }

        public int RejectedCount { get; set; }

        public int CancelledCount { get; set; }

        public int ExpiredCount { get; set; }

        public int ApprovalRate { get; set; }

        public string PopularFacility { get; set; } = "No data";

        public int PopularFacilityCount { get; set; }

        public string PeakDay { get; set; } = "No data";

        public int PeakDayCount { get; set; }

        public bool HasLoadError { get; set; }

        public string LoadErrorMessage { get; set; } = string.Empty;
    }
}
