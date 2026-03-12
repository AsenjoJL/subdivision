using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HOMEOWNER.Models.ViewModels
{
    public class FacilityFormViewModel
    {
        public int FacilityID { get; set; }

        [Required]
        public string FacilityName { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than zero.")]
        public int Capacity { get; set; }

        [Required]
        public string AvailabilityStatus { get; set; } = "Available";

        public string ExistingImageUrl { get; set; } = string.Empty;

        public List<IFormFile> ImageFiles { get; set; } = new();
    }
}
