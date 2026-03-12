using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class ReservationHistoryViewModel
    {
        public List<Reservation> Reservations { get; set; } = new();
        public bool IsEmbedded { get; set; }
    }
}
