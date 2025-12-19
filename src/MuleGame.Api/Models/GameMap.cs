namespace MuleGame.Api.Models;

/// <summary>
/// Represents the game map - 9 columns x 5 rows (45 tiles)
/// The town occupies the center tile (4, 2)
/// The river runs through the center column (column 4)
/// </summary>
public class GameMap
{
    public const int Width = 9;
    public const int Height = 5;
    public const int TownX = 4;
    public const int TownY = 2;

    public MapTile[,] Tiles { get; private set; } = new MapTile[Width, Height];

    /// <summary>
    /// Generate a new random map (Atari 800 accurate terrain distribution)
    /// </summary>
    public void Generate(Random rng)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var tile = new MapTile { X = x, Y = y };

                // Town in the center
                if (x == TownX && y == TownY)
                {
                    tile.Terrain = TerrainType.Town;
                }
                // River runs through center column (except town)
                else if (x == TownX)
                {
                    tile.Terrain = TerrainType.River;
                }
                // Random terrain for other tiles
                else
                {
                    tile.Terrain = GenerateRandomTerrain(rng, x, y);
                }

                tile.CalculateQualityFromTerrain(rng);
                Tiles[x, y] = tile;
            }
        }
    }

    /// <summary>
    /// Generate terrain based on Atari 800 distribution
    /// Mountains are more common at edges, plains in middle
    /// </summary>
    private TerrainType GenerateRandomTerrain(Random rng, int x, int y)
    {
        // Distance from center affects mountain probability
        int distanceFromCenter = Math.Abs(x - TownX);

        // Weight probabilities based on position
        int mountainWeight = 20 + (distanceFromCenter * 15);
        int plainsWeight = 60 - (distanceFromCenter * 10);

        int roll = rng.Next(100);

        if (roll < mountainWeight)
        {
            // Determine mountain size (1-3 peaks)
            int peaks = rng.Next(3) + 1;
            return peaks switch
            {
                1 => TerrainType.Mountains1,
                2 => TerrainType.Mountains2,
                _ => TerrainType.Mountains3
            };
        }

        return TerrainType.Plains;
    }

    /// <summary>
    /// Get a tile at specific coordinates
    /// </summary>
    public MapTile GetTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException($"Invalid tile coordinates: ({x}, {y})");

        return Tiles[x, y];
    }

    /// <summary>
    /// Get all tiles that can be granted (not town, not owned)
    /// </summary>
    public List<MapTile> GetAvailableTiles()
    {
        var available = new List<MapTile>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var tile = Tiles[x, y];
                if (tile.Terrain != TerrainType.Town && tile.OwnerId == null)
                {
                    available.Add(tile);
                }
            }
        }
        return available;
    }

    /// <summary>
    /// Get tiles adjacent to a given tile (for production bonuses)
    /// </summary>
    public List<MapTile> GetAdjacentTiles(int x, int y)
    {
        var adjacent = new List<MapTile>();
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
            {
                adjacent.Add(Tiles[nx, ny]);
            }
        }

        return adjacent;
    }

    /// <summary>
    /// Count how many adjacent tiles are owned by the same player producing the same resource
    /// </summary>
    public int CountAdjacentSameProduction(int x, int y, int playerId, MuleType muleType)
    {
        int count = 0;
        var adjacent = GetAdjacentTiles(x, y);

        foreach (var tile in adjacent)
        {
            if (tile.OwnerId == playerId && tile.InstalledMule == muleType)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Count total tiles producing same resource for a player (for 3+ bonus)
    /// </summary>
    public int CountTotalSameProduction(int playerId, MuleType muleType)
    {
        int count = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var tile = Tiles[x, y];
                if (tile.OwnerId == playerId && tile.InstalledMule == muleType)
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Get all tiles owned by a player
    /// </summary>
    public List<MapTile> GetPlayerTiles(int playerId)
    {
        var tiles = new List<MapTile>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (Tiles[x, y].OwnerId == playerId)
                {
                    tiles.Add(Tiles[x, y]);
                }
            }
        }
        return tiles;
    }

    /// <summary>
    /// Clone map for state snapshots
    /// </summary>
    public GameMap Clone()
    {
        var clone = new GameMap();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                clone.Tiles[x, y] = Tiles[x, y].Clone();
            }
        }
        return clone;
    }
}
