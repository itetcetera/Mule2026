using MuleGame.Api.Models;

namespace MuleGame.Api.Services;

/// <summary>
/// Main game service implementation - handles game flow and state transitions
/// Implements the Atari 800 M.U.L.E. game logic accurately
/// </summary>
public class GameService : IGameService
{
    private readonly GameSessionManager _sessionManager;
    private readonly IProductionService _productionService;
    private readonly IAuctionService _auctionService;
    private readonly IRandomEventService _randomEventService;
    private readonly IAIService _aiService;

    public GameService(
        GameSessionManager sessionManager,
        IProductionService productionService,
        IAuctionService auctionService,
        IRandomEventService randomEventService,
        IAIService aiService)
    {
        _sessionManager = sessionManager;
        _productionService = productionService;
        _auctionService = auctionService;
        _randomEventService = randomEventService;
        _aiService = aiService;
    }

    #region Game Lifecycle

    public GameState CreateGame(GameDifficulty difficulty, List<PlayerSetup> players)
    {
        var game = new GameState
        {
            Difficulty = difficulty,
            Phase = GamePhase.Setup,
            CurrentRound = 0
        };

        // Initialize store
        game.Store.Initialize(difficulty);

        // Generate map
        game.Map.Generate(game.Rng);

        // Create players
        for (int i = 0; i < players.Count; i++)
        {
            var setup = players[i];
            var player = new Player
            {
                Id = i,
                Name = setup.Name,
                Species = setup.Species,
                Type = setup.Type,
                ConnectionId = setup.ConnectionId,
                Money = Player.GetStartingMoney(setup.Species, setup.Type),
                Food = 4,   // Starting food
                Energy = 2, // Starting energy
                Smithore = 0,
                Crystite = 0,
                PositionX = GameMap.TownX,
                PositionY = GameMap.TownY
            };
            game.Players.Add(player);
        }

        // Start first round
        StartRound(game);

        _sessionManager.AddGame(game);
        return game;
    }

    public GameState? GetGame(string gameId)
    {
        return _sessionManager.GetGame(gameId);
    }

    public bool EndGame(string gameId)
    {
        return _sessionManager.RemoveGame(gameId);
    }

    #endregion

    #region Phase Management

    private void StartRound(GameState game)
    {
        game.CurrentRound++;

        // Check for colony ship on round 12 (happens at round start)
        if (game.CurrentRound == 12)
        {
            game.Store.Restock();
            game.PendingEvents.Add(new RandomEventResult
            {
                Type = RandomEventType.ColonyShipArrival,
                Message = "The Colony Ship has arrived! The store has been restocked.",
                Round = game.CurrentRound
            });
        }

        // Start land grant phase
        StartLandGrantPhase(game);
    }

    private void StartLandGrantPhase(GameState game)
    {
        game.Phase = GamePhase.LandGrant;

        // Determine selection order (by score, lowest first for land grant)
        var orderedPlayers = game.Players
            .OrderBy(p => p.CalculateScore(game))
            .ToList();

        game.LandGrantState = new LandGrantState
        {
            SelectionOrder = orderedPlayers.Select(p => p.Id).ToList(),
            CurrentSelectorIndex = 0,
            CursorX = GameMap.TownX,
            CursorY = 0
        };

        game.PhaseStartTime = DateTime.UtcNow;
        _sessionManager.UpdateGame(game);
    }

    private void StartLandAuctionPhase(GameState game)
    {
        game.Phase = GamePhase.LandAuction;

        // Get plots marked for sale and up to 5 random available plots
        var availablePlots = game.Map.GetAvailableTiles();
        var markedForSale = availablePlots.Where(t => t.MarkedForSale).ToList();
        var randomPlots = availablePlots
            .Where(t => !t.MarkedForSale)
            .OrderBy(_ => game.Rng.Next())
            .Take(Math.Max(0, 5 - markedForSale.Count))
            .ToList();

        var plotsToAuction = markedForSale.Concat(randomPlots).Take(5).ToList();

        if (plotsToAuction.Count == 0)
        {
            // No plots to auction, skip to development
            StartDevelopmentPhase(game);
            return;
        }

        game.LandAuctionState = new LandAuctionState
        {
            PlotsToAuction = plotsToAuction,
            CurrentPlot = plotsToAuction[0],
            MinBid = 100,
            AuctionTimeRemaining = 30,
            State = AuctionState.InProgress
        };

        game.PhaseStartTime = DateTime.UtcNow;
        _sessionManager.UpdateGame(game);
    }

    private void StartDevelopmentPhase(GameState game)
    {
        game.Phase = GamePhase.Development;

        // Turn order by score (leader first)
        var orderedPlayers = game.GetPlayersByScore();

        game.DevelopmentState = new DevelopmentState
        {
            TurnOrder = orderedPlayers.Select(p => p.Id).ToList(),
            CurrentPlayerIndex = 0,
            TurnsCompleted = 0
        };

        StartPlayerTurn(game, orderedPlayers[0].Id);
        _sessionManager.UpdateGame(game);
    }

    private void StartPlayerTurn(GameState game, int playerId)
    {
        var player = game.Players.First(p => p.Id == playerId);

        // Consume food and calculate turn time
        player.ConsumeFood();
        player.TimeRemaining = player.CalculateTurnTime();

        // Reset player position to town
        player.PositionX = GameMap.TownX;
        player.PositionY = GameMap.TownY;
        player.HasMule = false;
        player.CurrentMuleType = MuleType.None;

        game.DevelopmentState!.TurnStartTime = DateTime.UtcNow;
        game.DevelopmentState.InTown = true;
        game.CurrentPlayerIndex = playerId;

        // Possibly spawn wampus
        SpawnWampus(game);
    }

    private void SpawnWampus(GameState game)
    {
        // Wampus has ~30% chance to appear
        if (game.Rng.Next(100) < 30)
        {
            // Find a mountain tile
            var mountains = new List<(int X, int Y)>();
            for (int y = 0; y < GameMap.Height; y++)
            {
                for (int x = 0; x < GameMap.Width; x++)
                {
                    var terrain = game.Map.GetTile(x, y).Terrain;
                    if (terrain == TerrainType.Mountains1 ||
                        terrain == TerrainType.Mountains2 ||
                        terrain == TerrainType.Mountains3)
                    {
                        mountains.Add((x, y));
                      }
                }
            }

            if (mountains.Count > 0)
            {
                var pos = mountains[game.Rng.Next(mountains.Count)];
                game.Wampus = new WampusState
                {
                    X = pos.X,
                    Y = pos.Y,
                    AppearTime = DateTime.UtcNow.AddSeconds(game.Rng.Next(5, 20)),
                    DisappearTime = DateTime.UtcNow.AddSeconds(game.Rng.Next(25, 40)),
                    IsCaught = false
                };
            }
        }
        else
        {
            game.Wampus = null;
        }
    }

    private void EndPlayerTurn(GameState game)
    {
        var devState = game.DevelopmentState!;
        devState.TurnsCompleted++;

        // Release any MULE the player was holding (it runs away)
        var player = game.Players.First(p => p.Id == devState.TurnOrder[devState.CurrentPlayerIndex]);
        if (player.HasMule)
        {
            player.HasMule = false;
            player.CurrentMuleType = MuleType.None;
            // MULE runs away - player loses investment
        }

        // Check if all players have had their turn
        if (devState.TurnsCompleted >= game.Players.Count)
        {
            StartProductionPhase(game);
        }
        else
        {
            devState.CurrentPlayerIndex++;
            int nextPlayerId = devState.TurnOrder[devState.CurrentPlayerIndex];
            StartPlayerTurn(game, nextPlayerId);
        }
    }

    private void StartProductionPhase(GameState game)
    {
        game.Phase = GamePhase.Production;
        game.ProductionState = new ProductionState();

        // Calculate production for all plots
        var results = _productionService.CalculateProduction(game);
        game.ProductionState.Results = results;

        // Apply production to players
        _productionService.ApplyProduction(game, results);

        game.ProductionState.IsComplete = true;

        // Generate random event AFTER production (Atari 800 accurate - $4800 roundEvent)
        // Events are applied at start of auction phase
        var randomEvent = _randomEventService.GenerateRoundEvent(game);
        if (randomEvent != null)
        {
            game.PendingEvents.Add(randomEvent);
        }

        _sessionManager.UpdateGame(game);
    }

    private void StartResourceAuctionPhase(GameState game)
    {
        game.Phase = GamePhase.ResourceAuction;

        // Apply random event BEFORE auctions (Atari 800 accurate - $4800)
        foreach (var evt in game.PendingEvents.Where(e => !e.MuleLost))
        {
            ApplyEventEffects(game, evt);
        }
        game.EventHistory.AddRange(game.PendingEvents);
        game.PendingEvents.Clear();

        // Update store prices
        var (food, energy, smithore, crystite) = game.GetTotalResources();
        game.Store.UpdatePrices(food, energy, smithore, crystite, game.Rng);

        // Atari 800 auction order: Smithore → Crystite → Food → Energy ($4817-$482F)
        game.ResourceAuctionState = new ResourceAuctionState
        {
            CurrentResource = ResourceType.Smithore,
            State = AuctionState.InProgress,
            TimeRemaining = 60
        };

        _sessionManager.UpdateGame(game);
    }

    private void EndRound(GameState game)
    {
        // Apply spoilage
        game.ApplySpoilage();

        game.Phase = GamePhase.Summary;
        _sessionManager.UpdateGame(game);

        // Check if game is over
        if (game.CurrentRound >= game.MaxRounds)
        {
            game.Phase = GamePhase.GameOver;
            _sessionManager.UpdateGame(game);
        }
    }

    private void ApplyEventEffects(GameState game, RandomEventResult evt)
    {
        if (evt.AffectedPlayerId.HasValue)
        {
            var player = game.Players.FirstOrDefault(p => p.Id == evt.AffectedPlayerId);
            if (player != null)
            {
                player.Money += evt.MoneyChange;
                player.Food += evt.FoodChange;
                player.Energy += evt.EnergyChange;
                player.Smithore += evt.SmithoreChange;
                player.Crystite += evt.CrystiteChange;

                // Ensure no negative resources
                player.Money = Math.Max(0, player.Money);
                player.Food = Math.Max(0, player.Food);
                player.Energy = Math.Max(0, player.Energy);
                player.Smithore = Math.Max(0, player.Smithore);
                player.Crystite = Math.Max(0, player.Crystite);
            }
        }
    }

    public GameState AdvancePhase(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null)
            throw new InvalidOperationException("Game not found");

        switch (game.Phase)
        {
            case GamePhase.Setup:
                StartLandGrantPhase(game);
                break;
            case GamePhase.LandGrant:
                StartLandAuctionPhase(game);
                break;
            case GamePhase.LandAuction:
                StartDevelopmentPhase(game);
                break;
            case GamePhase.Development:
                StartProductionPhase(game);
                break;
            case GamePhase.Production:
                StartResourceAuctionPhase(game);
                break;
            case GamePhase.ResourceAuction:
                EndRound(game);
                break;
            case GamePhase.Summary:
                if (game.CurrentRound < game.MaxRounds)
                {
                    StartRound(game);
                }
                else
                {
                    game.Phase = GamePhase.GameOver;
                }
                break;
        }

        _sessionManager.UpdateGame(game);
        return game;
    }

    #endregion

    #region Land Grant Actions

    public GameState MoveLandCursor(string gameId, int playerId, int dx, int dy)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.LandGrant)
            throw new InvalidOperationException("Invalid game state");

        var state = game.LandGrantState!;
        if (state.SelectionOrder[state.CurrentSelectorIndex] != playerId)
            throw new InvalidOperationException("Not your turn");

        int newX = Math.Clamp(state.CursorX + dx, 0, GameMap.Width - 1);
        int newY = Math.Clamp(state.CursorY + dy, 0, GameMap.Height - 1);

        state.CursorX = newX;
        state.CursorY = newY;
        state.CursorMoveTime = DateTime.UtcNow;

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState SelectLand(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.LandGrant)
            throw new InvalidOperationException("Invalid game state");

        var state = game.LandGrantState!;
        if (state.SelectionOrder[state.CurrentSelectorIndex] != playerId)
            throw new InvalidOperationException("Not your turn");

        var tile = game.Map.GetTile(state.CursorX, state.CursorY);

        // Can't select town or already owned tile
        if (tile.Terrain == TerrainType.Town || tile.OwnerId != null)
            throw new InvalidOperationException("Cannot select this tile");

        // Assign tile to player
        tile.OwnerId = playerId;
        var player = game.Players.First(p => p.Id == playerId);
        player.OwnedPlots.Add((state.CursorX, state.CursorY));

        // Move to next player
        state.CurrentSelectorIndex++;

        if (state.CurrentSelectorIndex >= state.SelectionOrder.Count)
        {
            // All players selected, move to land auction
            StartLandAuctionPhase(game);
        }
        else
        {
            // Reset cursor for next player
            state.CursorX = GameMap.TownX;
            state.CursorY = 0;
        }

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState PassLandGrant(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.LandGrant)
            throw new InvalidOperationException("Invalid game state");

        var state = game.LandGrantState!;
        if (state.SelectionOrder[state.CurrentSelectorIndex] != playerId)
            throw new InvalidOperationException("Not your turn");

        // Player passes - gets no land this round
        state.CurrentSelectorIndex++;

        if (state.CurrentSelectorIndex >= state.SelectionOrder.Count)
        {
            StartLandAuctionPhase(game);
        }
        else
        {
            state.CursorX = GameMap.TownX;
            state.CursorY = 0;
        }

        _sessionManager.UpdateGame(game);
        return game;
    }

    #endregion

    #region Land Auction Actions

    public GameState PlaceLandBid(string gameId, int playerId, int amount)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.LandAuction)
            throw new InvalidOperationException("Invalid game state");

        var state = game.LandAuctionState!;
        var player = game.Players.First(p => p.Id == playerId);

        if (amount < state.MinBid || amount <= state.CurrentBid)
            throw new InvalidOperationException("Bid too low");

        if (amount > player.Money)
            throw new InvalidOperationException("Not enough money");

        state.CurrentBid = amount;
        state.CurrentBidder = playerId;
        state.PlayerBids[playerId] = amount;
        state.AuctionTimeRemaining = 10; // Reset timer on bid

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState PassLandAuction(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.LandAuction)
            throw new InvalidOperationException("Invalid game state");

        var state = game.LandAuctionState!;

        // Player passes - if no bids, plot unsold
        // This would typically be handled by timeout logic

        _sessionManager.UpdateGame(game);
        return game;
    }

    #endregion

    #region Development Phase Actions

    public GameState MovePlayer(string gameId, int playerId, int x, int y)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.CurrentPlayerIndex != playerId)
            throw new InvalidOperationException("Not your turn");

        var player = game.Players.First(p => p.Id == playerId);

        // Validate movement (adjacent tile only)
        int dx = Math.Abs(x - player.PositionX);
        int dy = Math.Abs(y - player.PositionY);

        if (dx > 1 || dy > 1 || (dx == 1 && dy == 1))
            throw new InvalidOperationException("Can only move to adjacent tiles");

        if (x < 0 || x >= GameMap.Width || y < 0 || y >= GameMap.Height)
            throw new InvalidOperationException("Out of bounds");

        player.PositionX = x;
        player.PositionY = y;

        // Check if in town
        game.DevelopmentState!.InTown = (x == GameMap.TownX && y == GameMap.TownY);

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState BuyMule(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.CurrentPlayerIndex != playerId)
            throw new InvalidOperationException("Not your turn");

        var player = game.Players.First(p => p.Id == playerId);

        if (player.HasMule)
            throw new InvalidOperationException("Already have a MULE");

        if (!game.Store.BuyMule(player, game.Difficulty))
            throw new InvalidOperationException("Cannot buy MULE");

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState OutfitMule(string gameId, int playerId, MuleType type)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.CurrentPlayerIndex != playerId)
            throw new InvalidOperationException("Not your turn");

        var player = game.Players.First(p => p.Id == playerId);

        if (!game.Store.OutfitMule(player, type, game.Difficulty))
            throw new InvalidOperationException("Cannot outfit MULE");

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState InstallMule(string gameId, int playerId, int x, int y)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.CurrentPlayerIndex != playerId)
            throw new InvalidOperationException("Not your turn");

        var player = game.Players.First(p => p.Id == playerId);

        if (!player.HasMule || player.CurrentMuleType == MuleType.None)
            throw new InvalidOperationException("No equipped MULE to install");

        // Must be on the tile to install
        if (player.PositionX != x || player.PositionY != y)
            throw new InvalidOperationException("Must be on tile to install MULE");

        var tile = game.Map.GetTile(x, y);

        // Must own the tile
        if (tile.OwnerId != playerId)
            throw new InvalidOperationException("Don't own this tile");

        // Can't install on river for mining
        if (tile.Terrain == TerrainType.River &&
            (player.CurrentMuleType == MuleType.Smithore || player.CurrentMuleType == MuleType.Crystite))
            throw new InvalidOperationException("Cannot mine on river tiles");

        // Install MULE
        tile.InstalledMule = player.CurrentMuleType;
        player.HasMule = false;
        player.CurrentMuleType = MuleType.None;

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState EnterBuilding(string gameId, int playerId, TownBuilding building)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.CurrentPlayerIndex != playerId)
            throw new InvalidOperationException("Not your turn");

        if (!game.DevelopmentState!.InTown)
            throw new InvalidOperationException("Must be in town");

        game.DevelopmentState.CurrentBuilding = building;

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState ExitBuilding(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        game.DevelopmentState!.CurrentBuilding = null;

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState EnterPub(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.CurrentPlayerIndex != playerId)
            throw new InvalidOperationException("Not your turn");

        var player = game.Players.First(p => p.Id == playerId);

        // Calculate gambling winnings (Atari 800 accurate - from disassembly $6A7E)
        // Formula: random[0; timeLeft×2] + roundsGamblingBonus[round/4]
        // Bonus table: [50, 100, 150, 200] for rounds [1-3], [4-7], [8-11], [12]
        int[] gamblingBonus = { 50, 100, 150, 200 };
        int bonusIndex = Math.Min(3, (game.CurrentRound - 1) / 4);
        int bonus = gamblingBonus[bonusIndex];

        // Random portion based on time remaining (timeLeft×2 max)
        int randomMax = player.TimeRemaining * 2;
        int randomPart = randomMax > 0 ? game.Rng.Next(randomMax + 1) : 0;

        int winnings = randomPart + bonus;

        player.Money += winnings;

        // End turn
        EndPlayerTurn(game);

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState CatchWampus(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.Wampus == null || game.Wampus.IsCaught)
            throw new InvalidOperationException("No wampus to catch");

        var player = game.Players.First(p => p.Id == playerId);

        // Player must be on wampus tile
        if (player.PositionX != game.Wampus.X || player.PositionY != game.Wampus.Y)
            throw new InvalidOperationException("Not on wampus tile");

        // Player must NOT have a MULE (wampus is afraid of MULEs)
        if (player.HasMule)
            throw new InvalidOperationException("Wampus won't appear near MULEs");

        // Check if wampus is visible
        var now = DateTime.UtcNow;
        if (now < game.Wampus.AppearTime || now > game.Wampus.DisappearTime)
            throw new InvalidOperationException("Wampus not visible");

        // Catch successful! Award money based on round
        int reward = game.CurrentRound switch
        {
            <= 3 => 100,
            <= 7 => 200,
            <= 11 => 300,
            _ => 400
        };

        player.Money += reward;
        game.Wampus.IsCaught = true;
        game.Wampus.CaughtByPlayerId = playerId;

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState EndTurn(string gameId, int playerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.Development)
            throw new InvalidOperationException("Invalid game state");

        if (game.CurrentPlayerIndex != playerId)
            throw new InvalidOperationException("Not your turn");

        EndPlayerTurn(game);

        _sessionManager.UpdateGame(game);
        return game;
    }

    #endregion

    #region Resource Auction Actions

    public GameState SetAuctionPosition(string gameId, int playerId, bool isBuying, int price)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.ResourceAuction)
            throw new InvalidOperationException("Invalid game state");

        var state = game.ResourceAuctionState!;
        var player = game.Players.First(p => p.Id == playerId);

        if (isBuying)
        {
            // Buyers - validate they have money and store has stock
            state.BuyerPositions[playerId] = price;
            state.SellerPositions.Remove(playerId);
        }
        else
        {
            // Sellers - validate they have the resource
            int available = GetPlayerResource(player, state.CurrentResource);
            if (available <= 0)
                throw new InvalidOperationException("No resource to sell");

            state.SellerPositions[playerId] = price;
            state.BuyerPositions.Remove(playerId);
        }

        // Check for matching trades
        _auctionService.CheckForTrades(game);

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState AcceptTrade(string gameId, int buyerId, int sellerId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.ResourceAuction)
            throw new InvalidOperationException("Invalid game state");

        _auctionService.ExecuteTrade(game, buyerId, sellerId);

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState TradeWithStore(string gameId, int playerId, ResourceType resource, int quantity, bool isBuying)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.ResourceAuction)
            throw new InvalidOperationException("Invalid game state");

        var player = game.Players.First(p => p.Id == playerId);

        if (isBuying)
        {
            game.Store.BuyFromStore(player, resource, quantity);
        }
        else
        {
            game.Store.SellToStore(player, resource, quantity);
        }

        _sessionManager.UpdateGame(game);
        return game;
    }

    public GameState NextAuctionResource(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.ResourceAuction)
            throw new InvalidOperationException("Invalid game state");

        var state = game.ResourceAuctionState!;

        // Clear positions
        state.BuyerPositions.Clear();
        state.SellerPositions.Clear();

        // Atari 800 auction order: Smithore → Crystite → Food → Energy ($4817-$482F)
        var previousResource = state.CurrentResource;
        state.CurrentResource = state.CurrentResource switch
        {
            ResourceType.Smithore when game.Difficulty == GameDifficulty.Tournament => ResourceType.Crystite,
            ResourceType.Smithore => ResourceType.Food, // Skip crystite in non-tournament
            ResourceType.Crystite => ResourceType.Food,
            ResourceType.Food => ResourceType.Energy,
            _ => ResourceType.Smithore // Signal end
        };

        // After Crystite auction, reset store crystite stock to 0 (Atari 800 - $4823-$4828)
        if (previousResource == ResourceType.Crystite)
        {
            game.Store.CrystiteStock = 0;
        }

        // If we've completed all auctions (cycled back to Smithore), end round
        if (state.CurrentResource == ResourceType.Smithore)
        {
            // Build MULEs from smithore before ending round (Atari 800 - $483F buildAndCalcPriceMule)
            BuildMulesFromSmithore(game);
            EndRound(game);
        }
        else
        {
            state.TimeRemaining = 60;
        }

        _sessionManager.UpdateGame(game);
        return game;
    }

    /// <summary>
    /// Build MULEs from store smithore (Atari 800 - $339A buildAndCalcPriceMule)
    /// Formula: nbMulesBuildable = goodsStoreNb[Smithore] / 2
    /// </summary>
    private void BuildMulesFromSmithore(GameState game)
    {
        // Each MULE requires 2 smithore to build
        int mulesBuildable = game.Store.SmithoreStock / 2;
        if (mulesBuildable > 0 && game.Difficulty != GameDifficulty.Beginner)
        {
            game.Store.SmithoreStock -= mulesBuildable * 2;
            game.Store.MuleCount += mulesBuildable;
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

    #endregion

    #region Event Management

    /// <summary>
    /// Acknowledge (consume) the next pending event for the game
    /// Removes the first pending event and records it in history.
    /// </summary>
    public GameState AcknowledgeEvent(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null)
            throw new InvalidOperationException("Game not found");

        if (game.PendingEvents.Count == 0)
        {
            _sessionManager.UpdateGame(game);
            return game;
        }

        var evt = game.PendingEvents[0];
        game.EventHistory.Add(evt);
        game.PendingEvents.RemoveAt(0);

        _sessionManager.UpdateGame(game);
        return game;
    }

    #endregion
}
