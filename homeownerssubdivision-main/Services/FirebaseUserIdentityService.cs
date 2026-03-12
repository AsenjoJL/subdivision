using System.Net;
using System.Net.Http.Json;
using FirebaseAdmin.Auth;
using HOMEOWNER.Configuration;
using Microsoft.Extensions.Options;

namespace HOMEOWNER.Services
{
    public class FirebaseUserIdentityService : IUserIdentityService
    {
        private readonly IFirebaseAdminAppProvider _firebaseAdminAppProvider;
        private readonly IAppPasswordHasher _appPasswordHasher;
        private readonly FirebaseAuthenticationOptions _options;
        private readonly HttpClient _httpClient;
        private readonly ILogger<FirebaseUserIdentityService> _logger;

        public FirebaseUserIdentityService(
            IFirebaseAdminAppProvider firebaseAdminAppProvider,
            IAppPasswordHasher appPasswordHasher,
            IOptions<FirebaseAuthenticationOptions> options,
            HttpClient httpClient,
            ILogger<FirebaseUserIdentityService> logger)
        {
            _firebaseAdminAppProvider = firebaseAdminAppProvider;
            _appPasswordHasher = appPasswordHasher;
            _options = options.Value;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string?> EnsureUserAsync(UserIdentityProfile profile, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(profile.Email))
            {
                throw new ArgumentException("Email is required.", nameof(profile));
            }

            if (string.IsNullOrWhiteSpace(profile.Password))
            {
                throw new ArgumentException("Password is required.", nameof(profile));
            }

            var auth = _firebaseAdminAppProvider.GetAuth();
            UserRecord? existingUser = null;

            if (!string.IsNullOrWhiteSpace(profile.ExistingUid))
            {
                try
                {
                    existingUser = await auth.GetUserAsync(profile.ExistingUid, cancellationToken);
                }
                catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                {
                    existingUser = null;
                }
            }

            if (existingUser == null)
            {
                try
                {
                    existingUser = await auth.GetUserByEmailAsync(profile.Email, cancellationToken);
                }
                catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                {
                    existingUser = null;
                }
            }

            if (existingUser == null)
            {
                var createArgs = new UserRecordArgs
                {
                    Email = profile.Email,
                    EmailVerified = true,
                    Password = profile.Password,
                    DisplayName = profile.DisplayName,
                    Disabled = !profile.IsEnabled
                };

                var createdUser = await auth.CreateUserAsync(createArgs, cancellationToken);
                _logger.LogInformation("Created Firebase Auth user {Email} with UID {Uid}.", profile.Email, createdUser.Uid);
                return createdUser.Uid;
            }

            var updateArgs = new UserRecordArgs
            {
                Uid = existingUser.Uid,
                Email = profile.Email,
                Password = profile.Password,
                DisplayName = profile.DisplayName,
                Disabled = !profile.IsEnabled
            };

            var updatedUser = await auth.UpdateUserAsync(updateArgs, cancellationToken);
            _logger.LogInformation("Updated Firebase Auth user {Email} with UID {Uid}.", profile.Email, updatedUser.Uid);
            return updatedUser.Uid;
        }

        public async Task<string?> SyncUserProfileAsync(UserIdentityProfile profile, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(profile.Email))
            {
                throw new ArgumentException("Email is required.", nameof(profile));
            }

            var auth = _firebaseAdminAppProvider.GetAuth();
            UserRecord? existingUser = null;

            if (!string.IsNullOrWhiteSpace(profile.ExistingUid))
            {
                try
                {
                    existingUser = await auth.GetUserAsync(profile.ExistingUid, cancellationToken);
                }
                catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                {
                    existingUser = null;
                }
            }

            if (existingUser == null)
            {
                try
                {
                    existingUser = await auth.GetUserByEmailAsync(profile.Email, cancellationToken);
                }
                catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                {
                    existingUser = null;
                }
            }

            if (existingUser == null)
            {
                return null;
            }

            var updateArgs = new UserRecordArgs
            {
                Uid = existingUser.Uid,
                Email = profile.Email,
                DisplayName = profile.DisplayName,
                Disabled = !profile.IsEnabled
            };

            var updatedUser = await auth.UpdateUserAsync(updateArgs, cancellationToken);
            _logger.LogInformation("Synchronized Firebase Auth profile {Email} with UID {Uid}.", profile.Email, updatedUser.Uid);
            return updatedUser.Uid;
        }

        public async Task DeleteUserAsync(string? uid, string? email = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(uid) && string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var auth = _firebaseAdminAppProvider.GetAuth();

            try
            {
                if (!string.IsNullOrWhiteSpace(uid))
                {
                    await auth.DeleteUserAsync(uid, cancellationToken);
                    _logger.LogInformation("Deleted Firebase Auth user with UID {Uid}.", uid);
                    return;
                }

                var user = await auth.GetUserByEmailAsync(email!, cancellationToken);
                await auth.DeleteUserAsync(user.Uid, cancellationToken);
                _logger.LogInformation("Deleted Firebase Auth user {Email} with UID {Uid}.", email, user.Uid);
            }
            catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
            {
                _logger.LogInformation("Firebase Auth user not found for deletion. Uid={Uid}, Email={Email}", uid, email);
            }
        }

        public async Task<bool> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_options.WebApiKey))
            {
                var request = new FirebasePasswordSignInRequest
                {
                    Email = email,
                    Password = password,
                    ReturnSecureToken = true
                };

                try
                {
                    using var response = await _httpClient.PostAsJsonAsync(
                        $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_options.WebApiKey}",
                        request,
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }

                    if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
                    {
                        return false;
                    }

                    _logger.LogWarning("Firebase password verification returned status code {StatusCode} for {Email}.",
                        response.StatusCode, email);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Firebase password verification failed for {Email}.", email);
                }
            }

            _logger.LogError("Firebase Authentication Web API key is not configured. Set FirebaseAuthentication__WebApiKey to enable direct Firebase Auth login.");
            return false;
        }

        public async Task<UserPasswordVerificationResult> VerifyPasswordWithMigrationAsync(
            UserIdentityProfile profile,
            string? legacyPasswordHash,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(profile.Email) || string.IsNullOrWhiteSpace(profile.Password))
            {
                return new UserPasswordVerificationResult { IsAuthenticated = false };
            }

            if (await VerifyPasswordAsync(profile.Email, profile.Password, cancellationToken))
            {
                return new UserPasswordVerificationResult
                {
                    IsAuthenticated = true,
                    UsedLegacyFallback = false,
                    FirebaseUid = profile.ExistingUid
                };
            }

            if (!_appPasswordHasher.VerifyPassword(profile.Password, legacyPasswordHash))
            {
                return new UserPasswordVerificationResult { IsAuthenticated = false };
            }

            try
            {
                var firebaseUid = await EnsureUserAsync(profile, cancellationToken);
                _logger.LogInformation("Migrated legacy password-hash login for {Email} into Firebase Authentication.", profile.Email);

                return new UserPasswordVerificationResult
                {
                    IsAuthenticated = true,
                    UsedLegacyFallback = true,
                    FirebaseUid = firebaseUid
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Legacy password migration failed for {Email}.", profile.Email);
                return new UserPasswordVerificationResult { IsAuthenticated = false };
            }
        }

        public async Task<string?> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            try
            {
                var auth = _firebaseAdminAppProvider.GetAuth();
                ActionCodeSettings? actionCodeSettings = null;

                if (!string.IsNullOrWhiteSpace(_options.PasswordResetContinueUrl))
                {
                    actionCodeSettings = new ActionCodeSettings
                    {
                        Url = _options.PasswordResetContinueUrl,
                        HandleCodeInApp = false
                    };
                }

                var resetLink = actionCodeSettings == null
                    ? await auth.GeneratePasswordResetLinkAsync(email)
                    : await auth.GeneratePasswordResetLinkAsync(email, actionCodeSettings);

                _logger.LogInformation("Generated Firebase password reset link for {Email}.", email);
                return resetLink;
            }
            catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
            {
                _logger.LogInformation("Password reset requested for unknown Firebase Auth user {Email}.", email);
                return null;
            }
        }

        private sealed class FirebasePasswordSignInRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool ReturnSecureToken { get; set; }
        }
    }
}
