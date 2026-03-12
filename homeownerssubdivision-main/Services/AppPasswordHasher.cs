using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace HOMEOWNER.Services
{
    public class AppPasswordHasher : IAppPasswordHasher
    {
        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentNullException(nameof(password), "Password cannot be empty or null.");
            }

            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 32);

            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public bool VerifyPassword(string? enteredPassword, string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(enteredPassword) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            var parts = storedHash.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[0]);
                var storedHashBytes = Convert.FromBase64String(parts[1]);
                var enteredHashBytes = KeyDerivation.Pbkdf2(
                    password: enteredPassword,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 100000,
                    numBytesRequested: 32);

                return CryptographicOperations.FixedTimeEquals(enteredHashBytes, storedHashBytes);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
