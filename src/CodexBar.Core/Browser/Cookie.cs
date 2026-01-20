namespace CodexBar.Core.Browser;

/// <summary>
/// Represents a browser cookie
/// </summary>
public record Cookie
{
    /// <summary>
    /// The cookie name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The cookie value (decrypted)
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// The domain the cookie belongs to
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// The path the cookie applies to
    /// </summary>
    public string Path { get; init; } = "/";

    /// <summary>
    /// When the cookie expires
    /// </summary>
    public DateTime? Expires { get; init; }

    /// <summary>
    /// Whether this is a secure cookie
    /// </summary>
    public bool Secure { get; init; }

    /// <summary>
    /// Whether this is an HTTP-only cookie
    /// </summary>
    public bool HttpOnly { get; init; }

    /// <summary>
    /// Whether the cookie is expired
    /// </summary>
    public bool IsExpired => Expires.HasValue && Expires.Value < DateTime.UtcNow;

    /// <summary>
    /// Formats the cookie as a header value
    /// </summary>
    public override string ToString() => $"{Name}={Value}";
}
