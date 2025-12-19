using MuleGame.Api.Models;

namespace MuleGame.Api.Services;

/// <summary>
/// Auction service interface
/// </summary>
public interface IAuctionService
{
    void CheckForTrades(GameState game);
    void ExecuteTrade(GameState game, int buyerId, int sellerId);
}

/// <summary>
/// Handles the resource auction phase (Atari 800 accurate)
///
/// Auction mechanics:
/// - Buyers start at bottom of screen (low prices)
/// - Sellers start at top of screen (high prices)
/// - Players move toward each other
/// - When a buyer and seller meet (same price), trade occurs
/// - Store provides floor/ceiling prices
/// </summary>
public class AuctionService : IAuctionService
{
    /// <summary>
    /// Check if any buyers and sellers have matching prices
    /// </summary>
    public void CheckForTrades(GameState game)
    {
        var state = game.ResourceAuctionState;
        if (state == null) return;

        var buyers = state.BuyerPositions.ToList();
        var sellers = state.SellerPositions.ToList();

        // Sort buyers by price descending (highest bidder first)
        buyers.Sort((a, b) => b.Value.CompareTo(a.Value));

        // Sort sellers by price ascending (lowest asker first)
        sellers.Sort((a, b) => a.Value.CompareTo(b.Value));

        // Match trades
        while (buyers.Count > 0 && sellers.Count > 0)
        {
            var topBuyer = buyers[0];
            var topSeller = sellers[0];

            // Check if prices overlap or meet
            if (topBuyer.Value >= topSeller.Value)
            {
                // Trade at seller's price (Atari 800 rule)
                ExecuteTradeInternal(game, topBuyer.Key, topSeller.Key, topSeller.Value);

                // Remove from auction
                state.BuyerPositions.Remove(topBuyer.Key);
                state.SellerPositions.Remove(topSeller.Key);
                buyers.RemoveAt(0);
                sellers.RemoveAt(0);
            }
            else
            {
                // No more matches possible
                break;
            }
        }
    }

    /// <summary>
    /// Execute a trade between two players
    /// </summary>
    public void ExecuteTrade(GameState game, int buyerId, int sellerId)
    {
        var state = game.ResourceAuctionState;
        if (state == null) return;

        if (!state.BuyerPositions.TryGetValue(buyerId, out int buyerPrice))
            return;
        if (!state.SellerPositions.TryGetValue(sellerId, out int sellerPrice))
            return;

        // Use the price where they meet (midpoint or seller's price)
        int tradePrice = sellerPrice;

        ExecuteTradeInternal(game, buyerId, sellerId, tradePrice);

        // Remove from auction positions
        state.BuyerPositions.Remove(buyerId);
        state.SellerPositions.Remove(sellerId);
    }

    private void ExecuteTradeInternal(GameState game, int buyerId, int sellerId, int price)
    {
        var state = game.ResourceAuctionState!;
        var buyer = game.Players.First(p => p.Id == buyerId);
        var seller = game.Players.First(p => p.Id == sellerId);

        // Determine quantity (1 unit per trade in original)
        int quantity = 1;

        // Verify buyer has money
        if (buyer.Money < price)
            return;

        // Verify seller has resource
        int sellerStock = GetPlayerResource(seller, state.CurrentResource);
        if (sellerStock < quantity)
            return;

        // Execute trade
        buyer.Money -= price;
        seller.Money += price;

        SetPlayerResource(seller, state.CurrentResource, sellerStock - quantity);
        int buyerStock = GetPlayerResource(buyer, state.CurrentResource);
        SetPlayerResource(buyer, state.CurrentResource, buyerStock + quantity);

        // Record trade
        state.ExecutedTrades.Add(new Trade
        {
            BuyerId = buyerId,
            SellerId = sellerId,
            Resource = state.CurrentResource,
            Quantity = quantity,
            PricePerUnit = price
        });
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
}
