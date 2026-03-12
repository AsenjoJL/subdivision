namespace HOMEOWNER.Services
{
    public class UserIdentityProfile
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? ExistingUid { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
