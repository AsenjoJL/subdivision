using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace HOMEOWNER.Models
{
    [FirestoreData]
    public class AdminWorkspaceSettings
    {
        [FirestoreProperty]
        [Key]
        public int AdminWorkspaceSettingsID { get; set; } = 1;

        [FirestoreProperty]
        public string WorkspaceName { get; set; } = "Admin workspace";

        [FirestoreProperty]
        public string DefaultLandingSection { get; set; } = "dashboard";

        [FirestoreProperty]
        public bool UseCompactTables { get; set; }

        [FirestoreProperty]
        public bool EnableSectionPrefetch { get; set; } = true;

        [FirestoreProperty]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
