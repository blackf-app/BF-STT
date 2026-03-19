using System.Security.Cryptography;
using System.Text;

namespace BF_STT.Services.Infrastructure
{
    internal static class SecureSettingsSerializer
    {
        public static string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            try
            {
                var encrypted = Convert.FromBase64String(value);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return value; } // plaintext fallback for migration
        }
    }
}
