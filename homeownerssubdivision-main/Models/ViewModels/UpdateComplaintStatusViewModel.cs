using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class UpdateComplaintStatusViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string Status { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Response { get; set; }

        [StringLength(500)]
        public string? ResolutionNotes { get; set; }
    }
}
