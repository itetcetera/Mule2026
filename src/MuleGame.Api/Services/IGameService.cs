using MuleGame.Api.Models;

namespace MuleGame.Api.Services;

/// <summary>
/// Main game service interface - handles game flow and state transitions
/// </summary>
public interface IGameService
{
    // Game lifecycle
    GameState CreateGame(GameDifficulty difficulty, List<PlayerSetup> players);
    GameState? GetGame(string gameId);
    bool EndGame(string gameId);

    // Phase transitions
    GameState AdvancePhase(string gameId);

    // Land Grant actions
    GameState MoveLandCursor(string gameId, int playerId, int dx, int dy);
    GameState SelectLand(string gameId, int playerId);
    GameState PassLandGrant(string gameId, int playerId);

    // Land Auction actions
    GameState PlaceLandBid(string gameId, int playerId, int amount);
    GameState PassLandAuction(string gameId, int playerId);

    // Development phase actions
    GameState MovePlayer(string gameId, int playerId, int x, int y);
    GameState BuyMule(string gameId, int playerId);
    GameState OutfitMule(string gameId, int playerId, MuleType type);
    GameState InstallMule(string gameId, int playerId, int x, int y);
    GameState EnterBuilding(string gameId, int playerId, TownBuilding building);
    GameState ExitBuilding(string gameId, int playerId);
    GameState EnterPub(string gameId, int playerId);
    GameState CatchWampus(string gameId, int playerId);
    GameState EndTurn(string gameId, int playerId);

    // Resource Auction actions
    GameState SetAuctionPosition(string gameId, int playerId, bool isBuying, int price);
    GameState AcceptTrade(string gameId, int buyerId, int sellerId);
    GameState TradeWithStore(string gameId, int playerId, ResourceType resource, int quantity, bool isBuying);
    GameState NextAuctionResource(string gameId);
}

/// <summary>
/// Player setup info for game creation
/// </summary>
public class PlayerSetup
{
    public string Name { get; set; } = string.Empty;
    public Species Species { get; set; }
    public PlayerType Type { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}
