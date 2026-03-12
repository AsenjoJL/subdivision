using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminProfileSettingsViewModel
    {
        public int AdminID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string OfficeLocation { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
    }

    public class AdminProfileSettingsUpdateViewModel
    {
        [Required]
        public int AdminID { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string OfficeLocation { get; set; } = string.Empty;
    }
}
