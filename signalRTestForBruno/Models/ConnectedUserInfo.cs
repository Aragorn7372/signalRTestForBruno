namespace signalRTestForBruno.Models;

/// <summary>
/// Represents a currently connected user tracked by <see cref="Infrastructure.ConnectedUserTracker"/>.
/// </summary>
public class ConnectedUserInfo
{
    /// <summary>The SignalR connection ID (unique per connection).</summary>
    public required string ConnectionId { get; init; }
    /// <summary>The user's display name, if authenticated.</summary>
    public string? UserName { get; init; }
    /// <summary>Formatted JWT claim info for display/logging purposes.</summary>
    public string? TokenInfo { get; init; }
}
