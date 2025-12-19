namespace MuleGame.Api.Models;

/// <summary>
/// Represents a single tile on the game map
/// </summary>
public class MapTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public TerrainType Terrain { get; set; }

    // Ownership
    public int? OwnerId { get; set; }
    public MuleType InstalledMule { get; set; } = MuleType.None;

    // Production quality (0-4 dots in original game)
    // These are base values before modifiers
    public int FoodQuality { get; set; }        // 0-4
    public int EnergyQuality { get; set; }      // 0-4
    public int SmithoreQuality { get; set; }    // 0-4
    public int CrystiteQuality { get; set; }    // 0-4 (hidden until assayed)
    public bool CrystiteAssayed { get; set; }   // Has player used assay office?

    // For selling plots
    public bool MarkedForSale { get; set; }

    /// <summary>
    /// Calculate production quality based on terrain type (Atari 800 accurate)
    /// </summary>
    public void CalculateQualityFromTerrain(Random rng)
    {
        switch (Terrain)
        {
            case TerrainType.River:
                // Rivers: Excellent for food, good energy, NO mining allowed
                FoodQuality = 3 + rng.Next(2);  // 3-4
                EnergyQuality = 2 + rng.Next(2); // 2-3
                SmithoreQuality = 0;             // Cannot mine on river
                CrystiteQuality = 0;             // Cannot mine on river
                break;

            case TerrainType.Plains:
                // Plains: Good for energy, some food, low mining
                FoodQuality = 1 + rng.Next(2);   // 1-2
                EnergyQuality = 3 + rng.Next(2); // 3-4
                SmithoreQuality = rng.Next(2);   // 0-1
                CrystiteQuality = rng.Next(3);   // 0-2
                break;

            case TerrainType.Mountains1:
                // 1 peak: Moderate mining
                FoodQuality = rng.Next(2);       // 0-1
                EnergyQuality = 1 + rng.Next(2); // 1-2
                SmithoreQuality = 2 + rng.Next(2); // 2-3
                CrystiteQuality = 1 + rng.Next(3); // 1-3
                break;

            case TerrainType.Mountains2:
                // 2 peaks: Good mining
                FoodQuality = rng.Next(1);       // 0
                EnergyQuality = 1 + rng.Next(2); // 1-2
                SmithoreQuality = 3 + rng.Next(2); // 3-4
                CrystiteQuality = 2 + rng.Next(3); // 2-4
                break;

            case TerrainType.Mountains3:
                // 3 peaks: Best mining
                FoodQuality = 0;
                EnergyQuality = rng.Next(2);     // 0-1
                SmithoreQuality = 4;              // Maximum
                CrystiteQuality = 3 + rng.Next(2); // 3-4
                break;

            case TerrainType.Town:
                // Town: No production
                FoodQuality = 0;
                EnergyQuality = 0;
                SmithoreQuality = 0;
                CrystiteQuality = 0;
                break;
        }
    }

    /// <summary>
    /// Get the production quality for a specific resource type
    /// </summary>
    public int GetQuality(ResourceType resource) => resource switch
    {
        ResourceType.Food => FoodQuality,
        ResourceType.Energy => EnergyQuality,
        ResourceType.Smithore => SmithoreQuality,
        ResourceType.Crystite => CrystiteQuality,
        _ => 0
    };

    /// <summary>
    /// Check if this tile can produce a given resource
    /// </summary>
    public bool CanProduce(ResourceType resource)
    {
        // Rivers cannot mine
        if (Terrain == TerrainType.River &&
            (resource == ResourceType.Smithore || resource == ResourceType.Crystite))
            return false;

        // Town produces nothing
        if (Terrain == TerrainType.Town)
            return false;

        return GetQuality(resource) > 0;
    }

    /// <summary>
    /// Clone tile for state snapshots
    /// </summary>
    public MapTile Clone()
    {
        return new MapTile
        {
            X = X,
            Y = Y,
            Terrain = Terrain,
            OwnerId = OwnerId,
            InstalledMule = InstalledMule,
            FoodQuality = FoodQuality,
            EnergyQuality = EnergyQuality,
            SmithoreQuality = SmithoreQuality,
            CrystiteQuality = CrystiteQuality,
            CrystiteAssayed = CrystiteAssayed,
            MarkedForSale = MarkedForSale
        };
    }
}
