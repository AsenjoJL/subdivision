using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerSettingsViewModel
    {
        public int HomeownerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string BlockLotNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public string ProfileImageUrl { get; set; } = string.Empty;
        public bool IsDevelopment { get; set; }
    }

    public class HomeownerSettingsUpdateViewModel
    {
        [Required]
        public int HomeownerId { get; set; }

        [Required]
        [StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(30)]
        public string ContactNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string BlockLotNumber { get; set; } = string.Empty;
    }
}
