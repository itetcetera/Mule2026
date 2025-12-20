extends Control
## Main game map display - 9x5 grid of Irata

const TILE_WIDTH = 100
const TILE_HEIGHT = 90
const MAP_WIDTH = 9
const MAP_HEIGHT = 5
const TOWN_X = 4
const TOWN_Y = 2

# Terrain colors (matching original Atari palette roughly)
const TERRAIN_COLORS = {
	GameState.Terrain.RIVER: Color(0.2, 0.4, 0.8),      # Blue
	GameState.Terrain.PLAINS: Color(0.4, 0.6, 0.3),     # Green
	GameState.Terrain.MOUNTAINS1: Color(0.5, 0.4, 0.3), # Brown (1 peak)
	GameState.Terrain.MOUNTAINS2: Color(0.6, 0.5, 0.4), # Brown (2 peaks)
	GameState.Terrain.MOUNTAINS3: Color(0.7, 0.6, 0.5), # Brown (3 peaks)
	GameState.Terrain.TOWN: Color(0.3, 0.3, 0.3),       # Gray
}

# Player colors
const PLAYER_COLORS = [
	Color(1.0, 0.2, 0.2),  # Red
	Color(0.2, 0.6, 1.0),  # Blue
	Color(0.2, 0.8, 0.2),  # Green
	Color(1.0, 0.8, 0.2),  # Yellow
]

@onready var tile_grid = $MapContainer/TileGrid
@onready var cursor = $MapContainer/Cursor
@onready var players_container = $MapContainer/PlayersContainer
@onready var phase_instructions = $PhaseInstructions
@onready var timer_label = $Timer
@onready var player_panels = [
	$PlayerInfoPanel/HBoxContainer/Player1,
	$PlayerInfoPanel/HBoxContainer/Player2,
	$PlayerInfoPanel/HBoxContainer/Player3,
	$PlayerInfoPanel/HBoxContainer/Player4,
]

var tiles: Array = []  # 2D array of tile nodes
var player_sprites: Dictionary = {}  # player_id -> sprite node
var cursor_pos: Vector2i = Vector2i(TOWN_X, 0)
var move_cooldown: float = 0.0

func _ready():
	_create_tile_grid()
	_update_from_state()

	# Connect to state updates
	GameServer.game_state_updated.connect(_on_state_updated)
	GameState.phase_changed.connect(_on_phase_changed)

func _create_tile_grid():
	# Clear existing
	for child in tile_grid.get_children():
		child.queue_free()
	tiles.clear()

	# Create 9x5 grid of tiles
	for y in range(MAP_HEIGHT):
		var row = []
		for x in range(MAP_WIDTH):
			var tile = _create_tile(x, y)
			tile_grid.add_child(tile)
			row.append(tile)
		tiles.append(row)

func _create_tile(x: int, y: int) -> Control:
	var tile = Panel.new()
	tile.custom_minimum_size = Vector2(TILE_WIDTH, TILE_HEIGHT)
	tile.name = "Tile_%d_%d" % [x, y]

	# Background color based on terrain (default to plains)
	var terrain = _get_default_terrain(x, y)
	var style = StyleBoxFlat.new()
	style.bg_color = TERRAIN_COLORS.get(terrain, Color.GRAY)
	style.border_width_bottom = 1
	style.border_width_right = 1
	style.border_color = Color(0.2, 0.2, 0.2)
	tile.add_theme_stylebox_override("panel", style)

	# Add terrain indicator label
	var label = Label.new()
	label.name = "TerrainLabel"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.text = _get_terrain_symbol(terrain)
	label.add_theme_font_size_override("font_size", 24)
	label.set_anchors_preset(Control.PRESET_FULL_RECT)
	tile.add_child(label)

	# Owner indicator
	var owner_indicator = ColorRect.new()
	owner_indicator.name = "OwnerIndicator"
	owner_indicator.custom_minimum_size = Vector2(20, 20)
	owner_indicator.position = Vector2(5, 5)
	owner_indicator.size = Vector2(20, 20)
	owner_indicator.visible = false
	tile.add_child(owner_indicator)

	# MULE indicator
	var mule_label = Label.new()
	mule_label.name = "MuleLabel"
	mule_label.position = Vector2(5, TILE_HEIGHT - 25)
	mule_label.add_theme_font_size_override("font_size", 12)
	mule_label.visible = false
	tile.add_child(mule_label)

	return tile

func _get_default_terrain(x: int, y: int) -> int:
	# River in center column
	if x == TOWN_X:
		if y == TOWN_Y:
			return GameState.Terrain.TOWN
		return GameState.Terrain.RIVER
	# Random mountains on edges (will be overwritten by server data)
	if x <= 1 or x >= 7:
		return GameState.Terrain.MOUNTAINS1
	return GameState.Terrain.PLAINS

func _get_terrain_symbol(terrain: int) -> String:
	match terrain:
		GameState.Terrain.RIVER: return "~"
		GameState.Terrain.PLAINS: return ""
		GameState.Terrain.MOUNTAINS1: return "^"
		GameState.Terrain.MOUNTAINS2: return "^^"
		GameState.Terrain.MOUNTAINS3: return "^^^"
		GameState.Terrain.TOWN: return "TOWN"
	return ""

func _process(delta):
	move_cooldown = max(0, move_cooldown - delta)

	# Handle input based on phase
	match GameState.phase:
		GameState.Phase.LAND_GRANT:
			_handle_land_grant_input()
		GameState.Phase.DEVELOPMENT:
			_handle_development_input()

func _handle_land_grant_input():
	if not GameState.is_my_turn():
		return

	if move_cooldown > 0:
		return

	var moved = false
	if Input.is_action_pressed("move_up") and cursor_pos.y > 0:
		cursor_pos.y -= 1
		moved = true
	elif Input.is_action_pressed("move_down") and cursor_pos.y < MAP_HEIGHT - 1:
		cursor_pos.y += 1
		moved = true
	elif Input.is_action_pressed("move_left") and cursor_pos.x > 0:
		cursor_pos.x -= 1
		moved = true
	elif Input.is_action_pressed("move_right") and cursor_pos.x < MAP_WIDTH - 1:
		cursor_pos.x += 1
		moved = true

	if moved:
		move_cooldown = 0.15
		GameServer.move_cursor(cursor_pos.x - _get_cursor_x(), cursor_pos.y - _get_cursor_y())
		_update_cursor_position()

	if Input.is_action_just_pressed("action"):
		GameServer.select_land()

	if Input.is_action_just_pressed("cancel"):
		GameServer.pass_land_grant()

func _handle_development_input():
	if not GameState.is_my_turn():
		return

	# Get current player position
	var player = GameState.get_local_player()
	if player.is_empty():
		return

	var pos_x = player.get("positionX", TOWN_X)
	var pos_y = player.get("positionY", TOWN_Y)

	if move_cooldown > 0:
		return

	var new_x = pos_x
	var new_y = pos_y

	if Input.is_action_pressed("move_up") and pos_y > 0:
		new_y -= 1
	elif Input.is_action_pressed("move_down") and pos_y < MAP_HEIGHT - 1:
		new_y += 1
	elif Input.is_action_pressed("move_left") and pos_x > 0:
		new_x -= 1
	elif Input.is_action_pressed("move_right") and pos_x < MAP_WIDTH - 1:
		new_x += 1

	if new_x != pos_x or new_y != pos_y:
		move_cooldown = 0.15
		GameServer.move_player(new_x, new_y)

	# Action button - context sensitive
	if Input.is_action_just_pressed("action"):
		if pos_x == TOWN_X and pos_y == TOWN_Y:
			# In town - could open menu or enter pub
			pass
		else:
			# On a plot - try to install MULE if holding one
			if player.get("hasMule", false):
				GameServer.install_mule(pos_x, pos_y)

func _get_cursor_x() -> int:
	if GameState.land_grant_state.has("cursorX"):
		return GameState.land_grant_state.cursorX
	return TOWN_X

func _get_cursor_y() -> int:
	if GameState.land_grant_state.has("cursorY"):
		return GameState.land_grant_state.cursorY
	return 0

func _update_cursor_position():
	var x = _get_cursor_x()
	var y = _get_cursor_y()
	cursor_pos = Vector2i(x, y)

	# Position cursor over the tile
	var grid_pos = tile_grid.global_position
	cursor.global_position = grid_pos + Vector2(x * TILE_WIDTH, y * TILE_HEIGHT)
	cursor.visible = GameState.phase == GameState.Phase.LAND_GRANT

func _on_state_updated(_state: Dictionary):
	_update_from_state()

func _on_phase_changed(_old: int, new: int):
	_update_phase_instructions(new)
	cursor.visible = new == GameState.Phase.LAND_GRANT

func _update_from_state():
	_update_tiles()
	_update_players()
	_update_player_info()
	_update_cursor_position()
	_update_phase_instructions(GameState.phase)

func _update_tiles():
	for y in range(min(MAP_HEIGHT, GameState.map_tiles.size())):
		var row = GameState.map_tiles[y]
		for x in range(min(MAP_WIDTH, row.size())):
			var tile_data = row[x]
			var tile_node = tiles[y][x]
			_update_tile_visuals(tile_node, tile_data)

func _update_tile_visuals(tile: Panel, data: Dictionary):
	var terrain = data.get("terrain", GameState.Terrain.PLAINS)

	# Update background color
	var style = tile.get_theme_stylebox("panel").duplicate()
	style.bg_color = TERRAIN_COLORS.get(terrain, Color.GRAY)
	tile.add_theme_stylebox_override("panel", style)

	# Update terrain label
	var label = tile.get_node("TerrainLabel")
	label.text = _get_terrain_symbol(terrain)

	# Update owner indicator
	var owner_id = data.get("ownerId", -1)
	var owner_indicator = tile.get_node("OwnerIndicator")
	if owner_id != null and owner_id >= 0:
		owner_indicator.visible = true
		owner_indicator.color = PLAYER_COLORS[owner_id % PLAYER_COLORS.size()]
	else:
		owner_indicator.visible = false

	# Update MULE indicator
	var mule_type = data.get("installedMule", GameState.MuleType.NONE)
	var mule_label = tile.get_node("MuleLabel")
	if mule_type != GameState.MuleType.NONE:
		mule_label.visible = true
		mule_label.text = _get_mule_symbol(mule_type)
	else:
		mule_label.visible = false

func _get_mule_symbol(mule_type: int) -> String:
	match mule_type:
		GameState.MuleType.FOOD: return "[F]"
		GameState.MuleType.ENERGY: return "[E]"
		GameState.MuleType.SMITHORE: return "[S]"
		GameState.MuleType.CRYSTITE: return "[C]"
	return ""

func _update_players():
	# Clear old sprites
	for sprite in player_sprites.values():
		sprite.queue_free()
	player_sprites.clear()

	# Create player sprites
	for player in GameState.players:
		var sprite = ColorRect.new()
		sprite.custom_minimum_size = Vector2(30, 30)
		sprite.size = Vector2(30, 30)
		sprite.color = PLAYER_COLORS[player.id % PLAYER_COLORS.size()]

		var x = player.get("positionX", TOWN_X)
		var y = player.get("positionY", TOWN_Y)

		var grid_pos = tile_grid.global_position
		sprite.global_position = grid_pos + Vector2(
			x * TILE_WIDTH + TILE_WIDTH/2 - 15,
			y * TILE_HEIGHT + TILE_HEIGHT/2 - 15
		)

		players_container.add_child(sprite)
		player_sprites[player.id] = sprite

func _update_player_info():
	for i in range(4):
		var panel = player_panels[i]
		if i < GameState.players.size():
			var player = GameState.players[i]
			panel.visible = true
			panel.get_node("Name").text = player.get("name", "Player %d" % (i+1))
			panel.get_node("Money").text = "$%d" % player.get("money", 0)
			panel.get_node("Resources").text = "F:%d E:%d S:%d C:%d" % [
				player.get("food", 0),
				player.get("energy", 0),
				player.get("smithore", 0),
				player.get("crystite", 0)
			]

			# Highlight current player
			if player.id == GameState.current_player_index:
				panel.modulate = Color(1.2, 1.2, 1.2)
			else:
				panel.modulate = Color.WHITE
		else:
			panel.visible = false

func _update_phase_instructions(phase: int):
	match phase:
		GameState.Phase.LAND_GRANT:
			if GameState.is_my_turn():
				phase_instructions.text = "Select a plot of land (SPACE to select, ESC to pass)"
			else:
				var current = GameState.get_current_player()
				phase_instructions.text = "%s is selecting land..." % current.get("name", "Player")
		GameState.Phase.DEVELOPMENT:
			if GameState.is_my_turn():
				phase_instructions.text = "Move around, buy MULEs, install them on your plots"
			else:
				var current = GameState.get_current_player()
				phase_instructions.text = "%s's turn..." % current.get("name", "Player")
		GameState.Phase.PRODUCTION:
			phase_instructions.text = "Calculating production..."
		_:
			phase_instructions.text = ""
