namespace HOMEOWNER.Services
{
    public interface IAppPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string? enteredPassword, string? storedHash);
    }
}
