using CodexBar.Core.Models;
using Microsoft.Data.Sqlite;

namespace CodexBar.Core.Browser;

/// <summary>
/// Reads cookies from Firefox browser
/// </summary>
public class FirefoxCookieReader : IBrowserCookieReader
{
    private readonly string _profilePath;

    public BrowserType BrowserType => BrowserType.Firefox;

    public bool IsInstalled => Directory.Exists(_profilePath) && GetDefaultProfilePath() != null;

    public FirefoxCookieReader()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _profilePath = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");
    }

    public async Task<IReadOnlyList<Cookie>> GetCookiesAsync(string domain, CancellationToken cancellationToken = default)
    {
        var profilePath = GetDefaultProfilePath();
        if (profilePath == null)
            return [];

        var cookiePath = Path.Combine(profilePath, "cookies.sqlite");
        if (!File.Exists(cookiePath))
            return [];

        var cookies = new List<Cookie>();

        // Copy the cookie database to a temp file (browser may have it locked)
        var tempPath = Path.GetTempFileName();
        try
        {
            File.Copy(cookiePath, tempPath, true);

            await using var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT name, value, host, path, expiry, isSecure, isHttpOnly
                FROM moz_cookies
                WHERE host LIKE @domain OR host LIKE @dotDomain";
            command.Parameters.AddWithValue("@domain", $"%{domain}%");
            command.Parameters.AddWithValue("@dotDomain", $"%.{domain}%");

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                var value = reader.GetString(1);
                var host = reader.GetString(2);
                var path = reader.GetString(3);
                var expiry = reader.GetInt64(4);
                var isSecure = reader.GetInt32(5) == 1;
                var isHttpOnly = reader.GetInt32(6) == 1;

                cookies.Add(new Cookie
                {
                    Name = name,
                    Value = value,
                    Domain = host,
                    Path = path,
                    Expires = expiry > 0 ? DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime : null,
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

    private string? GetDefaultProfilePath()
    {
        if (!Directory.Exists(_profilePath))
            return null;

        // Look for profiles.ini to find the default profile
        var profilesIni = Path.Combine(Path.GetDirectoryName(_profilePath)!, "profiles.ini");
        if (File.Exists(profilesIni))
        {
            var lines = File.ReadAllLines(profilesIni);
            string? currentPath = null;
            bool isDefault = false;
            bool isRelative = true;

            foreach (var line in lines)
            {
                if (line.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                {
                    currentPath = line[5..];
                }
                else if (line.StartsWith("IsRelative=", StringComparison.OrdinalIgnoreCase))
                {
                    isRelative = line[11..] == "1";
                }
                else if (line.StartsWith("Default=1", StringComparison.OrdinalIgnoreCase))
                {
                    isDefault = true;
                }
                else if (line.StartsWith("[") && currentPath != null && isDefault)
                {
                    break;
                }
            }

            if (currentPath != null && isDefault)
            {
                return isRelative
                    ? Path.Combine(_profilePath, "..", currentPath)
                    : currentPath;
            }
        }

        // Fallback: find the first profile directory with a .default suffix
        var directories = Directory.GetDirectories(_profilePath);
        return directories.FirstOrDefault(d => d.Contains(".default"));
    }
}
