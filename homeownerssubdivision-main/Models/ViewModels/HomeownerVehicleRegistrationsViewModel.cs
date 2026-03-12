using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class VehicleOptionGroup
    {
        public string Label { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }

    public class HomeownerVehicleRegistrationsViewModel
    {
        public VehicleRegistrationViewModel NewVehicle { get; set; } = new();
        public List<VehicleRegistration> Vehicles { get; set; } = new();
        public List<VehicleOptionGroup> BrandGroups { get; set; } = new();
        public List<VehicleOptionGroup> ModelGroups { get; set; } = new();
        public List<string> ColorOptions { get; set; } = new();
        public int TotalVehicles { get; set; }
        public int PendingVehicles { get; set; }
        public int ApprovedVehicles { get; set; }
        public int ExpiringSoonVehicles { get; set; }
    }
}
