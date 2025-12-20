extends Control
## Resource auction screen - M.U.L.E. style auction mechanics
## Buyers move up from bottom, sellers move down from top
## When they meet, a trade occurs

const PLAYER_COLORS = [
	Color(1.0, 0.2, 0.2),  # Red
	Color(0.2, 0.6, 1.0),  # Blue
	Color(0.2, 0.8, 0.2),  # Green
	Color(1.0, 0.8, 0.2),  # Yellow
]

const RESOURCE_NAMES = ["FOOD", "ENERGY", "SMITHORE", "CRYSTITE"]

# Auction order matches Atari 800: Smithore → Crystite → Food → Energy
const AUCTION_ORDER = [
	GameState.ResourceType.SMITHORE,
	GameState.ResourceType.CRYSTITE,
	GameState.ResourceType.FOOD,
	GameState.ResourceType.ENERGY
]

@onready var title_label = $Title
@onready var timer_label = $Timer
@onready var instructions = $Instructions
@onready var price_bar = $PriceBar
@onready var player_positions = $PriceBar/PlayerPositions
@onready var store_price_rect = $PriceBar/StorePrice
@onready var store_price_label = $PriceBar/StorePrice/StorePriceLabel
@onready var resource_info = $ResourceInfo/VBoxContainer

var current_resource: int = GameState.ResourceType.SMITHORE
var my_price: int = 0
var am_buying: bool = true
var price_min: int = 0
var price_max: int = 200
var store_price: int = 100
var player_markers: Dictionary = {}  # player_id -> marker node

var move_cooldown: float = 0.0

func _ready():
	_update_from_state()
	GameServer.game_state_updated.connect(_on_state_updated)

func _process(delta):
	move_cooldown = max(0, move_cooldown - delta)
	_handle_input()
	_update_timer()

func _handle_input():
	if move_cooldown > 0:
		return

	# Move price up/down
	if Input.is_action_pressed("move_up"):
		if am_buying:
			my_price = min(price_max, my_price + 5)
		else:
			my_price = max(price_min, my_price - 5)
		move_cooldown = 0.1
		_send_position()
		_update_my_marker()

	elif Input.is_action_pressed("move_down"):
		if am_buying:
			my_price = max(price_min, my_price - 5)
		else:
			my_price = min(price_max, my_price + 5)
		move_cooldown = 0.1
		_send_position()
		_update_my_marker()

	# Switch between buying and selling
	if Input.is_action_just_pressed("move_left") or Input.is_action_just_pressed("move_right"):
		am_buying = not am_buying
		my_price = store_price  # Reset to store price
		_send_position()
		_update_my_marker()

	# Buy/sell from store
	if Input.is_action_just_pressed("action"):
		var player = GameState.get_local_player()
		var my_stock = _get_player_resource(player, current_resource)

		if am_buying:
			# Buy from store if we have money and store has stock
			if player.get("money", 0) >= store_price:
				GameServer.trade_with_store(current_resource, 1, true)
		else:
			# Sell to store if we have stock
			if my_stock > 0:
				GameServer.trade_with_store(current_resource, 1, false)

func _send_position():
	GameServer.set_auction_position(am_buying, my_price)

func _on_state_updated(_state: Dictionary):
	_update_from_state()

func _update_from_state():
	var auction_state = GameState.resource_auction_state
	if auction_state.is_empty():
		return

	current_resource = auction_state.get("currentResource", GameState.ResourceType.SMITHORE)
	title_label.text = "%s AUCTION" % RESOURCE_NAMES[current_resource]

	# Update price range based on store prices
	_update_price_range()

	# Update player markers
	_update_player_markers(auction_state)

	# Update resource info
	_update_resource_info()

func _update_price_range():
	var store = GameState.store
	if store.is_empty():
		return

	match current_resource:
		GameState.ResourceType.FOOD:
			store_price = store.get("foodPrice", 30)
		GameState.ResourceType.ENERGY:
			store_price = store.get("energyPrice", 25)
		GameState.ResourceType.SMITHORE:
			store_price = store.get("smithorePrice", 50)
		GameState.ResourceType.CRYSTITE:
			store_price = store.get("crystitePrice", 100)

	# Price range is typically store price +/- some amount
	price_min = max(0, store_price - 100)
	price_max = store_price + 100

	# Update price labels
	$PriceBar/PriceLabels/MaxPrice.text = "$%d" % price_max
	$PriceBar/PriceLabels/MidPrice.text = "$%d" % store_price
	$PriceBar/PriceLabels/MinPrice.text = "$%d" % price_min

	# Position store price indicator
	var bar_height = price_bar.size.y
	var store_y = _price_to_y(store_price)
	store_price_rect.position.y = store_y - 20
	store_price_label.text = "STORE: $%d" % store_price

func _update_player_markers(auction_state: Dictionary):
	# Clear old markers
	for marker in player_markers.values():
		marker.queue_free()
	player_markers.clear()

	var buyer_positions = auction_state.get("buyerPositions", {})
	var seller_positions = auction_state.get("sellerPositions", {})

	# Create markers for buyers
	for player_id in buyer_positions:
		var price = buyer_positions[player_id]
		_create_player_marker(int(player_id), price, true)

	# Create markers for sellers
	for player_id in seller_positions:
		var price = seller_positions[player_id]
		_create_player_marker(int(player_id), price, false)

func _create_player_marker(player_id: int, price: int, is_buyer: bool):
	var marker = ColorRect.new()
	marker.custom_minimum_size = Vector2(40, 30)
	marker.size = Vector2(40, 30)
	marker.color = PLAYER_COLORS[player_id % PLAYER_COLORS.size()]

	var y = _price_to_y(price)
	var x = 50 + (player_id * 60)
	if not is_buyer:
		x += 300  # Sellers on right side

	marker.position = Vector2(x, y - 15)

	# Add label showing if buyer or seller
	var label = Label.new()
	label.text = "B" if is_buyer else "S"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_preset(Control.PRESET_FULL_RECT)
	marker.add_child(label)

	player_positions.add_child(marker)
	player_markers[player_id] = marker

func _update_my_marker():
	var player = GameState.get_local_player()
	if player.is_empty():
		return

	var player_id = player.get("id", 0)
	if player_markers.has(player_id):
		var marker = player_markers[player_id]
		var y = _price_to_y(my_price)
		var x = 50 + (player_id * 60)
		if not am_buying:
			x += 300
		marker.position = Vector2(x, y - 15)

func _price_to_y(price: int) -> float:
	# Convert price to Y position on the bar
	# Top = max price, bottom = min price
	var bar_height = price_bar.size.y
	var price_range = price_max - price_min
	if price_range == 0:
		return bar_height / 2

	var normalized = float(price - price_min) / float(price_range)
	return bar_height * (1.0 - normalized)

func _update_timer():
	var auction_state = GameState.resource_auction_state
	var time = auction_state.get("timeRemaining", 60)
	timer_label.text = str(time)

func _update_resource_info():
	var player = GameState.get_local_player()
	var store = GameState.store

	var store_stock = 0
	match current_resource:
		GameState.ResourceType.FOOD:
			store_stock = store.get("foodStock", 0)
		GameState.ResourceType.ENERGY:
			store_stock = store.get("energyStock", 0)
		GameState.ResourceType.SMITHORE:
			store_stock = store.get("smithoreStock", 0)
		GameState.ResourceType.CRYSTITE:
			store_stock = store.get("crystiteStock", 0)

	var my_stock = _get_player_resource(player, current_resource)

	resource_info.get_node("StoreStock").text = "Store Stock: %d" % store_stock
	resource_info.get_node("StorePrice").text = "Store Price: $%d" % store_price
	resource_info.get_node("YourStock").text = "Your Stock: %d" % my_stock
	resource_info.get_node("YourMoney").text = "Your Money: $%d" % player.get("money", 0)

func _get_player_resource(player: Dictionary, resource: int) -> int:
	match resource:
		GameState.ResourceType.FOOD:
			return player.get("food", 0)
		GameState.ResourceType.ENERGY:
			return player.get("energy", 0)
		GameState.ResourceType.SMITHORE:
			return player.get("smithore", 0)
		GameState.ResourceType.CRYSTITE:
			return player.get("crystite", 0)
	return 0
