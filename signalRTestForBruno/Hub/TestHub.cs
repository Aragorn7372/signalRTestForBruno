using Microsoft.AspNetCore.SignalR;

namespace signalRTestForBruno.Hub;

public class TestHub(ILogger<TestHub> logger) : Microsoft.AspNetCore.SignalR.Hub
{
    public async Task Ping(string message)
    {
        logger.LogInformation(message);
        await Clients.All.SendAsync("ReceiveMessage", message);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation($"Connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        logger.LogInformation($"Disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(ex);
    }
}