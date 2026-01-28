using Microsoft.AspNetCore.SignalR;

namespace WebSocketService.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;
    private static readonly Dictionary<string, string> _userConnections = new();

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        
        // Remove user mapping
        var userId = _userConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (userId != null)
        {
            _userConnections.Remove(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterUser(string userId)
    {
        _logger.LogInformation("User {UserId} registered with connection {ConnectionId}", userId, Context.ConnectionId);
        _userConnections[userId] = Context.ConnectionId;
        
        await Clients.Caller.SendAsync("UserRegistered", new { userId, connectionId = Context.ConnectionId });
    }

    public static string? GetConnectionId(string userId)
    {
        return _userConnections.TryGetValue(userId, out var connectionId) ? connectionId : null;
    }

    public async Task SendToUser(string userId, string eventName, object data)
    {
        var connectionId = GetConnectionId(userId);
        if (connectionId != null)
        {
            await Clients.Client(connectionId).SendAsync(eventName, data);
        }
    }
}
