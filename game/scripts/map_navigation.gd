class_name MapNavigation
extends RefCounted
## Small authored graph shared by offline bots. No engine navigation bake or 3D grid.
## Human points are feet; mosquito points are centers. Reuse routes between repaths.

const Geometry = preload("res://scripts/navigation_geometry.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const HouseBarriers = preload("res://scripts/house_barriers.gd")
const PickupSupports = preload("res://scripts/pickup_supports.gd")
const HUMAN_RADIUS := 0.60
const HUMAN_HEIGHT := 1.95
const MOSQUITO_RADIUS := 0.04
const STEP_HEIGHT := 0.221
const GeometryCache=preload("res://scripts/geometry_cache.gd")
static var _cache_order: Array[String]=[]
static var _cache: Dictionary = {}

static func _data(map_id: String) -> Dictionary:
	return Maps.get_map(map_id)

static func clear_cache() -> void:
	_cache.clear()
	_cache_order.clear()

static func _geometry(human: bool, map_id: String) -> Dictionary:
	var key: String = map_id + ("/human" if human else "/mosquito")
	GeometryCache.touch(_cache_order,key,48,[_cache])
	if _cache.has(key):
		return _cache[key]
	var data: Dictionary = _data(map_id)
	var extra: Array[AABB] = HouseBarriers.get_boxes(map_id)
	extra.append_array(PickupSupports.get_boxes(map_id))
	var result := Geometry.create(data, human, extra)
	_cache[key] = result
	return result

static func _fits(point: Vector3, geometry: Dictionary) -> bool:
	return Geometry._fits(point, geometry)

static func _support_height(point: Vector3, geometry: Dictionary, tolerance: float) -> float:
	return Geometry._support_height(point, geometry, tolerance)

static func _project_human(point: Vector3, geometry: Dictionary) -> Vector3:
	return Geometry._project_human(point, geometry)

static func _segment(from: Vector3, to: Vector3, geometry: Dictionary) -> bool:
	return Geometry._segment(from, to, geometry)

static func can_travel(from: Vector3, to: Vector3, human: bool, map_id: String = "house") -> bool:
	if not from.is_finite() or not to.is_finite() or (map_id!="lobby" and not Maps.is_playable(map_id)):
		return false
	return _segment(from, to, _geometry(human, map_id))

static func _graph(human: bool, map_id: String) -> Dictionary:
	var graph: Dictionary = _geometry(human, map_id)
	if not graph.nodes.is_empty():
		return graph
	var data: Dictionary = graph.data
	var authored: Array = data.get("nav_nodes", data.get("lobby_spawns", []))
	var nodes: Array[Vector3] = []
	var adjacency: Array = []
	for feet: Vector3 in authored:
		nodes.append(feet if human else feet + Vector3.UP * 1.2)
		adjacency.append([])
	var links: Array = data.get("nav_edges", [])
	if map_id == "lobby":
		for first: int in range(nodes.size()):
			for second: int in range(first + 1, nodes.size()):
				links.append(Vector2i(first, second))
	for link: Vector2i in links:
		if _segment(nodes[link.x], nodes[link.y], graph):
			adjacency[link.x].append(link.y)
			adjacency[link.y].append(link.x)
		else:
			graph.invalid_edges.append(link)
	graph.nodes = nodes
	graph.edges = adjacency
	return graph

static func graph_info(human: bool, map_id: String = "house") -> Dictionary:
	var graph: Dictionary = _graph(human, map_id)
	return {"nodes": graph.nodes.duplicate(), "edges": graph.edges.duplicate(true), "invalid_edges": graph.invalid_edges.duplicate()}

static func _connections(point: Vector3, graph: Dictionary) -> Array[int]:
	var order: Array[int] = []
	for index: int in range(graph.nodes.size()):
		order.append(index)
	order.sort_custom(func(first: int, second: int) -> bool: return point.distance_squared_to(graph.nodes[first]) < point.distance_squared_to(graph.nodes[second]))
	var result: Array[int] = []
	for index: int in order:
		if graph.human and absf(point.y - Vector3(graph.nodes[index]).y) > 0.25:
			continue
		if _segment(point, graph.nodes[index], graph):
			result.append(index)
			if result.size() >= 3:
				break
	return result

static func path(from: Vector3, to: Vector3, human: bool, map_id: String = "house") -> PackedVector3Array:
	var empty := PackedVector3Array()
	if not from.is_finite() or not to.is_finite() or (map_id!="lobby" and not Maps.is_playable(map_id)):
		return empty
	var graph: Dictionary = _graph(human, map_id)
	var origin: Vector3 = _project_human(from, graph) if human else from
	var destination: Vector3 = _project_human(to, graph) if human else to
	if not origin.is_finite() or not destination.is_finite():
		return empty
	if _segment(origin, destination, graph):
		return PackedVector3Array([destination])
	var starts: Array[int] = _connections(origin, graph)
	var ends: Array[int] = _connections(destination, graph)
	if starts.is_empty() or ends.is_empty():
		return empty
	var count: int = graph.nodes.size()
	var costs: Array[float] = []
	var previous: Array[int] = []
	var open: Array[int] = starts.duplicate()
	costs.resize(count + 1)
	costs.fill(INF)
	previous.resize(count + 1)
	previous.fill(-1)
	for start: int in starts:
		costs[start] = origin.distance_to(graph.nodes[start])
	while not open.is_empty():
		var best: int = 0
		var score: float = INF
		for slot: int in range(open.size()):
			var index: int = open[slot]
			var position: Vector3 = destination if index == count else graph.nodes[index]
			var estimate: float = costs[index] + position.distance_to(destination)
			if estimate < score:
				best = slot
				score = estimate
		var current: int = open[best]
		open.remove_at(best)
		if current == count:
			var route: Array[Vector3] = [destination]
			current = previous[current]
			while current >= 0:
				route.push_front(graph.nodes[current])
				current = previous[current]
			while route.size() > 1 and origin.distance_to(route[0]) < 0.08:
				route.pop_front()
			return PackedVector3Array(route)
		var neighbors: Array = graph.edges[current].duplicate()
		if ends.has(current):
			neighbors.append(count)
		for neighbor: int in neighbors:
			var next_position: Vector3 = destination if neighbor == count else graph.nodes[neighbor]
			var candidate: float = costs[current] + Vector3(graph.nodes[current]).distance_to(next_position)
			if candidate + 0.0001 < costs[neighbor]:
				costs[neighbor] = candidate
				previous[neighbor] = current
				if not open.has(neighbor):
					open.append(neighbor)
	return empty
