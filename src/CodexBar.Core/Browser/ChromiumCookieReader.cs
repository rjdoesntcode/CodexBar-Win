using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodexBar.Core.Models;
using Microsoft.Data.Sqlite;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace CodexBar.Core.Browser;

/// <summary>
/// Reads cookies from Chromium-based browsers (Chrome, Edge, Brave, Opera)
/// </summary>
public class ChromiumCookieReader : IBrowserCookieReader
{
    private readonly string _cookiePath;
    private readonly string _localStatePath;
    private byte[]? _encryptionKey;

    public BrowserType BrowserType { get; }

    public bool IsInstalled => File.Exists(_cookiePath);

    public ChromiumCookieReader(BrowserType browserType)
    {
        BrowserType = browserType;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        (_cookiePath, _localStatePath) = browserType switch
        {
            BrowserType.Chrome => (
                Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Network", "Cookies"),
                Path.Combine(localAppData, "Google", "Chrome", "User Data", "Local State")),
            BrowserType.Edge => (
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Network", "Cookies"),
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Local State")),
            BrowserType.Brave => (
                Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Network", "Cookies"),
                Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Local State")),
            BrowserType.Opera => (
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software", "Opera Stable", "Network", "Cookies"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software", "Opera Stable", "Local State")),
            _ => throw new ArgumentException($"Unsupported browser type: {browserType}")
        };
    }

    public async Task<IReadOnlyList<Cookie>> GetCookiesAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (!IsInstalled)
            return [];

        var cookies = new List<Cookie>();

        // Copy the cookie database to a temp file (browser may have it locked)
        var tempPath = Path.GetTempFileName();
        try
        {
            File.Copy(_cookiePath, tempPath, true);

            await using var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT name, encrypted_value, host_key, path, expires_utc, is_secure, is_httponly
                FROM cookies
                WHERE host_key LIKE @domain OR host_key LIKE @dotDomain";
            command.Parameters.AddWithValue("@domain", $"%{domain}%");
            command.Parameters.AddWithValue("@dotDomain", $"%.{domain}%");

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                var encryptedValue = (byte[])reader[1];
                var hostKey = reader.GetString(2);
                var path = reader.GetString(3);
                var expiresUtc = reader.GetInt64(4);
                var isSecure = reader.GetInt32(5) == 1;
                var isHttpOnly = reader.GetInt32(6) == 1;

                var value = await DecryptCookieValueAsync(encryptedValue, cancellationToken);
                if (value == null) continue;

                cookies.Add(new Cookie
                {
                    Name = name,
                    Value = value,
                    Domain = hostKey,
                    Path = path,
                    Expires = expiresUtc > 0 ? DateTimeOffset.FromUnixTimeSeconds((expiresUtc / 1000000) - 11644473600).UtcDateTime : null,
                    Secure = isSecure,
                    HttpOnly = isHttpOnly
                });
            }
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }

        return cookies;
    }

    public async Task<Cookie?> GetCookieAsync(string domain, string name, CancellationToken cancellationToken = default)
    {
        var cookies = await GetCookiesAsync(domain, cancellationToken);
        return cookies.FirstOrDefault(c => c.Name == name && !c.IsExpired);
    }

    private async Task<string?> DecryptCookieValueAsync(byte[] encryptedValue, CancellationToken cancellationToken)
    {
        if (encryptedValue.Length == 0)
            return null;

        // Check if it starts with "v10" or "v11" (AES-GCM encrypted)
        if (encryptedValue.Length > 3 && encryptedValue[0] == 'v' && (encryptedValue[1] == '1'))
        {
            return await DecryptAesGcmAsync(encryptedValue, cancellationToken);
        }

        // Try DPAPI decryption (older Chrome versions)
        return DecryptDpapi(encryptedValue);
    }

    private async Task<string?> DecryptAesGcmAsync(byte[] encryptedValue, CancellationToken cancellationToken)
    {
        try
        {
            var key = await GetEncryptionKeyAsync(cancellationToken);
            if (key == null) return null;

            // Skip the "v10" or "v11" prefix (3 bytes)
            var nonce = encryptedValue[3..15]; // 12 bytes nonce
            var ciphertext = encryptedValue[15..^16]; // Ciphertext (excluding tag)
            var tag = encryptedValue[^16..]; // 16 bytes tag

            // Use BouncyCastle for AES-GCM
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), 128, nonce);
            cipher.Init(false, parameters);

            // Combine ciphertext and tag for BouncyCastle
            var ciphertextWithTag = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, ciphertextWithTag, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, ciphertextWithTag, ciphertext.Length, tag.Length);

            var plaintext = new byte[cipher.GetOutputSize(ciphertextWithTag.Length)];
            var len = cipher.ProcessBytes(ciphertextWithTag, 0, ciphertextWithTag.Length, plaintext, 0);
            cipher.DoFinal(plaintext, len);

            return Encoding.UTF8.GetString(plaintext).TrimEnd('\0');
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]?> GetEncryptionKeyAsync(CancellationToken cancellationToken)
    {
        if (_encryptionKey != null) return _encryptionKey;

        if (!File.Exists(_localStatePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(_localStatePath, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("os_crypt", out var osCrypt)) return null;
            if (!osCrypt.TryGetProperty("encrypted_key", out var encryptedKeyElement)) return null;

            var encryptedKeyBase64 = encryptedKeyElement.GetString();
            if (string.IsNullOrEmpty(encryptedKeyBase64)) return null;

            var encryptedKey = Convert.FromBase64String(encryptedKeyBase64);

            // Remove "DPAPI" prefix (5 bytes)
            if (encryptedKey.Length > 5 && Encoding.ASCII.GetString(encryptedKey[..5]) == "DPAPI")
            {
                encryptedKey = encryptedKey[5..];
            }

            // Decrypt with DPAPI
            _encryptionKey = DecryptDpapiBytes(encryptedKey);
            return _encryptionKey;
        }
        catch
        {
            return null;
        }
    }

    private static string? DecryptDpapi(byte[] encryptedData)
    {
        var decrypted = DecryptDpapiBytes(encryptedData);
        return decrypted != null ? Encoding.UTF8.GetString(decrypted) : null;
    }

    private static byte[]? DecryptDpapiBytes(byte[] encryptedData)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            return ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
        }
        catch
        {
            return null;
        }
    }
}
