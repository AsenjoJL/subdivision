using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models.ViewModels
{
    public class ReservationRequestViewModel
    {
        [Required]
        public int FacilityId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime ReservationDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        [StringLength(250)]
        public string Purpose { get; set; } = string.Empty;
    }
}
