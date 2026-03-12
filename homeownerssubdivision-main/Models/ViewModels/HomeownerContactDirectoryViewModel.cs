using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerContactDirectoryViewModel
    {
        public List<Contact> Contacts { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public int TotalContacts { get; set; }
        public int EmergencyContacts { get; set; }
        public string SelectedCategory { get; set; } = "All";
    }
}
