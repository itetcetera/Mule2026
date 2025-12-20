extends Node
## Handles all communication with the C# game server
## Autoloaded as GameServer

signal connected
signal disconnected
signal connection_error(message: String)
signal game_state_updated(state: Dictionary)
signal error(message: String)

const DEFAULT_HOST = "http://localhost:5000"

var http_request: HTTPRequest
var server_url: String = DEFAULT_HOST
var game_id: String = ""
var player_id: int = -1
var polling_timer: Timer

func _ready():
	http_request = HTTPRequest.new()
	add_child(http_request)
	http_request.request_completed.connect(_on_request_completed)

	# Timer for polling game state
	polling_timer = Timer.new()
	polling_timer.wait_time = 0.1  # 100ms polling for responsive gameplay
	polling_timer.timeout.connect(_poll_game_state)
	add_child(polling_timer)

func set_server(url: String):
	server_url = url

## Create a new game
func create_game(difficulty: int, players: Array) -> void:
	var body = JSON.stringify({
		"difficulty": difficulty,
		"players": players
	})
	_post("/api/game/create", body)

## Join an existing game
func join_game(id: String, player_name: String, species: int) -> void:
	var body = JSON.stringify({
		"gameId": id,
		"playerName": player_name,
		"species": species
	})
	_post("/api/game/join", body)

## Get current game state
func get_game_state() -> void:
	if game_id.is_empty():
		return
	_get("/api/game/%s" % game_id)

## Start polling for game state updates
func start_polling():
	polling_timer.start()

func stop_polling():
	polling_timer.stop()

func _poll_game_state():
	get_game_state()

# ============ Land Grant Phase ============

func move_cursor(dx: int, dy: int) -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id,
		"dx": dx,
		"dy": dy
	})
	_post("/api/game/land/move", body)

func select_land() -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id
	})
	_post("/api/game/land/select", body)

func pass_land_grant() -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id
	})
	_post("/api/game/land/pass", body)

# ============ Development Phase ============

func move_player(x: int, y: int) -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id,
		"x": x,
		"y": y
	})
	_post("/api/game/player/move", body)

func buy_mule() -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id
	})
	_post("/api/game/store/buy-mule", body)

func outfit_mule(mule_type: int) -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id,
		"muleType": mule_type
	})
	_post("/api/game/store/outfit-mule", body)

func install_mule(x: int, y: int) -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id,
		"x": x,
		"y": y
	})
	_post("/api/game/mule/install", body)

func enter_pub() -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id
	})
	_post("/api/game/pub/enter", body)

func end_turn() -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id
	})
	_post("/api/game/turn/end", body)

# ============ Auction Phase ============

func set_auction_position(is_buying: bool, price: int) -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id,
		"isBuying": is_buying,
		"price": price
	})
	_post("/api/game/auction/position", body)

func trade_with_store(resource: int, quantity: int, is_buying: bool) -> void:
	var body = JSON.stringify({
		"gameId": game_id,
		"playerId": player_id,
		"resource": resource,
		"quantity": quantity,
		"isBuying": is_buying
	})
	_post("/api/game/auction/store-trade", body)

# ============ HTTP Helpers ============

func _get(endpoint: String) -> void:
	var url = server_url + endpoint
	http_request.request(url, [], HTTPClient.METHOD_GET)

func _post(endpoint: String, body: String) -> void:
	var url = server_url + endpoint
	var headers = ["Content-Type: application/json"]
	http_request.request(url, headers, HTTPClient.METHOD_POST, body)

func _on_request_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
	if result != HTTPRequest.RESULT_SUCCESS:
		error.emit("Connection failed: %d" % result)
		return

	if response_code >= 400:
		var error_text = body.get_string_from_utf8()
		error.emit("Server error %d: %s" % [response_code, error_text])
		return

	var json = JSON.new()
	var parse_result = json.parse(body.get_string_from_utf8())
	if parse_result != OK:
		error.emit("Failed to parse response")
		return

	var data = json.data

	# Update local state if this is game state
	if data.has("gameId"):
		game_id = data.gameId
	if data.has("phase"):
		game_state_updated.emit(data)
