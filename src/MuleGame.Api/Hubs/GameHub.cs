using Microsoft.AspNetCore.SignalR;
using MuleGame.Api.Models;
using MuleGame.Api.Services;

namespace MuleGame.Api.Hubs;

/// <summary>
/// SignalR hub for real-time game updates
/// </summary>
public class GameHub : Hub
{
    private readonly GameSessionManager _sessionManager;

    public GameHub(GameSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Join a game room for real-time updates
    /// </summary>
    public async Task JoinGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        _sessionManager.MapConnectionToGame(Context.ConnectionId, gameId);
    }

    /// <summary>
    /// Leave a game room
    /// </summary>
    public async Task LeaveGame(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
        _sessionManager.RemoveConnection(Context.ConnectionId);
    }

    /// <summary>
    /// Broadcast game state update to all players in a game
    /// </summary>
    public async Task BroadcastGameState(string gameId, object gameState)
    {
        await Clients.Group(gameId).SendAsync("GameStateUpdated", gameState);
    }

    /// <summary>
    /// Broadcast event message to all players
    /// </summary>
    public async Task BroadcastEvent(string gameId, string message)
    {
        await Clients.Group(gameId).SendAsync("EventMessage", message);
    }

    /// <summary>
    /// Handle player disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var gameId = _sessionManager.GetGameForConnection(Context.ConnectionId);
        if (gameId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
            _sessionManager.RemoveConnection(Context.ConnectionId);

            // Notify other players of disconnection
            await Clients.Group(gameId).SendAsync("PlayerDisconnected", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
