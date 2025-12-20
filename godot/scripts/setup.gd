extends Control
## Game setup screen - configure players and difficulty

signal game_started
signal back_pressed

@onready var difficulty_option = $VBoxContainer/DifficultyOption
@onready var server_input = $VBoxContainer/ServerInput
@onready var start_button = $VBoxContainer/ButtonContainer/StartButton
@onready var back_button = $VBoxContainer/ButtonContainer/BackButton

var player_rows: Array = []

func _ready():
	# Setup difficulty options
	difficulty_option.add_item("Beginner (6 rounds)", GameState.Difficulty.BEGINNER)
	difficulty_option.add_item("Standard (12 rounds)", GameState.Difficulty.STANDARD)
	difficulty_option.add_item("Tournament (12 rounds + Crystite)", GameState.Difficulty.TOURNAMENT)
	difficulty_option.select(1)  # Default to Standard

	# Setup player rows
	for i in range(4):
		var row = $VBoxContainer/PlayerList.get_child(i)
		player_rows.append(row)
		_setup_player_row(row, i)

	# Connect buttons
	start_button.pressed.connect(_on_start_pressed)
	back_button.pressed.connect(_on_back_pressed)

	# Connect server response
	GameServer.game_state_updated.connect(_on_game_created)
	GameServer.error.connect(_on_error)

func _setup_player_row(row: HBoxContainer, index: int):
	var species_option = row.get_node("Species")
	var type_option = row.get_node("Type")

	# Add species options
	species_option.add_item("Humanoid", GameState.Species.HUMANOID)
	species_option.add_item("Flapper", GameState.Species.FLAPPER)
	species_option.add_item("Packer", GameState.Species.PACKER)
	species_option.add_item("Gollumer", GameState.Species.GOLLUMER)
	species_option.add_item("Spheroid", GameState.Species.SPHEROID)
	species_option.add_item("Bonzoid", GameState.Species.BONZOID)
	species_option.add_item("Leggite", GameState.Species.LEGGITE)
	species_option.add_item("Mechtron", GameState.Species.MECHTRON)

	# Select different default species for each player
	species_option.select(index % 8)

	# Add type options
	type_option.add_item("Human", 0)
	type_option.add_item("Computer", 1)

	# First player is human, rest are computer by default
	type_option.select(0 if index == 0 else 1)

func _on_start_pressed():
	var difficulty = difficulty_option.get_selected_id()

	# Build players array
	var players = []
	for i in range(4):
		var row = player_rows[i]
		var player = {
			"name": row.get_node("Name").text,
			"species": row.get_node("Species").get_selected_id(),
			"type": row.get_node("Type").get_selected_id(),
			"connectionId": ""  # Will be assigned by server
		}
		players.append(player)

	# Set server URL
	GameServer.set_server(server_input.text)

	# Mark player 0 as the local player
	GameState.local_player_id = 0

	# Create game
	start_button.disabled = true
	start_button.text = "Creating..."
	GameServer.create_game(difficulty, players)

func _on_game_created(state: Dictionary):
	start_button.disabled = false
	start_button.text = "Start Game"

	if state.has("gameId"):
		GameServer.game_id = state.gameId
		GameServer.player_id = 0  # Local player
		game_started.emit()

func _on_error(message: String):
	start_button.disabled = false
	start_button.text = "Start Game"
	push_error("Failed to create game: %s" % message)
	# TODO: Show error dialog

func _on_back_pressed():
	back_pressed.emit()
