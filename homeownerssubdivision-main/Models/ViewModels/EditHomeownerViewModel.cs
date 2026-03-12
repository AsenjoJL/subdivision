using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class EditHomeownerViewModel
    {
        [Required]
        public int HomeownerID { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        public string BlockLotNumber { get; set; } = string.Empty;
    }
}
