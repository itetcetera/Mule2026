namespace MuleGame.Api.Models;

/// <summary>
/// Represents the colony store - buys/sells MULEs and resources
/// Prices follow Atari 800 original formulas
/// </summary>
public class Store
{
    // Store inventory
    public int MuleCount { get; set; }
    public int FoodStock { get; set; }
    public int EnergyStock { get; set; }
    public int SmithoreStock { get; set; }
    public int CrystiteStock { get; set; }

    // Current prices (dynamic based on supply/demand)
    public int FoodPrice { get; set; }
    public int EnergyPrice { get; set; }
    public int SmithorePrice { get; set; }
    public int CrystitePrice { get; set; }

    // Store price ranges (Atari 800 accurate - from disassembly)
    // Price limits from $28AD: Food 30, Energy 25, Smithore 35
    public const int FoodMinPrice = 4;
    public const int FoodMaxPrice = 100;
    public const int FoodBasePrice = 25;  // Initial price same as energy

    public const int EnergyMinPrice = 4;
    public const int EnergyMaxPrice = 100;
    public const int EnergyBasePrice = 25;

    public const int SmithoreMinPrice = 35;
    public const int SmithoreMaxPrice = 260;
    public const int SmithoreBasePrice = 50;

    public const int CrystiteMinPrice = 48;
    public const int CrystiteMaxPrice = 148;
    // Crystite price is random, not supply/demand based

    // MULE pricing - always 2x current Smithore price
    public int MulePrice => SmithorePrice * 2;

    /// <summary>
    /// Initialize store based on difficulty level
    /// </summary>
    public void Initialize(GameDifficulty difficulty)
    {
        switch (difficulty)
        {
            case GameDifficulty.Beginner:
                // Beginner: Unlimited MULEs (represented as high count), extra resources
                MuleCount = 99;
                FoodStock = 16;
                EnergyStock = 16;
                SmithoreStock = 16;
                CrystiteStock = 0;
                break;

            case GameDifficulty.Standard:
            case GameDifficulty.Tournament:
                // Standard/Tournament: Limited inventory
                MuleCount = 14;
                FoodStock = 8;
                EnergyStock = 8;
                SmithoreStock = 8;
                CrystiteStock = 0;
                break;
        }

        // Set initial prices
        FoodPrice = FoodBasePrice;
        EnergyPrice = EnergyBasePrice;
        SmithorePrice = SmithoreBasePrice;
        CrystitePrice = 100; // Mid-range start
    }

    /// <summary>
    /// Update prices based on supply and demand (Atari 800 formula)
    /// Called at the start of each auction phase
    /// </summary>
    public void UpdatePrices(int totalFood, int totalEnergy, int totalSmithore, int totalCrystite, Random rng)
    {
        // Food price: inversely proportional to total supply
        FoodPrice = CalculatePrice(totalFood + FoodStock, FoodMinPrice, FoodMaxPrice, FoodBasePrice, 40);

        // Energy price: inversely proportional to total supply
        EnergyPrice = CalculatePrice(totalEnergy + EnergyStock, EnergyMinPrice, EnergyMaxPrice, EnergyBasePrice, 35);

        // Smithore price: inversely proportional to total supply
        SmithorePrice = CalculatePrice(totalSmithore + SmithoreStock, SmithoreMinPrice, SmithoreMaxPrice, SmithoreBasePrice, 20);

        // Crystite price: Random between min and max (off-world market)
        CrystitePrice = rng.Next(CrystiteMinPrice, CrystiteMaxPrice + 1);
    }

    /// <summary>
    /// Calculate price based on supply (Atari 800 algorithm)
    /// Lower supply = higher price
    /// </summary>
    private int CalculatePrice(int totalSupply, int minPrice, int maxPrice, int basePrice, int threshold)
    {
        if (totalSupply <= 0)
            return maxPrice;

        if (totalSupply >= threshold * 2)
            return minPrice;

        // Linear interpolation based on supply
        double ratio = 1.0 - ((double)totalSupply / (threshold * 2));
        int price = (int)(minPrice + ratio * (maxPrice - minPrice));

        return Math.Clamp(price, minPrice, maxPrice);
    }

    /// <summary>
    /// Buy a MULE from the store
    /// </summary>
    public bool BuyMule(Player player, GameDifficulty difficulty)
    {
        if (MuleCount <= 0 && difficulty != GameDifficulty.Beginner)
            return false;

        if (player.Money < MulePrice)
            return false;

        player.Money -= MulePrice;
        player.HasMule = true;
        player.CurrentMuleType = MuleType.None;

        if (difficulty != GameDifficulty.Beginner)
            MuleCount--;

        return true;
    }

    /// <summary>
    /// Outfit a MULE with equipment
    /// </summary>
    public bool OutfitMule(Player player, MuleType type, GameDifficulty difficulty)
    {
        if (!player.HasMule)
            return false;

        if (type == MuleType.Crystite && difficulty != GameDifficulty.Tournament)
            return false;

        int cost = Player.GetMuleOutfitCost(type);
        if (player.Money < cost)
            return false;

        player.Money -= cost;
        player.CurrentMuleType = type;

        return true;
    }

    /// <summary>
    /// Sell resource to the store
    /// </summary>
    public int SellToStore(Player player, ResourceType resource, int quantity)
    {
        if (quantity <= 0)
            return 0;

        int available = GetPlayerResource(player, resource);
        int actualQuantity = Math.Min(quantity, available);

        if (actualQuantity <= 0)
            return 0;

        int price = GetResourcePrice(resource);
        int sellPrice = (int)(price * 0.75); // Store buys at 75% of sell price
        int totalValue = actualQuantity * sellPrice;

        // Update player resources
        SetPlayerResource(player, resource, available - actualQuantity);

        // Update store stock
        AddToStock(resource, actualQuantity);

        player.Money += totalValue;
        return totalValue;
    }

    /// <summary>
    /// Buy resource from the store
    /// </summary>
    public int BuyFromStore(Player player, ResourceType resource, int quantity)
    {
        if (quantity <= 0)
            return 0;

        int available = GetStoreStock(resource);
        int actualQuantity = Math.Min(quantity, available);

        if (actualQuantity <= 0)
            return 0;

        int price = GetResourcePrice(resource);
        int totalCost = actualQuantity * price;

        if (player.Money < totalCost)
        {
            // Buy what they can afford
            actualQuantity = player.Money / price;
            totalCost = actualQuantity * price;
        }

        if (actualQuantity <= 0)
            return 0;

        // Update player resources
        int current = GetPlayerResource(player, resource);
        SetPlayerResource(player, resource, current + actualQuantity);

        // Update store stock
        RemoveFromStock(resource, actualQuantity);

        player.Money -= totalCost;
        return actualQuantity;
    }

    // Helper methods
    public int GetResourcePrice(ResourceType resource) => resource switch
    {
        ResourceType.Food => FoodPrice,
        ResourceType.Energy => EnergyPrice,
        ResourceType.Smithore => SmithorePrice,
        ResourceType.Crystite => CrystitePrice,
        _ => 0
    };

    public int GetStoreStock(ResourceType resource) => resource switch
    {
        ResourceType.Food => FoodStock,
        ResourceType.Energy => EnergyStock,
        ResourceType.Smithore => SmithoreStock,
        ResourceType.Crystite => CrystiteStock,
        _ => 0
    };

    private void AddToStock(ResourceType resource, int amount)
    {
        switch (resource)
        {
            case ResourceType.Food: FoodStock += amount; break;
            case ResourceType.Energy: EnergyStock += amount; break;
            case ResourceType.Smithore: SmithoreStock += amount; break;
            case ResourceType.Crystite: CrystiteStock += amount; break;
        }
    }

    private void RemoveFromStock(ResourceType resource, int amount)
    {
        switch (resource)
        {
            case ResourceType.Food: FoodStock = Math.Max(0, FoodStock - amount); break;
            case ResourceType.Energy: EnergyStock = Math.Max(0, EnergyStock - amount); break;
            case ResourceType.Smithore: SmithoreStock = Math.Max(0, SmithoreStock - amount); break;
            case ResourceType.Crystite: CrystiteStock = Math.Max(0, CrystiteStock - amount); break;
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

    private void SetPlayerResource(Player player, ResourceType resource, int value)
    {
        switch (resource)
        {
            case ResourceType.Food: player.Food = value; break;
            case ResourceType.Energy: player.Energy = value; break;
            case ResourceType.Smithore: player.Smithore = value; break;
            case ResourceType.Crystite: player.Crystite = value; break;
        }
    }

    /// <summary>
    /// Restock store (colony ship event)
    /// </summary>
    public void Restock()
    {
        MuleCount = 14;
        FoodStock = 8;
        EnergyStock = 8;
        SmithoreStock = 8;
    }

    /// <summary>
    /// Clone store for state snapshots
    /// </summary>
    public Store Clone()
    {
        return new Store
        {
            MuleCount = MuleCount,
            FoodStock = FoodStock,
            EnergyStock = EnergyStock,
            SmithoreStock = SmithoreStock,
            CrystiteStock = CrystiteStock,
            FoodPrice = FoodPrice,
            EnergyPrice = EnergyPrice,
            SmithorePrice = SmithorePrice,
            CrystitePrice = CrystitePrice
        };
    }
}
