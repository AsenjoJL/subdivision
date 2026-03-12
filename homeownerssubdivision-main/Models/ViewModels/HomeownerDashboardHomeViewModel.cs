namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerDashboardHomeViewModel
    {
        public string HomeownerName { get; set; } = "Homeowner";
        public int ActiveReservations { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int OpenServiceRequests { get; set; }
    }
}
