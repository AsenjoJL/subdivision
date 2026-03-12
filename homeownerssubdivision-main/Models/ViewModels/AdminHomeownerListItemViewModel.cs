namespace HOMEOWNER.Models.ViewModels
{
    public class AdminHomeownerListItemViewModel
    {
        public int HomeownerID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string BlockLotNumber { get; set; } = string.Empty;
        public string Role { get; set; } = "Homeowner";
        public string ProfileImageUrl { get; set; } = string.Empty;
    }
}
