namespace MuleGame.Api.Models;

/// <summary>
/// Game difficulty levels matching Atari 800 original
/// </summary>
public enum GameDifficulty
{
    Beginner = 0,   // 6 rounds, extra resources, unlimited store MULEs
    Standard = 1,   // 12 rounds, normal resources, limited store
    Tournament = 2  // 12 rounds, crystite enabled, pirates steal crystite
}

/// <summary>
/// The 8 species from the original Atari 800 M.U.L.E.
/// </summary>
public enum Species
{
    Humanoid = 0,   // Expert - $600 start, 35 sec turns
    Flapper = 1,    // Beginner - $1600 start, 60 sec turns
    Packer = 2,     // Standard - $1000 start, 45 sec turns
    Gollumer = 3,   // Standard
    Spheroid = 4,   // Standard
    Bonzoid = 5,    // Standard
    Leggite = 6,    // Standard
    Mechtron = 7    // Standard (computer preferred)
}

/// <summary>
/// Resource types in M.U.L.E.
/// </summary>
public enum ResourceType
{
    Food = 0,
    Energy = 1,
    Smithore = 2,
    Crystite = 3   // Only in Tournament mode
}

/// <summary>
/// Terrain types for map tiles
/// </summary>
public enum TerrainType
{
    River = 0,          // Center column - best for food, cannot mine
    Plains = 1,         // Good for energy
    Mountains1 = 2,     // 1 mountain peak - some smithore
    Mountains2 = 3,     // 2 mountain peaks - better smithore
    Mountains3 = 4,     // 3 mountain peaks - best smithore
    Town = 5            // Center tile - store, assay office, pub, etc.
}

/// <summary>
/// MULE equipment/outfitting types
/// </summary>
public enum MuleType
{
    None = 0,           // Unequipped MULE
    Food = 1,           // $25 to outfit
    Energy = 2,         // $50 to outfit
    Smithore = 3,       // $75 to outfit
    Crystite = 4        // $100 to outfit (Tournament only)
}

/// <summary>
/// Game phases within each round
/// </summary>
public enum GamePhase
{
    Setup = 0,              // Initial game setup
    LandGrant = 1,          // Free land selection
    LandAuction = 2,        // Auction for additional land
    Development = 3,        // Player turns (buying MULEs, installing, etc.)
    Production = 4,         // Calculate resource production
    ResourceAuction = 5,    // Buy/sell resources between players
    Summary = 6,            // Round summary
    GameOver = 7            // Final scores
}

/// <summary>
/// Random event types
/// </summary>
public enum RandomEventType
{
    // Positive events (never happen to leader)
    WampusCaught,           // Catch wampus for money
    BenefactorDonation,     // Free money
    GoodReturnFromInvest,   // Investment pays off
    LostMuleFound,          // Free MULE returned
    MuseumBuysArtifact,     // Found artifact sold
    FriendRepaysLoan,       // Old friend pays

    // Negative events (never happen to last place)
    MuleRunsAway,           // MULE escapes from plot
    PestAttack,             // Destroys food on a plot
    CatbugEatsFood,         // Food in store eaten
    FireInStore,            // All store inventory destroyed
    PirateRaid,             // Steals smithore or crystite

    // Neutral events (affect all or random)
    Sunspot,                // Increases energy production
    AcidRainStorm,          // Increases food, decreases energy in column
    Planetquake,            // Reduces smithore/crystite production
    MeteorStrike,           // Destroys MULE, creates crystite deposit

    // Colony-wide events
    ColonyShipArrival,      // Restocks store (round 12)
    SmithoreShortage,       // No MULEs available
    FoodShortage,           // All players lose food
    EnergyShortage          // All players lose energy
}

/// <summary>
/// Building types in town
/// </summary>
public enum TownBuilding
{
    Store = 0,          // Buy/sell MULEs
    Pub = 1,            // Gamble for money, ends turn
    LandOffice = 2,     // Sell land plots
    AssayOffice = 3,    // Crystite quality info (Tournament)
    Corral = 4          // Where MULEs are kept
}

/// <summary>
/// Auction state
/// </summary>
public enum AuctionState
{
    NotStarted = 0,
    InProgress = 1,
    Sold = 2,
    Unsold = 3,
    Complete = 4
}

/// <summary>
/// Player type
/// </summary>
public enum PlayerType
{
    Human = 0,
    Computer = 1
}
