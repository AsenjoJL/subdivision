namespace HOMEOWNER.Configuration
{
    public class BootstrapAdminOptions
    {
        public bool Enabled { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = "System Administrator";
        public string OfficeLocation { get; set; } = "Main Office";
        public string Status { get; set; } = "Active";
        public bool OverwriteExisting { get; set; }
    }
}
