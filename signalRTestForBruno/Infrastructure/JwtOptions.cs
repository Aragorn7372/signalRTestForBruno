namespace signalRTestForBruno.Infrastructure;

/// <summary>
/// Configuration options for JWT token generation and validation.
/// Bound from the "Jwt" section of <c>appsettings.json</c>.
/// </summary>
public class JwtOptions
{
    /// <summary>The configuration section name in appsettings.json.</summary>
    public const string SectionName = "Jwt";

    /// <summary>The symmetric key used to sign JWT tokens. Must be at least 32 characters.</summary>
    public string SecretKey { get; init; } = string.Empty;
    /// <summary>The issuer claim for JWT tokens.</summary>
    public string Issuer { get; init; } = string.Empty;
    /// <summary>The audience claim for JWT tokens.</summary>
    public string Audience { get; init; } = string.Empty;
    /// <summary>Token expiration time in minutes. Defaults to 60.</summary>
    public int ExpirationMinutes { get; init; } = 60;
}
