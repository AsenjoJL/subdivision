using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class VisitorPassViewModel
    {
        [Required]
        public string VisitorName { get; set; } = string.Empty;
        
        [Required]
        public string? VisitorPhone { get; set; }
        
        public string? VisitorIDNumber { get; set; }
        
        public string? VehiclePlateNumber { get; set; }
        
        public string? VehicleType { get; set; }
        
        [Required]
        public DateTime VisitDate { get; set; }
        
        [Required]
        public TimeSpan? ExpectedArrivalTime { get; set; }
        
        [Required]
        public string? Purpose { get; set; }
    }
}
