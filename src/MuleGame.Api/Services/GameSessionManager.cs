using System.Collections.Concurrent;
using MuleGame.Api.Models;

namespace MuleGame.Api.Services;

/// <summary>
/// Manages active game sessions
/// Thread-safe singleton that stores all active games
/// </summary>
public class GameSessionManager
{
    private readonly ConcurrentDictionary<string, GameState> _games = new();
    private readonly ConcurrentDictionary<string, string> _connectionToGame = new();

    /// <summary>
    /// Add a new game session
    /// </summary>
    public void AddGame(GameState game)
    {
        _games[game.GameId] = game;
    }

    /// <summary>
    /// Get a game by ID
    /// </summary>
    public GameState? GetGame(string gameId)
    {
        _games.TryGetValue(gameId, out var game);
        return game;
    }

    /// <summary>
    /// Update a game
    /// </summary>
    public void UpdateGame(GameState game)
    {
        _games[game.GameId] = game;
    }

    /// <summary>
    /// Remove a game session
    /// </summary>
    public bool RemoveGame(string gameId)
    {
        return _games.TryRemove(gameId, out _);
    }

    /// <summary>
    /// Associate a connection with a game
    /// </summary>
    public void MapConnectionToGame(string connectionId, string gameId)
    {
        _connectionToGame[connectionId] = gameId;
    }

    /// <summary>
    /// Get game ID for a connection
    /// </summary>
    public string? GetGameForConnection(string connectionId)
    {
        _connectionToGame.TryGetValue(connectionId, out var gameId);
        return gameId;
    }

    /// <summary>
    /// Remove connection mapping
    /// </summary>
    public void RemoveConnection(string connectionId)
    {
        _connectionToGame.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Get all active games
    /// </summary>
    public IEnumerable<GameState> GetAllGames()
    {
        return _games.Values;
    }

    /// <summary>
    /// Get count of active games
    /// </summary>
    public int GameCount => _games.Count;
}
