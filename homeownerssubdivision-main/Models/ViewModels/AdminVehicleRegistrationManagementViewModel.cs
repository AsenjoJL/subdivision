using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminVehicleRegistrationManagementViewModel
    {
        public List<VehicleRegistration> Vehicles { get; set; } = new();
        public int TotalVehicles { get; set; }
        public int PendingVehicles { get; set; }
        public int ApprovedVehicles { get; set; }
        public int ExpiredVehicles { get; set; }
        public List<string> Statuses { get; set; } = new();
    }
}
