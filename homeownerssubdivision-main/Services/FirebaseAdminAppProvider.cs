using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using HOMEOWNER.Configuration;
using Microsoft.Extensions.Options;

namespace HOMEOWNER.Services
{
    public class FirebaseAdminAppProvider : IFirebaseAdminAppProvider
    {
        private readonly FirebaseAuthenticationOptions _options;
        private FirebaseApp? _app;
        private readonly object _lock = new();

        public FirebaseAdminAppProvider(IOptions<FirebaseAuthenticationOptions> options)
        {
            _options = options.Value;
        }

        public FirebaseAuth GetAuth()
        {
            lock (_lock)
            {
                if (_app == null)
                {
                    _app = FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.GetApplicationDefault(),
                        ProjectId = _options.ProjectId
                    }, $"homeowner-admin-{_options.ProjectId}");
                }

                return FirebaseAuth.GetAuth(_app);
            }
        }
    }
}
