using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class AnnouncementFormViewModel
    {
        public int AnnouncementID { get; set; }

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1200)]
        public string Content { get; set; } = string.Empty;

        public bool IsUrgent { get; set; }
    }
}
