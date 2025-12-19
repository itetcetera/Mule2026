using MuleGame.Api.Models;

namespace MuleGame.Api.Services;

/// <summary>
/// Random event service interface
/// </summary>
public interface IRandomEventService
{
    RandomEventResult? GenerateRoundEvent(GameState game);
    RandomEventResult? GeneratePlayerEvent(GameState game, int playerId);
}

/// <summary>
/// Handles random events (Atari 800 accurate)
///
/// Event rules:
/// - Good events never happen to the leader
/// - Bad events never happen to last place player
/// - Some events affect the whole colony
/// - Events have different probabilities based on difficulty
/// </summary>
public class RandomEventService : IRandomEventService
{
    // Event messages matching Atari 800 original
    private static readonly Dictionary<RandomEventType, string> EventMessages = new()
    {
        { RandomEventType.WampusCaught, "You caught a Wampus! It paid {0} to be set free." },
        { RandomEventType.BenefactorDonation, "A philanthropist awarded you {0} for your hard work." },
        { RandomEventType.GoodReturnFromInvest, "Your investment paid off! You received {0}." },
        { RandomEventType.LostMuleFound, "A lost M.U.L.E. wandered back to your corral." },
        { RandomEventType.MuseumBuysArtifact, "The museum bought an artifact you found for {0}." },
        { RandomEventType.FriendRepaysLoan, "An old friend repaid a loan of {0}." },

        { RandomEventType.MuleRunsAway, "Your M.U.L.E. in the {0} went crazy and ran away!" },
        { RandomEventType.PestAttack, "Space pests ate all the food on your {0} plot!" },
        { RandomEventType.CatbugEatsFood, "A catbug ate {0} units of your food!" },
        { RandomEventType.FireInStore, "Fire in the store! All stock has been destroyed!" },
        { RandomEventType.PirateRaid, "Space pirates raided the colony and stole all {0}!" },

        { RandomEventType.Sunspot, "Sunspot activity! Energy production increased!" },
        { RandomEventType.AcidRainStorm, "Acid rain storm in column {0}! Food up, energy down." },
        { RandomEventType.Planetquake, "Planetquake! Mining production reduced this round." },
        { RandomEventType.MeteorStrike, "Meteor strike at ({0},{1})! M.U.L.E. destroyed, crystite deposit created." },

        { RandomEventType.ColonyShipArrival, "The colony ship has arrived! The store is restocked." },
        { RandomEventType.SmithoreShortage, "Smithore shortage! No M.U.L.E.s available this round." },
        { RandomEventType.FoodShortage, "Food shortage! All players lose food." },
        { RandomEventType.EnergyShortage, "Energy crisis! All players lose energy." }
    };

    /// <summary>
    /// Generate a random event at the start of a round
    /// </summary>
    public RandomEventResult? GenerateRoundEvent(GameState game)
    {
        // Event probability based on round and difficulty
        int eventChance = game.Difficulty switch
        {
            GameDifficulty.Beginner => 20,
            GameDifficulty.Standard => 35,
            GameDifficulty.Tournament => 50,
            _ => 35
        };

        if (game.Rng.Next(100) >= eventChance)
            return null;

        // Determine event type
        var eventType = SelectEventType(game);
        return CreateEvent(game, eventType);
    }

    /// <summary>
    /// Generate a player-specific event during development phase
    /// </summary>
    public RandomEventResult? GeneratePlayerEvent(GameState game, int playerId)
    {
        // Lower probability for player events
        if (game.Rng.Next(100) >= 15)
            return null;

        var player = game.Players.First(p => p.Id == playerId);
        var (leader, last) = game.GetLeaderAndLast();

        bool isLeader = player.Id == leader.Id;
        bool isLast = player.Id == last.Id;

        // Select appropriate event type
        RandomEventType eventType;

        if (isLeader)
        {
            // Leaders only get bad events
            eventType = SelectBadEvent(game);
        }
        else if (isLast)
        {
            // Last place only gets good events
            eventType = SelectGoodEvent(game);
        }
        else
        {
            // Middle players can get either
            eventType = game.Rng.Next(2) == 0 ? SelectGoodEvent(game) : SelectBadEvent(game);
        }

        return CreatePlayerEvent(game, playerId, eventType);
    }

    private RandomEventType SelectEventType(GameState game)
    {
        // Colony-wide events
        var events = new List<(RandomEventType Type, int Weight)>
        {
            (RandomEventType.Sunspot, 20),
            (RandomEventType.AcidRainStorm, 15),
            (RandomEventType.Planetquake, 10),
            (RandomEventType.FireInStore, 5),
        };

        if (game.Difficulty == GameDifficulty.Tournament)
        {
            events.Add((RandomEventType.PirateRaid, 10));
            events.Add((RandomEventType.MeteorStrike, 5));
        }

        return SelectWeightedEvent(game.Rng, events);
    }

    private RandomEventType SelectGoodEvent(GameState game)
    {
        var events = new List<(RandomEventType Type, int Weight)>
        {
            (RandomEventType.BenefactorDonation, 25),
            (RandomEventType.GoodReturnFromInvest, 20),
            (RandomEventType.MuseumBuysArtifact, 20),
            (RandomEventType.FriendRepaysLoan, 20),
            (RandomEventType.LostMuleFound, 15),
        };

        return SelectWeightedEvent(game.Rng, events);
    }

    private RandomEventType SelectBadEvent(GameState game)
    {
        var events = new List<(RandomEventType Type, int Weight)>
        {
            (RandomEventType.MuleRunsAway, 30),
            (RandomEventType.PestAttack, 25),
            (RandomEventType.CatbugEatsFood, 25),
        };

        if (game.Difficulty == GameDifficulty.Tournament)
        {
            events.Add((RandomEventType.PirateRaid, 20));
        }

        return SelectWeightedEvent(game.Rng, events);
    }

    private RandomEventType SelectWeightedEvent(Random rng, List<(RandomEventType Type, int Weight)> events)
    {
        int totalWeight = events.Sum(e => e.Weight);
        int roll = rng.Next(totalWeight);

        int cumulative = 0;
        foreach (var (type, weight) in events)
        {
            cumulative += weight;
            if (roll < cumulative)
                return type;
        }

        return events[0].Type;
    }

    private RandomEventResult CreateEvent(GameState game, RandomEventType type)
    {
        var result = new RandomEventResult
        {
            Type = type,
            Round = game.CurrentRound
        };

        switch (type)
        {
            case RandomEventType.Sunspot:
                result.Message = EventMessages[type];
                break;

            case RandomEventType.AcidRainStorm:
                int column = game.Rng.Next(GameMap.Width);
                result.AffectedX = column;
                result.Message = string.Format(EventMessages[type], column);
                break;

            case RandomEventType.Planetquake:
                result.Message = EventMessages[type];
                break;

            case RandomEventType.FireInStore:
                result.Message = EventMessages[type];
                // Clear store inventory
                game.Store.FoodStock = 0;
                game.Store.EnergyStock = 0;
                game.Store.SmithoreStock = 0;
                game.Store.CrystiteStock = 0;
                break;

            case RandomEventType.PirateRaid:
                string resource = game.Difficulty == GameDifficulty.Tournament ? "crystite" : "smithore";
                result.Message = string.Format(EventMessages[type], resource);
                // Clear all player crystite/smithore and store
                foreach (var player in game.Players)
                {
                    if (game.Difficulty == GameDifficulty.Tournament)
                        player.Crystite = 0;
                    else
                        player.Smithore = 0;
                }
                if (game.Difficulty == GameDifficulty.Tournament)
                    game.Store.CrystiteStock = 0;
                else
                    game.Store.SmithoreStock = 0;
                break;

            case RandomEventType.MeteorStrike:
                // Find a random tile with a MULE to destroy
                var tilesWithMules = new List<MapTile>();
                for (int y = 0; y < GameMap.Height; y++)
                {
                    for (int x = 0; x < GameMap.Width; x++)
                    {
                        var tile = game.Map.GetTile(x, y);
                        if (tile.InstalledMule != MuleType.None)
                            tilesWithMules.Add(tile);
                    }
                }

                if (tilesWithMules.Count > 0)
                {
                    var targetTile = tilesWithMules[game.Rng.Next(tilesWithMules.Count)];
                    result.AffectedX = targetTile.X;
                    result.AffectedY = targetTile.Y;
                    result.MuleLost = true;
                    targetTile.InstalledMule = MuleType.None;
                    targetTile.CrystiteQuality = 4; // Rich crystite deposit
                    result.Message = string.Format(EventMessages[type], targetTile.X, targetTile.Y);
                }
                else
                {
                    // No MULEs to destroy, create deposit on random mountain
                    result.Message = "A meteor struck but caused no damage.";
                }
                break;
        }

        return result;
    }

    private RandomEventResult CreatePlayerEvent(GameState game, int playerId, RandomEventType type)
    {
        var player = game.Players.First(p => p.Id == playerId);
        var result = new RandomEventResult
        {
            Type = type,
            AffectedPlayerId = playerId,
            Round = game.CurrentRound
        };

        switch (type)
        {
            case RandomEventType.BenefactorDonation:
            case RandomEventType.GoodReturnFromInvest:
            case RandomEventType.MuseumBuysArtifact:
            case RandomEventType.FriendRepaysLoan:
                // Money bonus based on round (50-200)
                int bonus = 50 + game.CurrentRound * 12 + game.Rng.Next(50);
                result.MoneyChange = bonus;
                result.Message = string.Format(EventMessages[type], bonus);
                break;

            case RandomEventType.LostMuleFound:
                result.MuleGained = true;
                result.Message = EventMessages[type];
                break;

            case RandomEventType.MuleRunsAway:
                // Find a MULE to destroy
                var playerTiles = game.Map.GetPlayerTiles(playerId)
                    .Where(t => t.InstalledMule != MuleType.None)
                    .ToList();

                if (playerTiles.Count > 0)
                {
                    var targetTile = playerTiles[game.Rng.Next(playerTiles.Count)];
                    result.AffectedX = targetTile.X;
                    result.AffectedY = targetTile.Y;
                    result.MuleLost = true;
                    string terrain = GetTerrainName(targetTile.Terrain);
                    result.Message = string.Format(EventMessages[type], terrain);
                    targetTile.InstalledMule = MuleType.None;
                }
                else
                {
                    // No MULEs to lose
                    result.Type = RandomEventType.BenefactorDonation;
                    result.MoneyChange = 25;
                    result.Message = "Nothing happened... but you found $25!";
                }
                break;

            case RandomEventType.PestAttack:
                // Find a food MULE to affect
                var foodTiles = game.Map.GetPlayerTiles(playerId)
                    .Where(t => t.InstalledMule == MuleType.Food)
                    .ToList();

                if (foodTiles.Count > 0)
                {
                    var targetTile = foodTiles[game.Rng.Next(foodTiles.Count)];
                    result.AffectedX = targetTile.X;
                    result.AffectedY = targetTile.Y;
                    string terrain = GetTerrainName(targetTile.Terrain);
                    result.Message = string.Format(EventMessages[type], terrain);
                    // Food production will be 0 for this plot this round
                }
                else
                {
                    result.Message = "Space pests arrived but found nothing to eat.";
                }
                break;

            case RandomEventType.CatbugEatsFood:
                int foodLost = Math.Min(player.Food, 1 + game.Rng.Next(3));
                result.FoodChange = -foodLost;
                result.Message = string.Format(EventMessages[type], foodLost);
                break;
        }

        return result;
    }

    private string GetTerrainName(TerrainType terrain) => terrain switch
    {
        TerrainType.River => "river",
        TerrainType.Plains => "plains",
        TerrainType.Mountains1 => "hills",
        TerrainType.Mountains2 => "mountains",
        TerrainType.Mountains3 => "high mountains",
        _ => "land"
    };
}
