namespace HOMEOWNER.Services
{
    public interface IUserIdentityService
    {
        Task<string?> EnsureUserAsync(UserIdentityProfile profile, CancellationToken cancellationToken = default);
        Task<string?> SyncUserProfileAsync(UserIdentityProfile profile, CancellationToken cancellationToken = default);
        Task DeleteUserAsync(string? uid, string? email = null, CancellationToken cancellationToken = default);
        Task<bool> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<UserPasswordVerificationResult> VerifyPasswordWithMigrationAsync(
            UserIdentityProfile profile,
            string? legacyPasswordHash,
            CancellationToken cancellationToken = default);
        Task<string?> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default);
    }
}
