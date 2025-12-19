using Microsoft.AspNetCore.Mvc;
using MuleGame.Api.Models;
using MuleGame.Api.Services;

namespace MuleGame.Api.Controllers;

/// <summary>
/// Main game API controller
/// Handles game creation, state queries, and game actions
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly GameSessionManager _sessionManager;

    public GameController(IGameService gameService, GameSessionManager sessionManager)
    {
        _gameService = gameService;
        _sessionManager = sessionManager;
    }

    #region Game Lifecycle

    /// <summary>
    /// Create a new game
    /// </summary>
    [HttpPost("create")]
    public ActionResult<GameStateDto> CreateGame([FromBody] CreateGameRequest request)
    {
        try
        {
            var players = request.Players.Select(p => new PlayerSetup
            {
                Name = p.Name,
                Species = p.Species,
                Type = p.Type,
                ConnectionId = p.ConnectionId ?? ""
            }).ToList();

            var game = _gameService.CreateGame(request.Difficulty, players);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get current game state
    /// </summary>
    [HttpGet("{gameId}")]
    public ActionResult<GameStateDto> GetGame(string gameId)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        return Ok(GameStateDto.FromGameState(game));
    }

    /// <summary>
    /// Get list of active games
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<GameSummary>> GetGames()
    {
        var games = _sessionManager.GetAllGames()
            .Select(g => new GameSummary
            {
                GameId = g.GameId,
                Difficulty = g.Difficulty,
                Phase = g.Phase,
                CurrentRound = g.CurrentRound,
                PlayerCount = g.Players.Count
            });

        return Ok(games);
    }

    /// <summary>
    /// End/delete a game
    /// </summary>
    [HttpDelete("{gameId}")]
    public ActionResult EndGame(string gameId)
    {
        if (_gameService.EndGame(gameId))
            return Ok(new { message = "Game ended" });

        return NotFound(new { error = "Game not found" });
    }

    #endregion

    #region Phase Management

    /// <summary>
    /// Advance to the next phase
    /// </summary>
    [HttpPost("{gameId}/advance")]
    public ActionResult<GameStateDto> AdvancePhase(string gameId)
    {
        try
        {
            var game = _gameService.AdvancePhase(gameId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Land Grant Actions

    /// <summary>
    /// Move the land selection cursor
    /// </summary>
    [HttpPost("{gameId}/land-grant/move")]
    public ActionResult<GameStateDto> MoveLandCursor(string gameId, [FromBody] MoveRequest request)
    {
        try
        {
            var game = _gameService.MoveLandCursor(gameId, request.PlayerId, request.DX, request.DY);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Select current tile for land grant
    /// </summary>
    [HttpPost("{gameId}/land-grant/select")]
    public ActionResult<GameStateDto> SelectLand(string gameId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            var game = _gameService.SelectLand(gameId, request.PlayerId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pass on land grant
    /// </summary>
    [HttpPost("{gameId}/land-grant/pass")]
    public ActionResult<GameStateDto> PassLandGrant(string gameId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            var game = _gameService.PassLandGrant(gameId, request.PlayerId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Land Auction Actions

    /// <summary>
    /// Place a bid in land auction
    /// </summary>
    [HttpPost("{gameId}/land-auction/bid")]
    public ActionResult<GameStateDto> PlaceLandBid(string gameId, [FromBody] BidRequest request)
    {
        try
        {
            var game = _gameService.PlaceLandBid(gameId, request.PlayerId, request.Amount);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Development Phase Actions

    /// <summary>
    /// Move player on the map
    /// </summary>
    [HttpPost("{gameId}/development/move")]
    public ActionResult<GameStateDto> MovePlayer(string gameId, [FromBody] MoveToRequest request)
    {
        try
        {
            var game = _gameService.MovePlayer(gameId, request.PlayerId, request.X, request.Y);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Buy a MULE from the store
    /// </summary>
    [HttpPost("{gameId}/development/buy-mule")]
    public ActionResult<GameStateDto> BuyMule(string gameId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            var game = _gameService.BuyMule(gameId, request.PlayerId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Outfit a MULE with equipment
    /// </summary>
    [HttpPost("{gameId}/development/outfit-mule")]
    public ActionResult<GameStateDto> OutfitMule(string gameId, [FromBody] OutfitMuleRequest request)
    {
        try
        {
            var game = _gameService.OutfitMule(gameId, request.PlayerId, request.MuleType);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Install a MULE on a plot
    /// </summary>
    [HttpPost("{gameId}/development/install-mule")]
    public ActionResult<GameStateDto> InstallMule(string gameId, [FromBody] InstallMuleRequest request)
    {
        try
        {
            var game = _gameService.InstallMule(gameId, request.PlayerId, request.X, request.Y);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Enter the pub (ends turn with gambling reward)
    /// </summary>
    [HttpPost("{gameId}/development/enter-pub")]
    public ActionResult<GameStateDto> EnterPub(string gameId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            var game = _gameService.EnterPub(gameId, request.PlayerId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Try to catch the wampus
    /// </summary>
    [HttpPost("{gameId}/development/catch-wampus")]
    public ActionResult<GameStateDto> CatchWampus(string gameId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            var game = _gameService.CatchWampus(gameId, request.PlayerId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// End the current player's turn
    /// </summary>
    [HttpPost("{gameId}/development/end-turn")]
    public ActionResult<GameStateDto> EndTurn(string gameId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            var game = _gameService.EndTurn(gameId, request.PlayerId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Resource Auction Actions

    /// <summary>
    /// Set auction position (buying or selling)
    /// </summary>
    [HttpPost("{gameId}/auction/position")]
    public ActionResult<GameStateDto> SetAuctionPosition(string gameId, [FromBody] AuctionPositionRequest request)
    {
        try
        {
            var game = _gameService.SetAuctionPosition(gameId, request.PlayerId, request.IsBuying, request.Price);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Trade with the store
    /// </summary>
    [HttpPost("{gameId}/auction/store-trade")]
    public ActionResult<GameStateDto> TradeWithStore(string gameId, [FromBody] StoreTradeRequest request)
    {
        try
        {
            var game = _gameService.TradeWithStore(gameId, request.PlayerId, request.Resource, request.Quantity, request.IsBuying);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Move to next resource in auction
    /// </summary>
    [HttpPost("{gameId}/auction/next")]
    public ActionResult<GameStateDto> NextAuctionResource(string gameId)
    {
        try
        {
            var game = _gameService.NextAuctionResource(gameId);
            return Ok(GameStateDto.FromGameState(game));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion
}

#region Request/Response DTOs

public class CreateGameRequest
{
    public GameDifficulty Difficulty { get; set; }
    public List<PlayerSetupDto> Players { get; set; } = new();
}

public class PlayerSetupDto
{
    public string Name { get; set; } = "";
    public Species Species { get; set; }
    public PlayerType Type { get; set; }
    public string? ConnectionId { get; set; }
}

public class PlayerActionRequest
{
    public int PlayerId { get; set; }
}

public class MoveRequest
{
    public int PlayerId { get; set; }
    public int DX { get; set; }
    public int DY { get; set; }
}

public class MoveToRequest
{
    public int PlayerId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public class BidRequest
{
    public int PlayerId { get; set; }
    public int Amount { get; set; }
}

public class OutfitMuleRequest
{
    public int PlayerId { get; set; }
    public MuleType MuleType { get; set; }
}

public class InstallMuleRequest
{
    public int PlayerId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public class AuctionPositionRequest
{
    public int PlayerId { get; set; }
    public bool IsBuying { get; set; }
    public int Price { get; set; }
}

public class StoreTradeRequest
{
    public int PlayerId { get; set; }
    public ResourceType Resource { get; set; }
    public int Quantity { get; set; }
    public bool IsBuying { get; set; }
}

public class GameSummary
{
    public string GameId { get; set; } = "";
    public GameDifficulty Difficulty { get; set; }
    public GamePhase Phase { get; set; }
    public int CurrentRound { get; set; }
    public int PlayerCount { get; set; }
}

#endregion

#region Game State DTO

/// <summary>
/// Game state DTO for API responses
/// </summary>
public class GameStateDto
{
    public string GameId { get; set; } = "";
    public GameDifficulty Difficulty { get; set; }
    public GamePhase Phase { get; set; }
    public int CurrentRound { get; set; }
    public int MaxRounds { get; set; }
    public int CurrentPlayerIndex { get; set; }

    public MapDto Map { get; set; } = new();
    public StoreDto Store { get; set; } = new();
    public List<PlayerDto> Players { get; set; } = new();

    public LandGrantStateDto? LandGrantState { get; set; }
    public LandAuctionStateDto? LandAuctionState { get; set; }
    public DevelopmentStateDto? DevelopmentState { get; set; }
    public ResourceAuctionStateDto? ResourceAuctionState { get; set; }

    public List<RandomEventDto> PendingEvents { get; set; } = new();
    public WampusDto? Wampus { get; set; }

    public int ColonyScore { get; set; }
    public bool IsColonySuccessful { get; set; }

    public static GameStateDto FromGameState(GameState game)
    {
        return new GameStateDto
        {
            GameId = game.GameId,
            Difficulty = game.Difficulty,
            Phase = game.Phase,
            CurrentRound = game.CurrentRound,
            MaxRounds = game.MaxRounds,
            CurrentPlayerIndex = game.CurrentPlayerIndex,
            Map = MapDto.FromMap(game.Map),
            Store = StoreDto.FromStore(game.Store),
            Players = game.Players.Select(p => PlayerDto.FromPlayer(p, game)).ToList(),
            LandGrantState = game.LandGrantState != null ? LandGrantStateDto.FromState(game.LandGrantState) : null,
            LandAuctionState = game.LandAuctionState != null ? LandAuctionStateDto.FromState(game.LandAuctionState) : null,
            DevelopmentState = game.DevelopmentState != null ? DevelopmentStateDto.FromState(game.DevelopmentState) : null,
            ResourceAuctionState = game.ResourceAuctionState != null ? ResourceAuctionStateDto.FromState(game.ResourceAuctionState) : null,
            PendingEvents = game.PendingEvents.Select(e => new RandomEventDto
            {
                Type = e.Type,
                Message = e.Message,
                AffectedPlayerId = e.AffectedPlayerId
            }).ToList(),
            Wampus = game.Wampus != null ? WampusDto.FromState(game.Wampus) : null,
            ColonyScore = game.GetColonyScore(),
            IsColonySuccessful = game.IsColonySuccessful()
        };
    }
}

public class MapDto
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<TileDto> Tiles { get; set; } = new();

    public static MapDto FromMap(GameMap map)
    {
        var dto = new MapDto
        {
            Width = GameMap.Width,
            Height = GameMap.Height
        };

        for (int y = 0; y < GameMap.Height; y++)
        {
            for (int x = 0; x < GameMap.Width; x++)
            {
                dto.Tiles.Add(TileDto.FromTile(map.GetTile(x, y)));
            }
        }

        return dto;
    }
}

public class TileDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public TerrainType Terrain { get; set; }
    public int? OwnerId { get; set; }
    public MuleType InstalledMule { get; set; }
    public int FoodQuality { get; set; }
    public int EnergyQuality { get; set; }
    public int SmithoreQuality { get; set; }
    public int CrystiteQuality { get; set; }
    public bool CrystiteAssayed { get; set; }

    public static TileDto FromTile(MapTile tile)
    {
        return new TileDto
        {
            X = tile.X,
            Y = tile.Y,
            Terrain = tile.Terrain,
            OwnerId = tile.OwnerId,
            InstalledMule = tile.InstalledMule,
            FoodQuality = tile.FoodQuality,
            EnergyQuality = tile.EnergyQuality,
            SmithoreQuality = tile.SmithoreQuality,
            CrystiteQuality = tile.CrystiteAssayed ? tile.CrystiteQuality : 0,
            CrystiteAssayed = tile.CrystiteAssayed
        };
    }
}

public class StoreDto
{
    public int MuleCount { get; set; }
    public int MulePrice { get; set; }
    public int FoodStock { get; set; }
    public int FoodPrice { get; set; }
    public int EnergyStock { get; set; }
    public int EnergyPrice { get; set; }
    public int SmithoreStock { get; set; }
    public int SmithorePrice { get; set; }
    public int CrystiteStock { get; set; }
    public int CrystitePrice { get; set; }

    public static StoreDto FromStore(Store store)
    {
        return new StoreDto
        {
            MuleCount = store.MuleCount,
            MulePrice = store.MulePrice,
            FoodStock = store.FoodStock,
            FoodPrice = store.FoodPrice,
            EnergyStock = store.EnergyStock,
            EnergyPrice = store.EnergyPrice,
            SmithoreStock = store.SmithoreStock,
            SmithorePrice = store.SmithorePrice,
            CrystiteStock = store.CrystiteStock,
            CrystitePrice = store.CrystitePrice
        };
    }
}

public class PlayerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Species Species { get; set; }
    public PlayerType Type { get; set; }
    public int Money { get; set; }
    public int Food { get; set; }
    public int Energy { get; set; }
    public int Smithore { get; set; }
    public int Crystite { get; set; }
    public int Score { get; set; }
    public int LandCount { get; set; }
    public int TimeRemaining { get; set; }
    public bool HasMule { get; set; }
    public MuleType CurrentMuleType { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    public static PlayerDto FromPlayer(Player player, GameState game)
    {
        return new PlayerDto
        {
            Id = player.Id,
            Name = player.Name,
            Species = player.Species,
            Type = player.Type,
            Money = player.Money,
            Food = player.Food,
            Energy = player.Energy,
            Smithore = player.Smithore,
            Crystite = player.Crystite,
            Score = player.CalculateScore(game),
            LandCount = player.LandCount,
            TimeRemaining = player.TimeRemaining,
            HasMule = player.HasMule,
            CurrentMuleType = player.CurrentMuleType,
            PositionX = player.PositionX,
            PositionY = player.PositionY
        };
    }
}

public class LandGrantStateDto
{
    public int CurrentSelectorIndex { get; set; }
    public int CursorX { get; set; }
    public int CursorY { get; set; }
    public List<int> SelectionOrder { get; set; } = new();

    public static LandGrantStateDto FromState(LandGrantState state)
    {
        return new LandGrantStateDto
        {
            CurrentSelectorIndex = state.CurrentSelectorIndex,
            CursorX = state.CursorX,
            CursorY = state.CursorY,
            SelectionOrder = state.SelectionOrder
        };
    }
}

public class LandAuctionStateDto
{
    public TileDto? CurrentPlot { get; set; }
    public int CurrentBid { get; set; }
    public int? CurrentBidder { get; set; }
    public AuctionState State { get; set; }
    public int MinBid { get; set; }
    public int TimeRemaining { get; set; }

    public static LandAuctionStateDto FromState(LandAuctionState state)
    {
        return new LandAuctionStateDto
        {
            CurrentPlot = state.CurrentPlot != null ? TileDto.FromTile(state.CurrentPlot) : null,
            CurrentBid = state.CurrentBid,
            CurrentBidder = state.CurrentBidder,
            State = state.State,
            MinBid = state.MinBid,
            TimeRemaining = state.AuctionTimeRemaining
        };
    }
}

public class DevelopmentStateDto
{
    public int CurrentPlayerIndex { get; set; }
    public List<int> TurnOrder { get; set; } = new();
    public int TurnsCompleted { get; set; }
    public bool InTown { get; set; }
    public TownBuilding? CurrentBuilding { get; set; }

    public static DevelopmentStateDto FromState(DevelopmentState state)
    {
        return new DevelopmentStateDto
        {
            CurrentPlayerIndex = state.CurrentPlayerIndex,
            TurnOrder = state.TurnOrder,
            TurnsCompleted = state.TurnsCompleted,
            InTown = state.InTown,
            CurrentBuilding = state.CurrentBuilding
        };
    }
}

public class ResourceAuctionStateDto
{
    public ResourceType CurrentResource { get; set; }
    public AuctionState State { get; set; }
    public Dictionary<int, int> BuyerPositions { get; set; } = new();
    public Dictionary<int, int> SellerPositions { get; set; } = new();
    public int TimeRemaining { get; set; }

    public static ResourceAuctionStateDto FromState(ResourceAuctionState state)
    {
        return new ResourceAuctionStateDto
        {
            CurrentResource = state.CurrentResource,
            State = state.State,
            BuyerPositions = state.BuyerPositions,
            SellerPositions = state.SellerPositions,
            TimeRemaining = state.TimeRemaining
        };
    }
}

public class RandomEventDto
{
    public RandomEventType Type { get; set; }
    public string Message { get; set; } = "";
    public int? AffectedPlayerId { get; set; }
}

public class WampusDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsVisible { get; set; }
    public bool IsCaught { get; set; }

    public static WampusDto FromState(WampusState state)
    {
        var now = DateTime.UtcNow;
        return new WampusDto
        {
            X = state.X,
            Y = state.Y,
            IsVisible = now >= state.AppearTime && now <= state.DisappearTime && !state.IsCaught,
            IsCaught = state.IsCaught
        };
    }
}

#endregion
