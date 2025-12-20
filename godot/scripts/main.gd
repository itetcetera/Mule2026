extends Control
## Main game controller - handles scene switching and game flow

@onready var title_screen = $TitleScreen
@onready var game_container = $GameContainer
@onready var phase_label = $StatusBar/HBoxContainer/PhaseLabel
@onready var round_label = $StatusBar/HBoxContainer/RoundLabel
@onready var connection_label = $StatusBar/HBoxContainer/ConnectionLabel

var map_scene: PackedScene = preload("res://scenes/game_map.tscn")
var setup_scene: PackedScene = preload("res://scenes/setup.tscn")
var auction_scene: PackedScene = preload("res://scenes/auction.tscn")

var current_game_scene: Node = null

func _ready():
	# Connect UI buttons
	$TitleScreen/VBoxContainer/NewGameButton.pressed.connect(_on_new_game)
	$TitleScreen/VBoxContainer/JoinGameButton.pressed.connect(_on_join_game)
	$TitleScreen/VBoxContainer/SettingsButton.pressed.connect(_on_settings)
	$TitleScreen/VBoxContainer/QuitButton.pressed.connect(_on_quit)

	# Connect game state signals
	GameState.phase_changed.connect(_on_phase_changed)
	GameState.round_changed.connect(_on_round_changed)
	GameServer.connected.connect(_on_connected)
	GameServer.disconnected.connect(_on_disconnected)
	GameServer.error.connect(_on_server_error)

func _on_new_game():
	# Show setup screen
	_show_setup_screen()

func _on_join_game():
	# TODO: Show join game dialog
	pass

func _on_settings():
	# TODO: Show settings
	pass

func _on_quit():
	get_tree().quit()

func _show_setup_screen():
	title_screen.hide()
	game_container.show()

	if current_game_scene:
		current_game_scene.queue_free()

	current_game_scene = setup_scene.instantiate()
	game_container.add_child(current_game_scene)
	current_game_scene.game_started.connect(_on_game_started)

func _on_game_started():
	# Switch to map view
	_switch_to_phase_scene(GameState.phase)
	GameServer.start_polling()

func _on_phase_changed(old_phase: int, new_phase: int):
	_switch_to_phase_scene(new_phase)
	phase_label.text = "Phase: %s" % GameState.get_phase_name()

func _on_round_changed(round_num: int):
	round_label.text = "Round: %d/%d" % [round_num, GameState.max_rounds]

func _switch_to_phase_scene(phase: int):
	if current_game_scene:
		current_game_scene.queue_free()
		current_game_scene = null

	match phase:
		GameState.Phase.SETUP:
			current_game_scene = setup_scene.instantiate()
		GameState.Phase.LAND_GRANT, GameState.Phase.DEVELOPMENT, GameState.Phase.PRODUCTION:
			current_game_scene = map_scene.instantiate()
		GameState.Phase.LAND_AUCTION, GameState.Phase.RESOURCE_AUCTION:
			current_game_scene = auction_scene.instantiate()
		GameState.Phase.SUMMARY, GameState.Phase.GAME_OVER:
			# TODO: Summary scene
			current_game_scene = map_scene.instantiate()

	if current_game_scene:
		game_container.add_child(current_game_scene)

func _on_connected():
	connection_label.text = "Connected"
	connection_label.modulate = Color.GREEN

func _on_disconnected():
	connection_label.text = "Disconnected"
	connection_label.modulate = Color.RED

func _on_server_error(message: String):
	push_error("Server error: %s" % message)
	# TODO: Show error dialog
