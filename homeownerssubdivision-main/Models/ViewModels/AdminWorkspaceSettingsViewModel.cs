using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminWorkspaceSettingsViewModel
    {
        public string WorkspaceName { get; set; } = "Admin workspace";
        public string DefaultLandingSection { get; set; } = "dashboard";
        public bool UseCompactTables { get; set; }
        public bool EnableSectionPrefetch { get; set; } = true;
        public List<KeyValuePair<string, string>> AvailableSections { get; set; } = new();
    }

    public class AdminWorkspaceSettingsUpdateViewModel
    {
        [Required]
        public string WorkspaceName { get; set; } = "Admin workspace";

        [Required]
        public string DefaultLandingSection { get; set; } = "dashboard";

        public bool UseCompactTables { get; set; }

        public bool EnableSectionPrefetch { get; set; } = true;
    }
}
