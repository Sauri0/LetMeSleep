class_name MapCatalog
extends RefCounted

## Immutable authored data; callers receive their own copy. Lobby is never a round map.
const HOUSE := {
	"id": "house", "label": "Casa de verano", "playable": true,
	"half_x": 6.0, "half_z": 5.0, "ceiling": 2.8,
	"bounds": AABB(Vector3(-6, 0, -5), Vector3(12, 2.8, 10)),
	"obstacles": [
		AABB(Vector3(-5.5, 0, 1.5), Vector3(2.4, 0.80, 1.05)),
		AABB(Vector3(-4.7, 0, -0.6), Vector3(1.4, 0.50, 1.0)),
		AABB(Vector3(3.9, 0, 1.7), Vector3(1.0, 1.1, 1.8)),
		AABB(Vector3(2.8, 0, -3.8), Vector3(1.9, 0.8, 0.9)),
	],
	"stations": [
		{"name": "Cerrar la ventana", "label": "VENTANA", "p": Vector3(-2.5, 0, -4.1)},
		{"name": "Prender el ventilador", "label": "VENTILADOR", "p": Vector3(4.8, 0, -0.6)},
		{"name": "Preparar repelente", "label": "REPELENTE", "p": Vector3(3.1, 0, -2.6)},
	],
	"pickups": [
		{"tool": "swatter", "p": Vector3(-2.6, 0.15, 1.7), "yaw": 0.3},
		{"tool": "racket", "p": Vector3(2.1, 0.15, 2.6), "yaw": -0.5},
		{"tool": "newspaper", "p": Vector3(-2.0, 0.15, -2.2), "yaw": 0.8},
		{"tool": "broom", "p": Vector3(3.0, 0.15, -0.2), "yaw": 1.1},
	],
	"human_spawns": [Vector3(-2, 0, 0.4), Vector3(-1, 0, 0.4), Vector3(0, 0, 0.4), Vector3(1, 0, 0.4), Vector3(2, 0, 0.4)],
	"mosquito_spawns": [
		Vector3(-2, 1.2, 3.2), Vector3(-1.25, 1.2, 3.2), Vector3(-0.5, 1.2, 3.2), Vector3(0.25, 1.2, 3.2), Vector3(1, 1.2, 3.2), Vector3(1.75, 1.2, 3.2),
		Vector3(-2, 1.55, 3.7), Vector3(-1.25, 1.55, 3.7), Vector3(-0.5, 1.55, 3.7), Vector3(0.25, 1.55, 3.7), Vector3(1, 1.55, 3.7), Vector3(1.75, 1.55, 3.7),
	],
	"respawn_points": [Vector3(-5, 1.8, -4), Vector3(5, 1.8, -4), Vector3(-5, 1.8, 4), Vector3(5, 1.8, 4), Vector3(0, 2, 4), Vector3(0, 2, -4)],
	"lobby_spawns": [],
}
const LOBBY := {
	"id": "lobby", "label": "Patio de espera", "playable": false,
	"half_x": 4.0, "half_z": 3.0, "ceiling": 4.0,
	"bounds": AABB(Vector3(-4, 0, -3), Vector3(8, 4, 6)),
	"obstacles": [
		AABB(Vector3(-3.9, 0, -0.9), Vector3(0.45, 0.5, 1.8)),
		AABB(Vector3(3.45, 0, -0.9), Vector3(0.45, 0.5, 1.8)),
	],
	"stations": [], "pickups": [], "human_spawns": [], "mosquito_spawns": [], "respawn_points": [],
	"lobby_spawns": [
		Vector3(-2.7, 0, -1.95), Vector3(-0.9, 0, -1.95), Vector3(0.9, 0, -1.95), Vector3(2.7, 0, -1.95),
		Vector3(-2.7, 0, -0.65), Vector3(-0.9, 0, -0.65), Vector3(0.9, 0, -0.65), Vector3(2.7, 0, -0.65),
		Vector3(-2.7, 0, 0.65), Vector3(-0.9, 0, 0.65), Vector3(0.9, 0, 0.65), Vector3(2.7, 0, 0.65),
		Vector3(-2.7, 0, 1.95), Vector3(-0.9, 0, 1.95), Vector3(0.9, 0, 1.95), Vector3(2.7, 0, 1.95),
	],
}

static func get_map(id: String = "house") -> Dictionary:
	if id == "house":
		return HOUSE.duplicate(true)
	if id == "lobby":
		return LOBBY.duplicate(true)
	return {}

static func is_playable(id: String) -> bool:
	return id == "house"

static func human_spawn(id: String, index: int) -> Vector3:
	var data: Dictionary = LOBBY if id == "lobby" else HOUSE
	var points: Array = data.lobby_spawns if id == "lobby" else data.human_spawns
	return points[posmod(index, points.size())]

static func mosquito_spawn(id: String, index: int) -> Vector3:
	var points: Array = HOUSE.mosquito_spawns if id == "house" else []
	return points[posmod(index, points.size())] if not points.is_empty() else human_spawn(id, index) + Vector3.UP * 1.2
