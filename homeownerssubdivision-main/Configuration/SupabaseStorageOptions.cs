namespace HOMEOWNER.Configuration
{
    public class SupabaseStorageOptions
    {
        public string ProjectUrl { get; set; } = string.Empty;
        public string ServiceRoleKey { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = "profile-images";
        public string ObjectPrefix { get; set; } = "homeowners";
        public bool UseUpsert { get; set; } = true;
    }
}
