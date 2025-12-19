using MuleGame.Api.Models;

namespace MuleGame.Api.Services;

/// <summary>
/// AI service interface
/// </summary>
public interface IAIService
{
    void ProcessAITurn(GameState game, int playerId);
    (int X, int Y) SelectLandForAI(GameState game, int playerId);
    int GetAILandBid(GameState game, int playerId, MapTile plot);
    MuleType SelectMuleTypeForAI(GameState game, int playerId);
    (int X, int Y)? SelectPlotToInstallMule(GameState game, int playerId, MuleType muleType);
    (int Price, bool IsBuying) GetAIAuctionPosition(GameState game, int playerId, ResourceType resource);
}

/// <summary>
/// AI service implementation (Atari 800 accurate computer player behavior)
///
/// AI behavior characteristics:
/// - Mechtron is the computer's preferred species
/// - AI prioritizes smithore production early, energy mid-game
/// - AI bids strategically in auctions
/// - AI installs MULEs efficiently (adjacency bonus awareness)
/// </summary>
public class AIService : IAIService
{
    /// <summary>
    /// Process a full AI turn during development phase
    /// </summary>
    public void ProcessAITurn(GameState game, int playerId)
    {
        var player = game.Players.First(p => p.Id == playerId);

        // AI decision making based on current resources
        var decision = AnalyzeNeeds(game, player);

        // Try to buy and install a MULE if beneficial
        if (ShouldBuyMule(game, player, decision))
        {
            // Buy MULE
            if (game.Store.MuleCount > 0 && player.Money >= game.Store.MulePrice)
            {
                game.Store.BuyMule(player, game.Difficulty);

                // Outfit MULE
                var muleType = SelectMuleTypeForAI(game, playerId);
                int outfitCost = Player.GetMuleOutfitCost(muleType);

                if (player.Money >= outfitCost)
                {
                    game.Store.OutfitMule(player, muleType, game.Difficulty);

                    // Find best plot to install
                    var installPlot = SelectPlotToInstallMule(game, playerId, muleType);
                    if (installPlot.HasValue)
                    {
                        var tile = game.Map.GetTile(installPlot.Value.X, installPlot.Value.Y);
                        tile.InstalledMule = muleType;
                        player.HasMule = false;
                        player.CurrentMuleType = MuleType.None;
                    }
                }
            }
        }

        // AI always goes to pub to end turn (maximize gambling winnings)
        // This happens automatically when EndTurn is called
    }

    /// <summary>
    /// Analyze what the AI needs most
    /// </summary>
    private AIDecision AnalyzeNeeds(GameState game, Player player)
    {
        var decision = new AIDecision();

        // Count current production by type
        var playerTiles = game.Map.GetPlayerTiles(player.Id);
        int foodMules = playerTiles.Count(t => t.InstalledMule == MuleType.Food);
        int energyMules = playerTiles.Count(t => t.InstalledMule == MuleType.Energy);
        int smithoreMules = playerTiles.Count(t => t.InstalledMule == MuleType.Smithore);
        int crystiteMules = playerTiles.Count(t => t.InstalledMule == MuleType.Crystite);

        // Early game: prioritize smithore and energy
        if (game.CurrentRound <= 4)
        {
            if (smithoreMules < 2)
                decision.Priority = MuleType.Smithore;
            else if (energyMules < 2)
                decision.Priority = MuleType.Energy;
            else
                decision.Priority = MuleType.Food;
        }
        // Mid game: balance production
        else if (game.CurrentRound <= 8)
        {
            if (energyMules < smithoreMules + foodMules)
                decision.Priority = MuleType.Energy;
            else if (player.Food < 4)
                decision.Priority = MuleType.Food;
            else
                decision.Priority = MuleType.Smithore;
        }
        // Late game: maximize value
        else
        {
            if (game.Difficulty == GameDifficulty.Tournament && crystiteMules < 2)
                decision.Priority = MuleType.Crystite;
            else if (smithoreMules < 3)
                decision.Priority = MuleType.Smithore;
            else
                decision.Priority = MuleType.Food;
        }

        decision.NeedsMule = playerTiles.Any(t => t.InstalledMule == MuleType.None);
        decision.AvailableMoney = player.Money;

        return decision;
    }

    private bool ShouldBuyMule(GameState game, Player player, AIDecision decision)
    {
        // Always try to get MULEs on empty plots
        var emptyPlots = game.Map.GetPlayerTiles(player.Id)
            .Count(t => t.InstalledMule == MuleType.None);

        if (emptyPlots == 0)
            return false;

        int totalCost = game.Store.MulePrice + Player.GetMuleOutfitCost(decision.Priority);

        // Keep reserve for auctions
        int reserve = 200 + (game.CurrentRound * 20);

        return player.Money >= totalCost + reserve;
    }

    /// <summary>
    /// Select land during land grant phase
    /// </summary>
    public (int X, int Y) SelectLandForAI(GameState game, int playerId)
    {
        var player = game.Players.First(p => p.Id == playerId);
        var available = game.Map.GetAvailableTiles();

        if (available.Count == 0)
            return (GameMap.TownX, GameMap.TownY);

        // Score each tile based on AI priorities
        var decision = AnalyzeNeeds(game, player);
        var scored = available.Select(t => new
        {
            Tile = t,
            Score = ScoreTile(game, player, t, decision)
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        var best = scored.First().Tile;
        return (best.X, best.Y);
    }

    private int ScoreTile(GameState game, Player player, MapTile tile, AIDecision decision)
    {
        int score = 0;

        // Base score from quality
        switch (decision.Priority)
        {
            case MuleType.Food:
                score += tile.FoodQuality * 20;
                break;
            case MuleType.Energy:
                score += tile.EnergyQuality * 20;
                break;
            case MuleType.Smithore:
                score += tile.SmithoreQuality * 25; // Slightly prefer smithore
                break;
            case MuleType.Crystite:
                score += tile.CrystiteQuality * 30; // Crystite is valuable
                break;
        }

        // Adjacency bonus - prefer tiles near owned tiles
        var adjacent = game.Map.GetAdjacentTiles(tile.X, tile.Y);
        score += adjacent.Count(t => t.OwnerId == player.Id) * 15;

        // Prefer river tiles for food
        if (tile.Terrain == TerrainType.River)
            score += 10;

        // Prefer mountains for smithore/crystite
        if ((tile.Terrain == TerrainType.Mountains2 || tile.Terrain == TerrainType.Mountains3) &&
            (decision.Priority == MuleType.Smithore || decision.Priority == MuleType.Crystite))
            score += 20;

        return score;
    }

    /// <summary>
    /// Determine bid for land auction
    /// </summary>
    public int GetAILandBid(GameState game, int playerId, MapTile plot)
    {
        var player = game.Players.First(p => p.Id == playerId);
        var decision = AnalyzeNeeds(game, player);

        // Calculate plot value based on priority
        int baseValue = ScoreTile(game, player, plot, decision) * 5;

        // AI won't bid more than 30% of money
        int maxBid = player.Money * 30 / 100;

        // Minimum bid
        int minBid = 100;

        return Math.Clamp(baseValue, minBid, maxBid);
    }

    /// <summary>
    /// Select MULE type to outfit
    /// </summary>
    public MuleType SelectMuleTypeForAI(GameState game, int playerId)
    {
        var player = game.Players.First(p => p.Id == playerId);
        var decision = AnalyzeNeeds(game, player);
        return decision.Priority;
    }

    /// <summary>
    /// Select best plot to install a MULE
    /// </summary>
    public (int X, int Y)? SelectPlotToInstallMule(GameState game, int playerId, MuleType muleType)
    {
        var playerTiles = game.Map.GetPlayerTiles(playerId)
            .Where(t => t.InstalledMule == MuleType.None)
            .ToList();

        if (playerTiles.Count == 0)
            return null;

        var resourceType = muleType switch
        {
            MuleType.Food => ResourceType.Food,
            MuleType.Energy => ResourceType.Energy,
            MuleType.Smithore => ResourceType.Smithore,
            MuleType.Crystite => ResourceType.Crystite,
            _ => ResourceType.Food
        };

        // Score by quality and adjacency
        var best = playerTiles
            .OrderByDescending(t =>
            {
                int quality = t.GetQuality(resourceType);
                int adjacency = game.Map.CountAdjacentSameProduction(t.X, t.Y, playerId, muleType);
                return quality * 10 + adjacency * 5;
            })
            .FirstOrDefault();

        if (best == null)
            return null;

        return (best.X, best.Y);
    }

    /// <summary>
    /// Get AI position for resource auction
    /// </summary>
    public (int Price, bool IsBuying) GetAIAuctionPosition(GameState game, int playerId, ResourceType resource)
    {
        var player = game.Players.First(p => p.Id == playerId);
        int playerStock = GetPlayerResource(player, resource);
        int storePrice = game.Store.GetResourcePrice(resource);

        // Determine if AI should buy or sell
        int threshold = resource switch
        {
            ResourceType.Food => 6,      // Keep 6 food
            ResourceType.Energy => 4,    // Keep 4 energy
            ResourceType.Smithore => 3,  // Keep 3 smithore
            ResourceType.Crystite => 2,  // Keep 2 crystite
            _ => 4
        };

        if (playerStock > threshold + 2)
        {
            // Sell excess - ask slightly above store buy price
            int askPrice = (int)(storePrice * 0.9);
            return (askPrice, false);
        }
        else if (playerStock < threshold)
        {
            // Buy to meet threshold - bid slightly below store sell price
            int bidPrice = (int)(storePrice * 0.85);
            return (bidPrice, true);
        }
        else
        {
            // No strong preference - sit out or sell at high price
            return (storePrice * 2, false);
        }
    }

    private int GetPlayerResource(Player player, ResourceType resource) => resource switch
    {
        ResourceType.Food => player.Food,
        ResourceType.Energy => player.Energy,
        ResourceType.Smithore => player.Smithore,
        ResourceType.Crystite => player.Crystite,
        _ => 0
    };

    private class AIDecision
    {
        public MuleType Priority { get; set; } = MuleType.Food;
        public bool NeedsMule { get; set; }
        public int AvailableMoney { get; set; }
    }
}
