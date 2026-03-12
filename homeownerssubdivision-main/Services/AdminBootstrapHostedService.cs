using HOMEOWNER.Configuration;
using HOMEOWNER.Data;
using HOMEOWNER.Models;
using Microsoft.Extensions.Options;

namespace HOMEOWNER.Services
{
    public class AdminBootstrapHostedService : IHostedService
    {
        private readonly IDataService _data;
        private readonly IUserIdentityService _userIdentityService;
        private readonly BootstrapAdminOptions _options;
        private readonly ILogger<AdminBootstrapHostedService> _logger;

        public AdminBootstrapHostedService(
            IDataService data,
            IUserIdentityService userIdentityService,
            IOptions<BootstrapAdminOptions> options,
            ILogger<AdminBootstrapHostedService> logger)
        {
            _data = data;
            _userIdentityService = userIdentityService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
            {
                return;
            }

            var email = _options.Email.Trim();
            var password = _options.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Bootstrap admin is enabled, but email or password is missing. Skipping admin bootstrap.");
                return;
            }

            var existingAdmin = await _data.GetAdminByEmailAsync(email);
            if (existingAdmin != null &&
                !_options.OverwriteExisting &&
                !string.IsNullOrWhiteSpace(existingAdmin.FirebaseUid))
            {
                _logger.LogInformation("Bootstrap admin '{Email}' already exists. No changes applied.", email);
                return;
            }

            if (existingAdmin == null)
            {
                existingAdmin = new Admin();
            }

            existingAdmin.Email = email;
            existingAdmin.FullName = string.IsNullOrWhiteSpace(_options.FullName)
                ? "System Administrator"
                : _options.FullName.Trim();
            existingAdmin.OfficeLocation = string.IsNullOrWhiteSpace(_options.OfficeLocation)
                ? "Main Office"
                : _options.OfficeLocation.Trim();
            existingAdmin.Status = string.IsNullOrWhiteSpace(_options.Status)
                ? "Active"
                : _options.Status.Trim();
            existingAdmin.Role = "Admin";
            existingAdmin.PasswordHash = string.Empty;
            existingAdmin.FirebaseUid = await _userIdentityService.EnsureUserAsync(new UserIdentityProfile
            {
                Email = existingAdmin.Email,
                Password = password,
                DisplayName = existingAdmin.FullName,
                ExistingUid = existingAdmin.FirebaseUid,
                IsEnabled = string.Equals(existingAdmin.Status, "Active", StringComparison.OrdinalIgnoreCase)
            }, cancellationToken);

            if (existingAdmin.AdminID == 0)
            {
                await _data.AddAdminAsync(existingAdmin);
                _logger.LogInformation("Created bootstrap admin '{Email}' with AdminID {AdminId}.", existingAdmin.Email, existingAdmin.AdminID);
                return;
            }

            await _data.UpdateAdminAsync(existingAdmin);
            _logger.LogInformation("Updated bootstrap admin '{Email}' (AdminID {AdminId}).", existingAdmin.Email, existingAdmin.AdminID);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
