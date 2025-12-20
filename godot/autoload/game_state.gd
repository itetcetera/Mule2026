extends Node
## Local cache of game state received from server
## Autoloaded as GameState

signal phase_changed(old_phase: int, new_phase: int)
signal round_changed(round_num: int)
signal player_updated(player_id: int)
signal production_complete(results: Array)
signal event_occurred(event: Dictionary)

# Enums matching C# backend
enum Phase {
	SETUP = 0,
	LAND_GRANT = 1,
	LAND_AUCTION = 2,
	DEVELOPMENT = 3,
	PRODUCTION = 4,
	RESOURCE_AUCTION = 5,
	SUMMARY = 6,
	GAME_OVER = 7
}

enum Difficulty {
	BEGINNER = 0,
	STANDARD = 1,
	TOURNAMENT = 2
}

enum Species {
	HUMANOID = 0,
	FLAPPER = 1,
	PACKER = 2,
	GOLLUMER = 3,
	SPHEROID = 4,
	BONZOID = 5,
	LEGGITE = 6,
	MECHTRON = 7
}

enum ResourceType {
	FOOD = 0,
	ENERGY = 1,
	SMITHORE = 2,
	CRYSTITE = 3
}

enum MuleType {
	NONE = 0,
	FOOD = 1,
	ENERGY = 2,
	SMITHORE = 3,
	CRYSTITE = 4
}

enum Terrain {
	RIVER = 0,
	PLAINS = 1,
	MOUNTAINS1 = 2,
	MOUNTAINS2 = 3,
	MOUNTAINS3 = 4,
	TOWN = 5
}

# Current game state
var game_id: String = ""
var difficulty: int = Difficulty.STANDARD
var phase: int = Phase.SETUP
var current_round: int = 0
var max_rounds: int = 12

# Players
var players: Array = []
var current_player_index: int = 0
var local_player_id: int = -1

# Map (9x5 grid)
var map_tiles: Array = []  # 2D array of tile data

# Store
var store: Dictionary = {}

# Phase-specific state
var land_grant_state: Dictionary = {}
var land_auction_state: Dictionary = {}
var development_state: Dictionary = {}
var resource_auction_state: Dictionary = {}

# Events
var pending_events: Array = []

func _ready():
	GameServer.game_state_updated.connect(_on_game_state_updated)

func _on_game_state_updated(state: Dictionary):
	var old_phase = phase

	# Update core state
	if state.has("gameId"):
		game_id = state.gameId
	if state.has("difficulty"):
		difficulty = state.difficulty
	if state.has("phase"):
		phase = state.phase
	if state.has("currentRound"):
		var old_round = current_round
		current_round = state.currentRound
		if current_round != old_round:
			round_changed.emit(current_round)
	if state.has("maxRounds"):
		max_rounds = state.maxRounds

	# Update players
	if state.has("players"):
		players = state.players

	# Update map
	if state.has("map") and state.map.has("tiles"):
		map_tiles = state.map.tiles

	# Update store
	if state.has("store"):
		store = state.store

	# Update phase-specific state
	if state.has("landGrantState"):
		land_grant_state = state.landGrantState if state.landGrantState else {}
	if state.has("landAuctionState"):
		land_auction_state = state.landAuctionState if state.landAuctionState else {}
	if state.has("developmentState"):
		development_state = state.developmentState if state.developmentState else {}
	if state.has("resourceAuctionState"):
		resource_auction_state = state.resourceAuctionState if state.resourceAuctionState else {}

	# Handle events
	if state.has("pendingEvents"):
		for event in state.pendingEvents:
			if not pending_events.has(event):
				pending_events.append(event)
				event_occurred.emit(event)

	# Emit phase change
	if phase != old_phase:
		phase_changed.emit(old_phase, phase)

## Get the current player whose turn it is
func get_current_player() -> Dictionary:
	if current_player_index >= 0 and current_player_index < players.size():
		return players[current_player_index]
	return {}

## Get local player data
func get_local_player() -> Dictionary:
	for player in players:
		if player.id == local_player_id:
			return player
	return {}

## Check if it's the local player's turn
func is_my_turn() -> bool:
	var current = get_current_player()
	return current.has("id") and current.id == local_player_id

## Get tile at position
func get_tile(x: int, y: int) -> Dictionary:
	if y >= 0 and y < map_tiles.size():
		var row = map_tiles[y]
		if x >= 0 and x < row.size():
			return row[x]
	return {}

## Get player by ID
func get_player(id: int) -> Dictionary:
	for player in players:
		if player.id == id:
			return player
	return {}

## Get phase name for display
func get_phase_name() -> String:
	match phase:
		Phase.SETUP: return "Setup"
		Phase.LAND_GRANT: return "Land Grant"
		Phase.LAND_AUCTION: return "Land Auction"
		Phase.DEVELOPMENT: return "Development"
		Phase.PRODUCTION: return "Production"
		Phase.RESOURCE_AUCTION: return "Resource Auction"
		Phase.SUMMARY: return "Summary"
		Phase.GAME_OVER: return "Game Over"
	return "Unknown"

## Get resource name
static func get_resource_name(resource: int) -> String:
	match resource:
		ResourceType.FOOD: return "Food"
		ResourceType.ENERGY: return "Energy"
		ResourceType.SMITHORE: return "Smithore"
		ResourceType.CRYSTITE: return "Crystite"
	return "Unknown"

## Get species name
static func get_species_name(species: int) -> String:
	match species:
		Species.HUMANOID: return "Humanoid"
		Species.FLAPPER: return "Flapper"
		Species.PACKER: return "Packer"
		Species.GOLLUMER: return "Gollumer"
		Species.SPHEROID: return "Spheroid"
		Species.BONZOID: return "Bonzoid"
		Species.LEGGITE: return "Leggite"
		Species.MECHTRON: return "Mechtron"
	return "Unknown"
