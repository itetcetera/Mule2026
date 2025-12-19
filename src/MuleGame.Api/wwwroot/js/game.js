/**
 * M.U.L.E. Web - Frontend Game Client
 * Authentic Atari 800 M.U.L.E. experience in the browser
 */

// API Base URL
const API_BASE = '/api/game';

// Game State
let gameState = null;
let currentPlayerId = 0;
let selectedDifficulty = 0;
let pollInterval = null;

// Species data
const SPECIES = {
    0: { name: 'HUMANOID', startMoney: 600, turnTime: 35 },
    1: { name: 'FLAPPER', startMoney: 1600, turnTime: 60 },
    2: { name: 'PACKER', startMoney: 1000, turnTime: 45 },
    3: { name: 'GOLLUMER', startMoney: 1000, turnTime: 45 },
    4: { name: 'SPHEROID', startMoney: 1000, turnTime: 45 },
    5: { name: 'BONZOID', startMoney: 1000, turnTime: 45 },
    6: { name: 'LEGGITE', startMoney: 1000, turnTime: 45 },
    7: { name: 'MECHTRON', startMoney: 1000, turnTime: 45 }
};

// Terrain types
const TERRAIN = {
    0: 'river',
    1: 'plains',
    2: 'mountains1',
    3: 'mountains2',
    4: 'mountains3',
    5: 'town'
};

// MULE types
const MULE_TYPES = {
    0: 'none',
    1: 'food',
    2: 'energy',
    3: 'smithore',
    4: 'crystite'
};

// Phase names
const PHASES = {
    0: 'SETUP',
    1: 'LAND GRANT',
    2: 'LAND AUCTION',
    3: 'DEVELOPMENT',
    4: 'PRODUCTION',
    5: 'RESOURCE AUCTION',
    6: 'SUMMARY',
    7: 'GAME OVER'
};

// Resource names
const RESOURCES = {
    0: 'FOOD',
    1: 'ENERGY',
    2: 'SMITHORE',
    3: 'CRYSTITE'
};

// DOM Elements
const screens = {
    title: document.getElementById('title-screen'),
    setup: document.getElementById('setup-screen'),
    game: document.getElementById('game-screen'),
    gameover: document.getElementById('gameover-screen')
};

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    initEventListeners();
    showScreen('title');
});

function initEventListeners() {
    // Title screen
    document.getElementById('btn-new-game').addEventListener('click', () => showScreen('setup'));
    document.getElementById('btn-how-to-play').addEventListener('click', showHowToPlay);

    // Setup screen
    document.getElementById('btn-back-to-title').addEventListener('click', () => showScreen('title'));
    document.getElementById('btn-start-game').addEventListener('click', startGame);

    // Difficulty buttons
    document.querySelectorAll('.difficulty-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            document.querySelectorAll('.difficulty-btn').forEach(b => b.classList.remove('active'));
            e.target.classList.add('active');
            selectedDifficulty = parseInt(e.target.dataset.difficulty);
            updateCrystiteVisibility();
        });
    });

    // Game over
    document.getElementById('btn-play-again').addEventListener('click', () => showScreen('title'));

    // Event popup
    document.getElementById('event-ok').addEventListener('click', closeEventPopup);

    // Store panel
    document.getElementById('close-store').addEventListener('click', () => {
        document.getElementById('store-panel').classList.add('hidden');
    });

    // Keyboard controls
    document.addEventListener('keydown', handleKeyDown);

    // Generate player setup slots
    generatePlayerSetup();
}

function showScreen(screenName) {
    Object.values(screens).forEach(s => s.classList.remove('active'));
    screens[screenName].classList.add('active');
}

function generatePlayerSetup() {
    const container = document.getElementById('player-setup');
    container.innerHTML = '';

    for (let i = 0; i < 4; i++) {
        const slot = document.createElement('div');
        slot.className = `player-slot player${i + 1}`;
        slot.innerHTML = `
            <div class="slot-header">PLAYER ${i + 1}</div>
            <input type="text" class="player-name-input" placeholder="Name" value="${i === 0 ? 'PLAYER 1' : ''}">
            <select class="player-species-select">
                ${Object.entries(SPECIES).map(([id, s]) =>
                    `<option value="${id}" ${i === 0 && id === '1' ? 'selected' : ''}>${s.name}</option>`
                ).join('')}
            </select>
            <select class="player-type-select">
                <option value="0" ${i === 0 ? 'selected' : ''}>HUMAN</option>
                <option value="1" ${i !== 0 ? 'selected' : ''}>COMPUTER</option>
            </select>
        `;
        container.appendChild(slot);
    }
}

function updateCrystiteVisibility() {
    const crystiteBtn = document.querySelector('.outfit-btn[data-type="4"]');
    if (crystiteBtn) {
        crystiteBtn.classList.toggle('hidden', selectedDifficulty !== 2);
    }
}

async function startGame() {
    const players = [];
    const slots = document.querySelectorAll('.player-slot');

    slots.forEach((slot, index) => {
        const name = slot.querySelector('.player-name-input').value || `PLAYER ${index + 1}`;
        const species = parseInt(slot.querySelector('.player-species-select').value);
        const type = parseInt(slot.querySelector('.player-type-select').value);

        players.push({ name, species, type });
    });

    try {
        const response = await fetch(`${API_BASE}/create`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                difficulty: selectedDifficulty,
                players: players
            })
        });

        if (!response.ok) throw new Error('Failed to create game');

        gameState = await response.json();
        currentPlayerId = 0; // First player is human

        showScreen('game');
        renderGame();
        startPolling();

    } catch (error) {
        console.error('Error starting game:', error);
        alert('Failed to start game: ' + error.message);
    }
}

function startPolling() {
    if (pollInterval) clearInterval(pollInterval);
    pollInterval = setInterval(async () => {
        if (gameState) {
            await refreshGameState();
        }
    }, 1000);
}

async function refreshGameState() {
    try {
        const response = await fetch(`${API_BASE}/${gameState.gameId}`);
        if (response.ok) {
            gameState = await response.json();
            renderGame();
        }
    } catch (error) {
        console.error('Error refreshing game state:', error);
    }
}

function renderGame() {
    if (!gameState) return;

    renderHeader();
    renderMap();
    renderPlayerPanels();
    renderActionPanel();
    renderPhaseSpecificUI();
    checkForEvents();
    checkForGameOver();
}

function renderHeader() {
    document.getElementById('round-display').textContent =
        `ROUND ${gameState.currentRound}/${gameState.maxRounds}`;
    document.getElementById('phase-display').textContent =
        PHASES[gameState.phase] || 'UNKNOWN';
    document.getElementById('colony-score').textContent =
        `COLONY: $${gameState.colonyScore.toLocaleString()}`;
}

function renderMap() {
    const mapContainer = document.getElementById('game-map');
    mapContainer.innerHTML = '';

    gameState.map.tiles.forEach(tile => {
        const tileEl = document.createElement('div');
        tileEl.className = `map-tile ${TERRAIN[tile.terrain]}`;
        tileEl.dataset.x = tile.x;
        tileEl.dataset.y = tile.y;

        // Owner indicator
        if (tile.ownerId !== null) {
            const owner = document.createElement('div');
            owner.className = `tile-owner player${tile.ownerId}`;
            tileEl.appendChild(owner);
        }

        // MULE indicator
        if (tile.installedMule > 0) {
            const mule = document.createElement('div');
            mule.className = `tile-mule ${MULE_TYPES[tile.installedMule]}`;
            mule.textContent = MULE_TYPES[tile.installedMule][0].toUpperCase();
            tileEl.appendChild(mule);
        }

        // Quality dots (during development)
        if (gameState.phase === 3 && tile.terrain !== 5) {
            const quality = document.createElement('div');
            quality.className = 'tile-quality';
            quality.textContent = '•'.repeat(Math.max(tile.foodQuality, tile.energyQuality, tile.smithoreQuality));
            tileEl.appendChild(quality);
        }

        // Cursor for land grant
        if (gameState.phase === 1 && gameState.landGrantState) {
            if (tile.x === gameState.landGrantState.cursorX &&
                tile.y === gameState.landGrantState.cursorY) {
                tileEl.classList.add('cursor');
            }
        }

        // Click handler
        tileEl.addEventListener('click', () => handleTileClick(tile.x, tile.y));

        mapContainer.appendChild(tileEl);
    });

    // Render player markers during development
    if (gameState.phase === 3) {
        renderPlayerMarkers();
    }

    // Render wampus
    if (gameState.wampus && gameState.wampus.isVisible) {
        renderWampus();
    }
}

function renderPlayerMarkers() {
    gameState.players.forEach(player => {
        const tileIndex = player.positionY * 9 + player.positionX;
        const tiles = document.querySelectorAll('.map-tile');
        if (tiles[tileIndex]) {
            const marker = document.createElement('div');
            marker.className = `player-marker`;
            marker.style.backgroundColor = getPlayerColor(player.id);
            tiles[tileIndex].appendChild(marker);
        }
    });
}

function renderWampus() {
    const tileIndex = gameState.wampus.y * 9 + gameState.wampus.x;
    const tiles = document.querySelectorAll('.map-tile');
    if (tiles[tileIndex]) {
        const wampus = document.createElement('div');
        wampus.className = 'wampus';
        wampus.textContent = '👾';
        tiles[tileIndex].appendChild(wampus);
    }
}

function renderPlayerPanels() {
    const container = document.getElementById('player-panels');
    container.innerHTML = '';

    gameState.players.forEach(player => {
        const panel = document.createElement('div');
        panel.className = `player-panel player${player.id}`;
        if (gameState.currentPlayerIndex === player.id) {
            panel.classList.add('active');
        }

        panel.innerHTML = `
            <div class="player-name">${player.name}</div>
            <div class="player-resources">
                <div class="resource money">$${player.money}</div>
                <div class="resource">LAND: ${player.landCount}</div>
                <div class="resource food">F: ${player.food}</div>
                <div class="resource energy">E: ${player.energy}</div>
                <div class="resource smithore">S: ${player.smithore}</div>
                ${gameState.difficulty === 2 ? `<div class="resource crystite">C: ${player.crystite}</div>` : ''}
            </div>
            <div class="player-score">SCORE: $${player.score.toLocaleString()}</div>
            ${gameState.phase === 3 && gameState.currentPlayerIndex === player.id ?
                `<div class="time-bar">
                    <div class="time-bar-fill ${player.timeRemaining < 10 ? 'critical' : player.timeRemaining < 20 ? 'warning' : ''}"
                         style="width: ${(player.timeRemaining / SPECIES[player.species].turnTime) * 100}%"></div>
                </div>` : ''}
        `;

        container.appendChild(panel);
    });
}

function renderActionPanel() {
    const messageArea = document.getElementById('message-area');
    const buttonsArea = document.getElementById('action-buttons');

    // Generate context-sensitive message and buttons based on phase
    const currentPlayer = gameState.players[gameState.currentPlayerIndex];
    const isMyTurn = currentPlayer && currentPlayer.type === 0 && currentPlayer.id === currentPlayerId;

    switch (gameState.phase) {
        case 1: // Land Grant
            if (isMyTurn) {
                messageArea.textContent = 'Select a plot of land for your colony!';
                buttonsArea.innerHTML = `
                    <button class="action-btn" onclick="selectLand()">SELECT</button>
                    <button class="action-btn" onclick="passLandGrant()">PASS</button>
                `;
            } else {
                messageArea.textContent = `${currentPlayer?.name || 'Computer'} is selecting land...`;
                buttonsArea.innerHTML = '';
            }
            break;

        case 2: // Land Auction
            messageArea.textContent = 'Land Auction in progress...';
            buttonsArea.innerHTML = `
                <button class="action-btn" onclick="placeBid()">BID</button>
                <button class="action-btn" onclick="advancePhase()">SKIP</button>
            `;
            break;

        case 3: // Development
            if (isMyTurn) {
                const hasMule = currentPlayer.hasMule;
                messageArea.textContent = hasMule ?
                    `Take your M.U.L.E. to your land and install it!` :
                    `Buy a M.U.L.E. from the store or go to the pub.`;

                buttonsArea.innerHTML = `
                    <button class="action-btn" onclick="openStore()">STORE</button>
                    <button class="action-btn" onclick="enterPub()">PUB</button>
                    ${hasMule ? `<button class="action-btn" onclick="installMule()">INSTALL</button>` : ''}
                    <button class="action-btn" onclick="endTurn()">END TURN</button>
                `;
            } else {
                messageArea.textContent = `${currentPlayer?.name || 'Computer'}'s turn...`;
                buttonsArea.innerHTML = '';
            }
            break;

        case 4: // Production
            messageArea.textContent = 'Calculating production...';
            buttonsArea.innerHTML = `
                <button class="action-btn" onclick="advancePhase()">CONTINUE</button>
            `;
            break;

        case 5: // Resource Auction
            if (gameState.resourceAuctionState) {
                const resource = RESOURCES[gameState.resourceAuctionState.currentResource];
                messageArea.textContent = `${resource} AUCTION - Buy low, sell high!`;
                buttonsArea.innerHTML = `
                    <button class="action-btn" onclick="openAuction()">TRADE</button>
                    <button class="action-btn" onclick="nextAuctionResource()">NEXT</button>
                `;
            }
            break;

        case 6: // Summary
            messageArea.textContent = `Round ${gameState.currentRound} complete!`;
            buttonsArea.innerHTML = `
                <button class="action-btn" onclick="advancePhase()">NEXT ROUND</button>
            `;
            break;

        default:
            buttonsArea.innerHTML = '';
    }
}

function renderPhaseSpecificUI() {
    // Show/hide special panels based on phase
    const auctionPanel = document.getElementById('auction-panel');
    const storePanel = document.getElementById('store-panel');

    // Auto-show auction panel during resource auction
    if (gameState.phase === 5 && gameState.resourceAuctionState) {
        updateAuctionPanel();
    }

    // Update store if open
    if (!storePanel.classList.contains('hidden')) {
        updateStorePanel();
    }
}

function updateStorePanel() {
    document.getElementById('store-mules').textContent = gameState.store.muleCount;
    document.getElementById('mule-price').textContent = `$${gameState.store.mulePrice}`;
}

function updateAuctionPanel() {
    if (!gameState.resourceAuctionState) return;

    const resource = RESOURCES[gameState.resourceAuctionState.currentResource];
    document.getElementById('auction-title').textContent = `${resource} AUCTION`;

    const store = gameState.store;
    let price;
    switch (gameState.resourceAuctionState.currentResource) {
        case 0: price = store.foodPrice; break;
        case 1: price = store.energyPrice; break;
        case 2: price = store.smithorePrice; break;
        case 3: price = store.crystitePrice; break;
    }

    document.getElementById('store-buy-price').textContent = `$${price}`;
    document.getElementById('store-sell-price').textContent = `$${Math.floor(price * 0.75)}`;
}

function checkForEvents() {
    if (gameState.pendingEvents && gameState.pendingEvents.length > 0) {
        const event = gameState.pendingEvents[0];
        showEventPopup(event.message);
    }
}

function showEventPopup(message) {
    document.getElementById('event-message').textContent = message;
    document.getElementById('event-popup').classList.remove('hidden');
}

function closeEventPopup() {
    document.getElementById('event-popup').classList.add('hidden');
}

function checkForGameOver() {
    if (gameState.phase === 7) {
        showGameOver();
    }
}

function showGameOver() {
    showScreen('gameover');

    const title = document.getElementById('gameover-title');
    const result = document.getElementById('gameover-result');
    const scores = document.getElementById('final-scores');

    if (gameState.isColonySuccessful) {
        title.textContent = 'COLONY SUCCESSFUL!';
        title.className = 'win';
        result.textContent = 'The colony has thrived on Planet Irata!';
    } else {
        title.textContent = 'COLONY FAILED';
        title.className = 'lose';
        result.textContent = 'The colony did not meet minimum requirements...';
    }

    // Sort players by score
    const sortedPlayers = [...gameState.players].sort((a, b) => b.score - a.score);

    scores.innerHTML = sortedPlayers.map((player, index) => `
        <div class="final-score-row ${index === 0 ? 'winner' : ''}">
            <span>${index + 1}. ${player.name} (${SPECIES[player.species].name})</span>
            <span>$${player.score.toLocaleString()}</span>
        </div>
    `).join('');

    // Stop polling
    if (pollInterval) {
        clearInterval(pollInterval);
        pollInterval = null;
    }
}

// API Actions
async function apiPost(endpoint, data = {}) {
    try {
        const response = await fetch(`${API_BASE}/${gameState.gameId}${endpoint}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'API error');
        }

        gameState = await response.json();
        renderGame();
        return gameState;

    } catch (error) {
        console.error('API error:', error);
        showMessage(error.message);
    }
}

function showMessage(text) {
    document.getElementById('message-area').textContent = text;
}

// Game Actions
async function selectLand() {
    await apiPost('/land-grant/select', { playerId: currentPlayerId });
}

async function passLandGrant() {
    await apiPost('/land-grant/pass', { playerId: currentPlayerId });
}

async function advancePhase() {
    await apiPost('/advance');
}

async function placeBid() {
    const amount = prompt('Enter bid amount:');
    if (amount) {
        await apiPost('/land-auction/bid', {
            playerId: currentPlayerId,
            amount: parseInt(amount)
        });
    }
}

function openStore() {
    updateStorePanel();
    document.getElementById('store-panel').classList.remove('hidden');
}

async function buyMule() {
    document.getElementById('store-panel').classList.add('hidden');
    await apiPost('/development/buy-mule', { playerId: currentPlayerId });
}

async function outfitMule(type) {
    await apiPost('/development/outfit-mule', {
        playerId: currentPlayerId,
        muleType: type
    });
}

async function installMule() {
    const player = gameState.players[currentPlayerId];
    await apiPost('/development/install-mule', {
        playerId: currentPlayerId,
        x: player.positionX,
        y: player.positionY
    });
}

async function enterPub() {
    await apiPost('/development/enter-pub', { playerId: currentPlayerId });
}

async function endTurn() {
    await apiPost('/development/end-turn', { playerId: currentPlayerId });
}

async function catchWampus() {
    await apiPost('/development/catch-wampus', { playerId: currentPlayerId });
}

function openAuction() {
    document.getElementById('auction-panel').classList.remove('hidden');
    updateAuctionPanel();
}

async function nextAuctionResource() {
    document.getElementById('auction-panel').classList.add('hidden');
    await apiPost('/auction/next');
}

async function tradeWithStore(resource, quantity, isBuying) {
    await apiPost('/auction/store-trade', {
        playerId: currentPlayerId,
        resource: resource,
        quantity: quantity,
        isBuying: isBuying
    });
}

// Tile click handler
async function handleTileClick(x, y) {
    if (gameState.phase === 1) {
        // Land grant - move cursor
        const state = gameState.landGrantState;
        if (state) {
            const dx = x - state.cursorX;
            const dy = y - state.cursorY;
            if (Math.abs(dx) <= 1 && Math.abs(dy) <= 1) {
                await apiPost('/land-grant/move', {
                    playerId: currentPlayerId,
                    dx: dx,
                    dy: dy
                });
            }
        }
    } else if (gameState.phase === 3) {
        // Development - move player
        const player = gameState.players[currentPlayerId];
        if (player) {
            const dx = Math.abs(x - player.positionX);
            const dy = Math.abs(y - player.positionY);
            if (dx <= 1 && dy <= 1 && !(dx === 1 && dy === 1)) {
                await apiPost('/development/move', {
                    playerId: currentPlayerId,
                    x: x,
                    y: y
                });
            }
        }
    }
}

// Keyboard controls
function handleKeyDown(e) {
    if (!gameState) return;

    const currentPlayer = gameState.players[currentPlayerId];
    if (!currentPlayer || currentPlayer.type !== 0) return;

    let dx = 0, dy = 0;

    switch (e.key) {
        case 'ArrowUp': case 'w': case 'W': dy = -1; break;
        case 'ArrowDown': case 's': case 'S': dy = 1; break;
        case 'ArrowLeft': case 'a': case 'A': dx = -1; break;
        case 'ArrowRight': case 'd': case 'D': dx = 1; break;
        case 'Enter': case ' ':
            if (gameState.phase === 1) selectLand();
            else if (gameState.phase === 3 && currentPlayer.hasMule) installMule();
            return;
        case 'Escape':
            document.getElementById('store-panel').classList.add('hidden');
            document.getElementById('auction-panel').classList.add('hidden');
            closeEventPopup();
            return;
        default: return;
    }

    if (dx !== 0 || dy !== 0) {
        e.preventDefault();

        if (gameState.phase === 1) {
            apiPost('/land-grant/move', { playerId: currentPlayerId, dx, dy });
        } else if (gameState.phase === 3) {
            const newX = currentPlayer.positionX + dx;
            const newY = currentPlayer.positionY + dy;
            if (newX >= 0 && newX < 9 && newY >= 0 && newY < 5) {
                apiPost('/development/move', { playerId: currentPlayerId, x: newX, y: newY });
            }
        }
    }
}

// Store button handlers
document.addEventListener('click', (e) => {
    if (e.target.matches('[data-action="buy-mule"]')) {
        buyMule();
    }
    if (e.target.matches('.outfit-btn')) {
        const type = parseInt(e.target.dataset.type);
        outfitMule(type);
    }
});

// Utility functions
function getPlayerColor(playerId) {
    const colors = ['#ff5555', '#5555ff', '#55ff55', '#ffff55'];
    return colors[playerId] || '#ffffff';
}

function showHowToPlay() {
    alert(`M.U.L.E. - HOW TO PLAY

OBJECTIVE:
Colonize Planet Irata by developing land, producing resources, and trading with other players.

PHASES:
1. LAND GRANT - Select a free plot of land
2. LAND AUCTION - Bid on additional land
3. DEVELOPMENT - Buy M.U.L.E.s, outfit them, and install on your land
4. PRODUCTION - Collect resources from your plots
5. AUCTION - Trade resources with other players

RESOURCES:
- FOOD: Needed for longer turns
- ENERGY: Powers your M.U.L.E.s
- SMITHORE: Used to make M.U.L.E.s
- CRYSTITE: High-value mineral (Tournament only)

CONTROLS:
- Arrow keys/WASD: Move cursor/player
- Enter/Space: Select/Confirm
- Escape: Close popups

TIPS:
- Rivers produce the most food
- Mountains produce the most smithore
- Adjacent plots with same production get bonuses
- Go to the pub to gamble for money
- Catch the Wampus for bonus cash!

Good luck, colonist!`);
}
