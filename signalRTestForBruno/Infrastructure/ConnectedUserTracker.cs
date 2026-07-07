using System.Collections.Concurrent;
using signalRTestForBruno.Models;

namespace signalRTestForBruno.Infrastructure;

/// <summary>
/// Thread-safe in-memory tracker for connected SignalR users.
/// Registered as a singleton so all hub instances share the same state.
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> for thread safety.
/// </summary>
public class ConnectedUserTracker
{
    private readonly ConcurrentDictionary<string, ConnectedUserInfo> _connections = new();

    /// <summary>
    /// Adds or updates a connection entry.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="userName">The user's display name (from JWT).</param>
    /// <param name="tokenInfo">Formatted JWT claim info for logging.</param>
    public void Add(string connectionId, string? userName, string? tokenInfo)
    {
        _connections[connectionId] = new ConnectedUserInfo
        {
            ConnectionId = connectionId,
            UserName = userName,
            TokenInfo = tokenInfo
        };
    }

    /// <summary>
    /// Removes a connection entry. Safe to call even if the ID does not exist.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID to remove.</param>
    public void Remove(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Returns a snapshot of all currently connected users.
    /// </summary>
    /// <returns>A list of <see cref="ConnectedUserInfo"/>.</returns>
    public List<ConnectedUserInfo> GetAll()
    {
        return _connections.Values.ToList();
    }

    /// <summary>
    /// Checks whether a given connection ID is currently active.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <returns>True if the connection exists, false otherwise.</returns>
    public bool Exists(string connectionId)
    {
        return _connections.ContainsKey(connectionId);
    }
}
