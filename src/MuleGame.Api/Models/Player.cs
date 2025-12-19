namespace MuleGame.Api.Models;

/// <summary>
/// Represents a player in the M.U.L.E. game
/// </summary>
public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Species Species { get; set; }
    public PlayerType Type { get; set; }
    public string ConnectionId { get; set; } = string.Empty;

    // Resources
    public int Money { get; set; }
    public int Food { get; set; }
    public int Energy { get; set; }
    public int Smithore { get; set; }
    public int Crystite { get; set; }

    // Current turn state
    public int TimeRemaining { get; set; }  // In game ticks (60ths of a second originally)
    public bool HasMule { get; set; }
    public MuleType CurrentMuleType { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // Score tracking
    public int LandCount => OwnedPlots.Count;
    public List<(int X, int Y)> OwnedPlots { get; set; } = new();

    /// <summary>
    /// Calculate total score/wealth based on Atari 800 formula
    /// </summary>
    public int CalculateScore(GameState gameState)
    {
        var landValue = CalculateLandValue(gameState);
        var resourceValue = CalculateResourceValue(gameState);
        return Money + landValue + resourceValue;
    }

    /// <summary>
    /// Land value = $500 per plot + value of installed MULEs
    /// </summary>
    private int CalculateLandValue(GameState gameState)
    {
        int value = 0;
        foreach (var (x, y) in OwnedPlots)
        {
            var plot = gameState.Map.GetTile(x, y);
            value += 500; // Base land value
            if (plot.InstalledMule != MuleType.None)
            {
                value += GetMuleOutfitCost(plot.InstalledMule);
            }
        }
        return value;
    }

    /// <summary>
    /// Resource value based on current store prices
    /// </summary>
    private int CalculateResourceValue(GameState gameState)
    {
        var store = gameState.Store;
        return Food * store.FoodPrice +
               Energy * store.EnergyPrice +
               Smithore * store.SmithorePrice +
               Crystite * store.CrystitePrice;
    }

    /// <summary>
    /// Get MULE outfitting cost
    /// </summary>
    public static int GetMuleOutfitCost(MuleType type) => type switch
    {
        MuleType.Food => 25,
        MuleType.Energy => 50,
        MuleType.Smithore => 75,
        MuleType.Crystite => 100,
        _ => 0
    };

    /// <summary>
    /// Get starting money based on species and player type (Atari 800 accurate - from disassembly)
    /// Human: $258 = 600, Flapper: $640 = 1600, Others: $3E8 = 1000, CPU: $4B0 = 1200
    /// </summary>
    public static int GetStartingMoney(Species species, PlayerType playerType = PlayerType.Human)
    {
        // CPU players get $1200 regardless of species
        if (playerType == PlayerType.Computer)
            return 1200;

        return species switch
        {
            Species.Humanoid => 600,    // Expert species - less money
            Species.Flapper => 1600,    // Beginner species - more money
            _ => 1000                    // All others - standard money
        };
    }

    /// <summary>
    /// Get turn time in seconds based on species (Atari 800 accurate)
    /// </summary>
    public static int GetBaseTurnTime(Species species) => species switch
    {
        Species.Humanoid => 35,     // Expert gets less time
        Species.Flapper => 60,      // Beginner gets more time
        _ => 45                      // Standard time
    };

    /// <summary>
    /// Calculate turn time based on food (Atari 800 accurate)
    /// Full food = full time, no food = 5 seconds
    /// </summary>
    public int CalculateTurnTime()
    {
        int baseTime = GetBaseTurnTime(Species);

        if (Food >= 1)
            return baseTime;

        // Minimum 5 seconds if no food
        return 5;
    }

    /// <summary>
    /// Apply food consumption at start of turn
    /// </summary>
    public void ConsumeFood()
    {
        if (Food > 0)
            Food--;
    }

    /// <summary>
    /// Clone player for state snapshots
    /// </summary>
    public Player Clone()
    {
        return new Player
        {
            Id = Id,
            Name = Name,
            Species = Species,
            Type = Type,
            ConnectionId = ConnectionId,
            Money = Money,
            Food = Food,
            Energy = Energy,
            Smithore = Smithore,
            Crystite = Crystite,
            TimeRemaining = TimeRemaining,
            HasMule = HasMule,
            CurrentMuleType = CurrentMuleType,
            PositionX = PositionX,
            PositionY = PositionY,
            OwnedPlots = new List<(int X, int Y)>(OwnedPlots)
        };
    }
}
