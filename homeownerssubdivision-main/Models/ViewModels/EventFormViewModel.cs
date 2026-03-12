using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class EventFormViewModel
    {
        public int EventID { get; set; }

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [StringLength(60)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(140)]
        public string Location { get; set; } = string.Empty;
    }
}
