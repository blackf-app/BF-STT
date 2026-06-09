using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
#if WINDOWS
using WindowsProtectedData = System.Security.Cryptography.ProtectedData;
#endif

namespace BF_STT.Services.Infrastructure
{
    /// <summary>
    /// Encrypts/decrypts API keys at rest.
    ///   - Windows: uses DPAPI (ProtectedData) tied to the current user.
    ///   - macOS / other: uses AES-256 with a key derived from a per-user file
    ///     placed in the app data folder. Good enough to keep keys out of plain text
    ///     in personal-use builds; not a secure secret store.
    /// </summary>
    internal static class SecureSettingsSerializer
    {
        public static string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;
#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                return WindowsEncrypt(plaintext);
            }
#endif
            return AesEncrypt(plaintext);
        }

        public static string Decrypt(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            try
            {
#if WINDOWS
                if (OperatingSystem.IsWindows())
                {
                    return WindowsDecrypt(value);
                }
#endif
                return AesDecrypt(value);
            }
            catch { return value; }
        }

#if WINDOWS
        [SupportedOSPlatform("windows")]
        private static string WindowsEncrypt(string plaintext)
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = WindowsProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        [SupportedOSPlatform("windows")]
        private static string WindowsDecrypt(string value)
        {
            var encrypted = Convert.FromBase64String(value);
            var decrypted = WindowsProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
#endif

        private static readonly Lazy<byte[]> AesKey = new(LoadOrCreateAesKey);

        private static byte[] LoadOrCreateAesKey()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BF-STT");
            Directory.CreateDirectory(appData);
            var keyPath = Path.Combine(appData, ".keyfile");

            if (File.Exists(keyPath))
            {
                var existing = File.ReadAllBytes(keyPath);
                if (existing.Length == 32) return existing;
            }

            var newKey = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(keyPath, newKey);

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                try { File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
                catch { /* best effort */ }
            }
            return newKey;
        }

        private static string AesEncrypt(string plaintext)
        {
            using var aes = Aes.Create();
            aes.Key = AesKey.Value;
            aes.GenerateIV();

            var plain = Encoding.UTF8.GetBytes(plaintext);
            using var encryptor = aes.CreateEncryptor();
            var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

            var result = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
            return Convert.ToBase64String(result);
        }

        private static string AesDecrypt(string value)
        {
            var raw = Convert.FromBase64String(value);
            using var aes = Aes.Create();
            aes.Key = AesKey.Value;

            var iv = new byte[aes.BlockSize / 8];
            Buffer.BlockCopy(raw, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(raw, iv.Length, raw.Length - iv.Length);
            return Encoding.UTF8.GetString(plain);
        }
    }
}
