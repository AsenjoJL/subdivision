namespace HOMEOWNER.Services
{
    public class UserPasswordVerificationResult
    {
        public bool IsAuthenticated { get; init; }
        public bool UsedLegacyFallback { get; init; }
        public string? FirebaseUid { get; init; }
    }
}
