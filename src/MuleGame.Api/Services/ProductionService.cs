using MuleGame.Api.Models;

namespace MuleGame.Api.Services;

/// <summary>
/// Production service interface
/// </summary>
public interface IProductionService
{
    List<ProductionResult> CalculateProduction(GameState game);
    void ApplyProduction(GameState game, List<ProductionResult> results);
}

/// <summary>
/// Handles production calculations (Atari 800 accurate)
/// Each MULE can produce 0-8 units based on:
/// - Terrain quality (0-4 dots)
/// - Random variation
/// - Energy availability
/// - Adjacency bonus
/// - Group bonus (3+ same production)
/// - Weather/event modifiers
/// </summary>
public class ProductionService : IProductionService
{
    /// <summary>
    /// Calculate production for all plots
    /// </summary>
    public List<ProductionResult> CalculateProduction(GameState game)
    {
        var results = new List<ProductionResult>();

        // First pass: Calculate base production for each plot
        foreach (var player in game.Players)
        {
            // Calculate energy requirements
            int energyMulesCount = 0;
            int nonEnergyMulesCount = 0;

            foreach (var (x, y) in player.OwnedPlots)
            {
                var tile = game.Map.GetTile(x, y);
                if (tile.InstalledMule == MuleType.Energy)
                    energyMulesCount++;
                else if (tile.InstalledMule != MuleType.None)
                    nonEnergyMulesCount++;
            }

            // Energy produced this turn (calculated first)
            int energyProduced = 0;
            foreach (var (x, y) in player.OwnedPlots)
            {
                var tile = game.Map.GetTile(x, y);
                if (tile.InstalledMule == MuleType.Energy)
                {
                    int baseProduction = CalculateBaseProduction(game, tile, ResourceType.Energy);
                    energyProduced += baseProduction;
                }
            }

            // Total energy available = stored + produced
            int totalEnergy = player.Energy + energyProduced;

            // Energy needed for non-energy MULEs
            int energyNeeded = nonEnergyMulesCount;

            // How many MULEs will have power outage
            int unpoweredMules = Math.Max(0, energyNeeded - totalEnergy);
            var unpoweredIndices = new HashSet<int>();

            if (unpoweredMules > 0)
            {
                // Randomly select which MULEs won't produce
                var nonEnergyPlots = player.OwnedPlots
                    .Select((p, i) => (plot: p, index: i))
                    .Where(x => game.Map.GetTile(x.plot.X, x.plot.Y).InstalledMule != MuleType.None &&
                               game.Map.GetTile(x.plot.X, x.plot.Y).InstalledMule != MuleType.Energy)
                    .OrderBy(_ => game.Rng.Next())
                    .Take(unpoweredMules)
                    .Select(x => x.index)
                    .ToHashSet();

                unpoweredIndices = nonEnergyPlots;
            }

            // Calculate production for each plot
            int plotIndex = 0;
            foreach (var (x, y) in player.OwnedPlots)
            {
                var tile = game.Map.GetTile(x, y);

                if (tile.InstalledMule == MuleType.None)
                {
                    plotIndex++;
                    continue;
                }

                var resourceType = MuleTypeToResourceType(tile.InstalledMule);
                var result = new ProductionResult
                {
                    PlayerId = player.Id,
                    X = x,
                    Y = y,
                    Resource = resourceType
                };

                // Check for energy shortage
                if (tile.InstalledMule != MuleType.Energy && unpoweredIndices.Contains(plotIndex))
                {
                    result.EnergyShortage = true;
                    result.TotalProduction = 0;
                    results.Add(result);
                    plotIndex++;
                    continue;
                }

                // Base production (0-8 based on quality)
                result.BaseProduction = CalculateBaseProduction(game, tile, resourceType);

                // Adjacency bonus
                result.AdjacencyBonus = CalculateAdjacencyBonus(game, tile, player.Id);

                // Group bonus (3+ same production type)
                result.GroupBonus = CalculateGroupBonus(game, player.Id, tile.InstalledMule);

                // Event modifiers
                result.EventModifier = CalculateEventModifier(game, tile);

                // Total production
                result.TotalProduction = Math.Max(0,
                    result.BaseProduction +
                    result.AdjacencyBonus +
                    result.GroupBonus +
                    result.EventModifier);

                results.Add(result);
                plotIndex++;
            }
        }

        return results;
    }

    /// <summary>
    /// Calculate base production for a tile (Atari 800 algorithm)
    /// Quality 0-4 maps to average production 0-2, 1-3, 2-4, 3-5, 4-6
    /// With random variation of +/- 2
    /// </summary>
    private int CalculateBaseProduction(GameState game, MapTile tile, ResourceType resource)
    {
        int quality = tile.GetQuality(resource);

        // Standard mode has random variation
        if (game.Difficulty != GameDifficulty.Beginner)
        {
            // Base is quality + 1, with +/- 2 random
            int baseValue = quality + 1;
            int variation = game.Rng.Next(-2, 3); // -2 to +2
            return Math.Clamp(baseValue + variation, 0, 8);
        }
        else
        {
            // Beginner mode is more predictable
            return quality + 2;
        }
    }

    /// <summary>
    /// Calculate adjacency bonus (Atari 800 accurate)
    /// +1 for each adjacent plot producing same resource owned by same player
    /// </summary>
    private int CalculateAdjacencyBonus(GameState game, MapTile tile, int playerId)
    {
        return game.Map.CountAdjacentSameProduction(tile.X, tile.Y, playerId, tile.InstalledMule);
    }

    /// <summary>
    /// Calculate group bonus (Atari 800 accurate)
    /// +1 for every 3 plots producing same resource
    /// </summary>
    private int CalculateGroupBonus(GameState game, int playerId, MuleType muleType)
    {
        int count = game.Map.CountTotalSameProduction(playerId, muleType);
        return count / 3;
    }

    /// <summary>
    /// Calculate event modifier from pending events
    /// </summary>
    private int CalculateEventModifier(GameState game, MapTile tile)
    {
        int modifier = 0;

        foreach (var evt in game.PendingEvents)
        {
            switch (evt.Type)
            {
                case RandomEventType.Sunspot:
                    // +2 energy production
                    if (tile.InstalledMule == MuleType.Energy)
                        modifier += 2;
                    break;

                case RandomEventType.AcidRainStorm:
                    // Affects tiles in same column, +2 food, -2 energy
                    if (evt.AffectedX == tile.X)
                    {
                        if (tile.InstalledMule == MuleType.Food)
                            modifier += 2;
                        else if (tile.InstalledMule == MuleType.Energy)
                            modifier -= 2;
                    }
                    break;

                case RandomEventType.Planetquake:
                    // -2 smithore and crystite production
                    if (tile.InstalledMule == MuleType.Smithore ||
                        tile.InstalledMule == MuleType.Crystite)
                        modifier -= 2;
                    break;

                case RandomEventType.PestAttack:
                    // Destroys all food on affected plot
                    if (evt.AffectedX == tile.X && evt.AffectedY == tile.Y &&
                        tile.InstalledMule == MuleType.Food)
                        modifier = -100; // Will result in 0 production
                    break;
            }
        }

        return modifier;
    }

    /// <summary>
    /// Apply production results to player resources
    /// </summary>
    public void ApplyProduction(GameState game, List<ProductionResult> results)
    {
        foreach (var result in results)
        {
            var player = game.Players.First(p => p.Id == result.PlayerId);

            switch (result.Resource)
            {
                case ResourceType.Food:
                    player.Food += result.TotalProduction;
                    break;
                case ResourceType.Energy:
                    player.Energy += result.TotalProduction;
                    break;
                case ResourceType.Smithore:
                    player.Smithore += result.TotalProduction;
                    break;
                case ResourceType.Crystite:
                    player.Crystite += result.TotalProduction;
                    break;
            }
        }

        // Consume energy used for non-energy MULEs
        foreach (var player in game.Players)
        {
            int nonEnergyMules = player.OwnedPlots
                .Select(p => game.Map.GetTile(p.X, p.Y))
                .Count(t => t.InstalledMule != MuleType.None && t.InstalledMule != MuleType.Energy);

            player.Energy = Math.Max(0, player.Energy - nonEnergyMules);
        }
    }

    private ResourceType MuleTypeToResourceType(MuleType mule) => mule switch
    {
        MuleType.Food => ResourceType.Food,
        MuleType.Energy => ResourceType.Energy,
        MuleType.Smithore => ResourceType.Smithore,
        MuleType.Crystite => ResourceType.Crystite,
        _ => ResourceType.Food
    };
}
