namespace signalRTestForBruno.Models;

/// <summary>
/// Represents the authenticated user's identity information extracted from JWT claims.
/// </summary>
public class UserInfo
{
    /// <summary>The user's display name (from the "name" claim).</summary>
    public string? UserName { get; init; }
    /// <summary>The user's role (from the "role" claim).</summary>
    public string? Role { get; init; }
    /// <summary>The user's unique identifier (from the "nameidentifier" claim).</summary>
    public string? Sub { get; init; }
}
