namespace MuleGame.Api.Models;

/// <summary>
/// Complete game state - the central model for M.U.L.E.
/// All game logic operates on this state
/// </summary>
public class GameState
{
    public string GameId { get; set; } = Guid.NewGuid().ToString();
    public GameDifficulty Difficulty { get; set; }
    public GamePhase Phase { get; set; } = GamePhase.Setup;

    // Game progress
    public int CurrentRound { get; set; } = 1;
    public int MaxRounds => Difficulty == GameDifficulty.Beginner ? 6 : 12;
    public int CurrentPlayerIndex { get; set; } = 0;

    // Core game objects
    public GameMap Map { get; set; } = new();
    public Store Store { get; set; } = new();
    public List<Player> Players { get; set; } = new();

    // Phase-specific state
    public LandGrantState? LandGrantState { get; set; }
    public LandAuctionState? LandAuctionState { get; set; }
    public DevelopmentState? DevelopmentState { get; set; }
    public ProductionState? ProductionState { get; set; }
    public ResourceAuctionState? ResourceAuctionState { get; set; }

    // Random events
    public List<RandomEventResult> PendingEvents { get; set; } = new();
    public List<RandomEventResult> EventHistory { get; set; } = new();

    // Timing
    public DateTime? PhaseStartTime { get; set; }
    public int PhaseTimeRemaining { get; set; }

    // Wampus state (appears during development phase)
    public WampusState? Wampus { get; set; }

    public Random Rng { get; set; } = new();

    /// <summary>
    /// Get the current player
    /// </summary>
    public Player? CurrentPlayer =>
        CurrentPlayerIndex >= 0 && CurrentPlayerIndex < Players.Count
            ? Players[CurrentPlayerIndex]
            : null;

    /// <summary>
    /// Get players sorted by score (for turn order - leader first)
    /// </summary>
    public List<Player> GetPlayersByScore()
    {
        return Players.OrderByDescending(p => p.CalculateScore(this)).ToList();
    }

    /// <summary>
    /// Get players sorted by score ascending (for random event targeting)
    /// Leader = first, Last = last
    /// </summary>
    public (Player Leader, Player Last) GetLeaderAndLast()
    {
        var sorted = GetPlayersByScore();
        return (sorted.First(), sorted.Last());
    }

    /// <summary>
    /// Calculate total colony score
    /// </summary>
    public int GetColonyScore()
    {
        return Players.Sum(p => p.CalculateScore(this));
    }

    /// <summary>
    /// Check if colony meets minimum score requirement
    /// </summary>
    public bool IsColonySuccessful()
    {
        int minScore = Difficulty switch
        {
            GameDifficulty.Beginner => 30000,
            GameDifficulty.Standard => 60000,
            GameDifficulty.Tournament => 100000,
            _ => 60000
        };
        return GetColonyScore() >= minScore;
    }

    /// <summary>
    /// Get the winner
    /// </summary>
    public Player? GetWinner()
    {
        if (Phase != GamePhase.GameOver)
            return null;

        return Players.OrderByDescending(p => p.CalculateScore(this)).FirstOrDefault();
    }

    /// <summary>
    /// Calculate total resources across all players (for price calculations)
    /// </summary>
    public (int Food, int Energy, int Smithore, int Crystite) GetTotalResources()
    {
        return (
            Players.Sum(p => p.Food),
            Players.Sum(p => p.Energy),
            Players.Sum(p => p.Smithore),
            Players.Sum(p => p.Crystite)
        );
    }

    /// <summary>
    /// Apply spoilage to resources at end of round (Atari 800 accurate)
    /// </summary>
    public void ApplySpoilage()
    {
        foreach (var player in Players)
        {
            // Food spoils at 50% rate
            if (player.Food > 8)
            {
                int spoiled = (player.Food - 8) / 2;
                player.Food -= spoiled;
            }

            // Energy spoils at 25% rate
            if (player.Energy > 8)
            {
                int spoiled = (player.Energy - 8) / 4;
                player.Energy -= spoiled;
            }

            // Smithore/Crystite spoil above 50 units
            if (player.Smithore > 50)
            {
                player.Smithore = 50;
            }
            if (player.Crystite > 50)
            {
                player.Crystite = 50;
            }
        }
    }

    /// <summary>
    /// Clone game state for history/undo
    /// </summary>
    public GameState Clone()
    {
        return new GameState
        {
            GameId = GameId,
            Difficulty = Difficulty,
            Phase = Phase,
            CurrentRound = CurrentRound,
            CurrentPlayerIndex = CurrentPlayerIndex,
            Map = Map.Clone(),
            Store = Store.Clone(),
            Players = Players.Select(p => p.Clone()).ToList(),
            EventHistory = new List<RandomEventResult>(EventHistory)
        };
    }
}

/// <summary>
/// State for land grant phase
/// </summary>
public class LandGrantState
{
    public int CurrentSelectorIndex { get; set; }
    public int CursorX { get; set; } = GameMap.TownX;
    public int CursorY { get; set; } = GameMap.TownY;
    public bool SelectionMade { get; set; }
    public List<int> SelectionOrder { get; set; } = new(); // Player indices in selection order
    public DateTime CursorMoveTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// State for land auction phase
/// </summary>
public class LandAuctionState
{
    public MapTile? CurrentPlot { get; set; }
    public List<MapTile> PlotsToAuction { get; set; } = new();
    public int CurrentBid { get; set; }
    public int? CurrentBidder { get; set; }
    public AuctionState State { get; set; } = AuctionState.NotStarted;
    public Dictionary<int, int> PlayerBids { get; set; } = new();
    public int MinBid { get; set; } = 100;
    public int AuctionTimeRemaining { get; set; }
}

/// <summary>
/// State for development phase (player turns)
/// </summary>
public class DevelopmentState
{
    public int CurrentPlayerIndex { get; set; }
    public List<int> TurnOrder { get; set; } = new();
    public int TurnsCompleted { get; set; }
    public DateTime TurnStartTime { get; set; }
    public bool InTown { get; set; } = true;
    public TownBuilding? CurrentBuilding { get; set; }
}

/// <summary>
/// State for production calculation phase
/// </summary>
public class ProductionState
{
    public List<ProductionResult> Results { get; set; } = new();
    public bool IsComplete { get; set; }
}

/// <summary>
/// Production result for a single plot
/// </summary>
public class ProductionResult
{
    public int PlayerId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ResourceType Resource { get; set; }
    public int BaseProduction { get; set; }
    public int AdjacencyBonus { get; set; }
    public int GroupBonus { get; set; }
    public int EventModifier { get; set; }
    public int TotalProduction { get; set; }
    public bool EnergyShortage { get; set; }
}

/// <summary>
/// State for resource auction phase
/// </summary>
public class ResourceAuctionState
{
    public ResourceType CurrentResource { get; set; }
    public AuctionState State { get; set; } = AuctionState.NotStarted;

    // Buyers move up from bottom, sellers move down from top
    public Dictionary<int, int> BuyerPositions { get; set; } = new(); // Player ID -> Price willing to pay
    public Dictionary<int, int> SellerPositions { get; set; } = new(); // Player ID -> Price asking

    // Trade execution
    public List<Trade> ExecutedTrades { get; set; } = new();

    public int TimeRemaining { get; set; }
}

/// <summary>
/// Represents a trade between players
/// </summary>
public class Trade
{
    public int SellerId { get; set; }
    public int BuyerId { get; set; }
    public ResourceType Resource { get; set; }
    public int Quantity { get; set; }
    public int PricePerUnit { get; set; }
}

/// <summary>
/// Wampus hunting state
/// </summary>
public class WampusState
{
    public int X { get; set; }
    public int Y { get; set; }
    public DateTime AppearTime { get; set; }
    public DateTime DisappearTime { get; set; }
    public bool IsCaught { get; set; }
    public int? CaughtByPlayerId { get; set; }
}

/// <summary>
/// Result of a random event
/// </summary>
public class RandomEventResult
{
    public RandomEventType Type { get; set; }
    public int? AffectedPlayerId { get; set; }
    public int? AffectedX { get; set; }
    public int? AffectedY { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Round { get; set; }

    // Effect values
    public int MoneyChange { get; set; }
    public int FoodChange { get; set; }
    public int EnergyChange { get; set; }
    public int SmithoreChange { get; set; }
    public int CrystiteChange { get; set; }
    public bool MuleLost { get; set; }
    public bool MuleGained { get; set; }
}
