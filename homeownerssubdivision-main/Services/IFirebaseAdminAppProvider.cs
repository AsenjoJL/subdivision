using FirebaseAdmin.Auth;

namespace HOMEOWNER.Services
{
    public interface IFirebaseAdminAppProvider
    {
        FirebaseAuth GetAuth();
    }
}
