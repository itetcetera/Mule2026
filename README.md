# M.U.L.E. Web - Faithful Atari 800 Recreation

A complete web-based recreation of the classic 1983 Atari 800 game **M.U.L.E.** (Multiple Use Labor Element) by Ozark Softscape / Electronic Arts.

## 🎮 About M.U.L.E.

M.U.L.E. is a pioneering multiplayer strategy game where up to 4 players compete to colonize the planet Irata. Players must develop land, produce resources, and trade with each other to build the most successful colony.

This implementation faithfully recreates the original Atari 800 experience, including:

- ✅ All 8 species (Humanoid, Flapper, Packer, Gollumer, Spheroid, Bonzoid, Leggite, Mechtron)
- ✅ 3 difficulty levels (Beginner, Standard, Tournament)
- ✅ Complete game phases (Land Grant, Auction, Development, Production, Trading)
- ✅ Accurate production formulas and adjacency bonuses
- ✅ Random events (Wampus, sunspots, planetquakes, pirates, etc.)
- ✅ AI computer players
- ✅ Authentic retro Atari 800 styling

## 🛠️ Technology Stack

- **Backend**: C# / .NET 8 Web API
- **Frontend**: HTML5 / CSS3 / Vanilla JavaScript
- **Real-time Updates**: SignalR (optional)
- **Architecture**: Server-centric state management

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the Game

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd Mule2026
   ```

2. Build and run:
   ```bash
   cd src/MuleGame.Api
   dotnet run
   ```

3. Open your browser to `http://localhost:5000`

### Development

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run with hot reload
dotnet watch run
```

## 🎲 Game Rules

### Species

| Species | Starting Money | Turn Time | Recommended For |
|---------|---------------|-----------|-----------------|
| Humanoid | $600 | 35 sec | Expert |
| Flapper | $1,600 | 60 sec | Beginner |
| Others | $1,000 | 45 sec | Standard |

### Resources

- **Food**: Needed for longer turns. Rivers produce the most.
- **Energy**: Powers your M.U.L.E.s. Plains produce the most.
- **Smithore**: Used to make M.U.L.E.s. Mountains produce the most.
- **Crystite**: High-value mineral (Tournament mode only).

### Production Bonuses

- **Adjacency**: +1 for each adjacent plot producing the same resource
- **Group**: +1 for every 3 plots producing the same resource

### Random Events

Good events (never happen to the leader):
- Benefactor donation
- Museum buys artifact
- Lost M.U.L.E. returns

Bad events (never happen to last place):
- M.U.L.E. runs away
- Pest attack
- Catbug eats food

Colony events (affect everyone):
- Sunspots (boost energy)
- Acid rain (boost food, reduce energy)
- Planetquake (reduce mining)
- Fire in store
- Pirate raid

### The Wampus

A native creature that appears on mountain tiles during your turn. Catch it for bonus money:
- Rounds 1-3: $100
- Rounds 4-7: $200
- Rounds 8-11: $300
- Round 12: $400

## 🏗️ Architecture

```
src/MuleGame.Api/
├── Controllers/          # REST API endpoints
│   └── GameController.cs
├── Models/               # Game domain models
│   ├── Enums.cs
│   ├── GameMap.cs
│   ├── GameState.cs
│   ├── MapTile.cs
│   ├── Player.cs
│   └── Store.cs
├── Services/             # Business logic
│   ├── GameService.cs    # Main game state machine
│   ├── ProductionService.cs
│   ├── AuctionService.cs
│   ├── RandomEventService.cs
│   └── AIService.cs
├── Hubs/                 # SignalR real-time
│   └── GameHub.cs
└── wwwroot/              # Frontend assets
    ├── index.html
    ├── css/style.css
    └── js/game.js
```

## 📡 API Reference

### Game Lifecycle

- `POST /api/game/create` - Create new game
- `GET /api/game/{gameId}` - Get game state
- `POST /api/game/{gameId}/advance` - Advance to next phase

### Land Grant

- `POST /api/game/{gameId}/land-grant/move` - Move cursor
- `POST /api/game/{gameId}/land-grant/select` - Select land
- `POST /api/game/{gameId}/land-grant/pass` - Pass selection

### Development

- `POST /api/game/{gameId}/development/move` - Move player
- `POST /api/game/{gameId}/development/buy-mule` - Buy M.U.L.E.
- `POST /api/game/{gameId}/development/outfit-mule` - Outfit M.U.L.E.
- `POST /api/game/{gameId}/development/install-mule` - Install M.U.L.E.
- `POST /api/game/{gameId}/development/enter-pub` - Enter pub
- `POST /api/game/{gameId}/development/end-turn` - End turn

### Auction

- `POST /api/game/{gameId}/auction/position` - Set buy/sell position
- `POST /api/game/{gameId}/auction/store-trade` - Trade with store
- `POST /api/game/{gameId}/auction/next` - Next resource

## 🎨 Original Game Credits

- **Design**: Danielle Bunten Berry
- **Development**: Ozark Softscape
- **Publisher**: Electronic Arts
- **Year**: 1983

## 📜 References

- [M.U.L.E. Reverse Engineering](http://bringerp.free.fr/RE/Mule/reverseEngineering.php5)
- [M.U.L.E. on Wikipedia](https://en.wikipedia.org/wiki/M.U.L.E.)
- [StrategyWiki M.U.L.E. Guide](https://strategywiki.org/wiki/M.U.L.E.)
- [Original Game Manual](https://archive.org/details/Mule_atari8)

## 📄 License

This is a fan recreation for educational purposes. M.U.L.E. is a trademark of Electronic Arts.

---

*"In Irata, everyone prospers together... or fails together."*
