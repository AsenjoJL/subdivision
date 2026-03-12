namespace HOMEOWNER.Models.ViewModels
{
    public class AdminStaffListItemViewModel
    {
        public int StaffID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
    }
}
