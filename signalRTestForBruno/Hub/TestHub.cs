using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using signalRTestForBruno.Infrastructure;
using signalRTestForBruno.Models;

namespace signalRTestForBruno.Hub;

/// <summary>
/// SignalR Hub that demonstrates core real-time communication patterns:
/// broadcast, groups, private messaging, connection tracking, and JWT authentication.
/// Every method in this hub shows a different SignalR concept.
/// <para>
/// Hub methods (called by clients) are defined here. Server events (sent to clients)
/// are named as strings like "ReceiveMessage", "ReceiveGroupJoined", etc.
/// The client must listen for these event names.
/// </para>
/// </summary>
public class TestHub(
    ConnectedUserTracker userTracker,
    ILogger<TestHub> logger) : Microsoft.AspNetCore.SignalR.Hub
{
    /// <summary>
    /// Sends a message to ALL connected clients.
    /// Demonstrates: <c>Clients.All.SendAsync</c>
    /// Server event: "ReceiveMessage" → (message, connectionId, userName)
    /// </summary>
    /// <param name="message">The message text to broadcast.</param>
    public async Task Broadcast(string message)
    {
        var info = GetUserInfo();
        logger.LogInformation("Broadcast from {ConnectionId}: {Message}", Context.ConnectionId, message);
        await Clients.All.SendAsync("ReceiveMessage", message, Context.ConnectionId, info?.UserName);
    }

    /// <summary>
    /// Adds the calling client to a named group.
    /// Demonstrates: <c>Groups.AddToGroupAsync</c> and <c>Clients.Caller</c>
    /// Server event: "ReceiveGroupJoined" → (groupName)
    /// Groups are named collections of connections — ideal for chat rooms, channels, etc.
    /// </summary>
    /// <param name="groupName">The name of the group to join.</param>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        logger.LogInformation("{ConnectionId} joined group {Group}", Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("ReceiveGroupJoined", groupName);
    }

    /// <summary>
    /// Removes the calling client from a named group.
    /// Demonstrates: <c>Groups.RemoveFromGroupAsync</c>
    /// Server event: "ReceiveGroupLeft" → (groupName)
    /// </summary>
    /// <param name="groupName">The name of the group to leave.</param>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        logger.LogInformation("{ConnectionId} left group {Group}", Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("ReceiveGroupLeft", groupName);
    }

    /// <summary>
    /// Sends a message to all members of a named group.
    /// Demonstrates: <c>Clients.Group(groupName).SendAsync</c>
    /// Server event: "ReceiveGroupMessage" → (groupName, message, connectionId, userName)
    /// </summary>
    /// <param name="groupName">The target group name.</param>
    /// <param name="message">The message text.</param>
    public async Task GroupChat(string groupName, string message)
    {
        var info = GetUserInfo();
        logger.LogInformation("GroupChat {Group} from {ConnectionId}: {Message}",
            groupName, Context.ConnectionId, message);
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage",
            groupName, message, Context.ConnectionId, info?.UserName);
    }

    /// <summary>
    /// Sends a private message to a specific client by connection ID.
    /// Demonstrates: <c>Clients.Client(targetConnectionId).SendAsync</c>
    /// Server events:
    ///   "ReceivePrivateMessage" → (message, fromConnectionId, userName) — sent to target
    ///   "ReceivePrivateMessageSent" → (message, toConnectionId, userName) — sent to caller
    ///   "ReceiveError" → (errorMessage) — if the target connection does not exist
    /// </summary>
    /// <param name="targetConnectionId">The target client's connection ID.</param>
    /// <param name="message">The message text.</param>
    public async Task IndividualChat(string targetConnectionId, string message)
    {
        var info = GetUserInfo();
        logger.LogInformation("IndividualChat from {ConnectionId} to {Target}: {Message}",
            Context.ConnectionId, targetConnectionId, message);

        if (userTracker.Exists(targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceivePrivateMessage",
                message, Context.ConnectionId, info?.UserName);
            await Clients.Caller.SendAsync("ReceivePrivateMessageSent",
                message, targetConnectionId, info?.UserName);
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveError",
                $"User {targetConnectionId} not found");
        }
    }
/*
    public Task<List<ConnectedUserInfo>> GetConnectedUsers()
    {
        return Task.FromResult(userTracker.GetAll());
    }*/
    /// <summary>
    /// Returns the list of all currently connected users to the caller.
    /// Demonstrates: <c>Clients.Caller.SendAsync</c>
    /// Server event: "ReceiveConnectedUsers" → (List&lt;ConnectedUserInfo&gt;)
    /// </summary>
    public async Task GetConnectedUsers()
    {
        var users = userTracker.GetAll();
        await Clients.Caller.SendAsync("ReceiveConnectedUsers", users);
    }

    /// <summary>
    /// Called automatically when a new client connects to the hub.
    /// Extracts the user's JWT claims, registers them in the connection tracker,
    /// and sends the connection ID back to the client.
    /// Demonstrates: <c>OnConnectedAsync</c> lifecycle hook
    /// Server event: "OnConnected" → (connectionId, tokenInfo)
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var tokenInfo = ExtractTokenInfo();
        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value;

        userTracker.Add(Context.ConnectionId, userName, tokenInfo);

        logger.LogInformation("Connected: {ConnectionId}, User: {User}, Token: {Token}",
            Context.ConnectionId, userName, tokenInfo ?? "none");

        await Clients.Caller.SendAsync("OnConnected", Context.ConnectionId, tokenInfo);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called automatically when a client disconnects from the hub.
    /// Removes the user from the connection tracker and logs the disconnection.
    /// Demonstrates: <c>OnDisconnectedAsync</c> lifecycle hook
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        userTracker.Remove(Context.ConnectionId);
        logger.LogInformation("Disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Helper method that extracts the authenticated user's claims
    /// (Name, Role, NameIdentifier) from <c>Context.User</c>.
    /// Returns null if the user is not authenticated.
    /// </summary>
    /// <returns>A <see cref="UserInfo"/> object or null.</returns>
    private UserInfo? GetUserInfo()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
            return null;

        return new UserInfo
        {
            UserName = Context.User.FindFirst(ClaimTypes.Name)?.Value,
            Role = Context.User.FindFirst(ClaimTypes.Role)?.Value,
            Sub = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };
    }

    /// <summary>
    /// Helper method that formats the authenticated user's claims
    /// into a readable string for logging purposes.
    /// Returns null if the user is not authenticated.
    /// </summary>
    /// <returns>A formatted string like "name=..., role=..., favoriteColor=..." or null.</returns>
    private string? ExtractTokenInfo()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
            return null;

        var name = Context.User.FindFirst(ClaimTypes.Name)?.Value ?? "?";
        var role = Context.User.FindFirst(ClaimTypes.Role)?.Value ?? "?";
        var color = Context.User.FindFirst("favoriteColor")?.Value ?? "?";
        return $"name={name}, role={role}, favoriteColor={color}";
    }
}
