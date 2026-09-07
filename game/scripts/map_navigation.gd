class_name MapNavigation
extends RefCounted
## Small authored graph shared by offline bots. No engine navigation bake or 3D grid.
## Human points are feet; mosquito points are centers. Reuse routes between repaths.

const Maps = preload("res://scripts/map_catalog.gd")
const HUMAN_RADIUS := 0.60
const HUMAN_HEIGHT := 1.95
const MOSQUITO_RADIUS := 0.04
const STEP_HEIGHT := 0.221
static var _cache: Dictionary = {}

static func _data(map_id: String) -> Dictionary:
	return Maps.LOBBY if map_id == "lobby" else Maps.HOUSE

static func clear_cache() -> void:
	_cache.clear()

static func _geometry(human: bool, map_id: String) -> Dictionary:
	var key: String = map_id + ("/human" if human else "/mosquito")
	if _cache.has(key):
		return _cache[key]
	var data: Dictionary = _data(map_id)
	var expanded: Array[AABB] = []
	for obstacle: AABB in data.obstacles:
		if human:
			expanded.append(AABB(obstacle.position - Vector3(HUMAN_RADIUS, HUMAN_HEIGHT - 0.003, HUMAN_RADIUS), obstacle.size + Vector3(HUMAN_RADIUS * 2.0, HUMAN_HEIGHT - 0.006, HUMAN_RADIUS * 2.0)))
		else:
			expanded.append(obstacle.grow(MOSQUITO_RADIUS))
	var supports: Array[AABB] = []
	for obstacle: AABB in data.get("floors", []):
		supports.append(obstacle)
	for obstacle: AABB in data.get("steps", []):
		supports.append(obstacle)
	var result := {"data": data, "expanded": expanded, "supports": supports, "human": human, "nodes": [], "edges": [], "invalid_edges": []}
	_cache[key] = result
	return result

static func _fits(point: Vector3, geometry: Dictionary) -> bool:
	var data: Dictionary = geometry.data
	var radius: float = HUMAN_RADIUS if geometry.human else MOSQUITO_RADIUS
	var top: float = HUMAN_HEIGHT if geometry.human else MOSQUITO_RADIUS
	var bottom: float = 0.0 if geometry.human else MOSQUITO_RADIUS
	if absf(point.x) + radius > float(data.half_x) or absf(point.z) + radius > float(data.half_z) or point.y < bottom - 0.001 or point.y + top > float(data.ceiling) + 0.001:
		return false
	for obstacle: AABB in geometry.expanded:
		if obstacle.has_point(point):
			return false
	return true

static func _support_height(point: Vector3, geometry: Dictionary, tolerance: float) -> float:
	var result: float = 0.0 if point.y <= tolerance else -INF
	for obstacle: AABB in geometry.supports:
		if obstacle.end.y > point.y + tolerance or obstacle.end.y < point.y - tolerance:
			continue
		if point.x + HUMAN_RADIUS > obstacle.position.x and point.x - HUMAN_RADIUS < obstacle.end.x and point.z + HUMAN_RADIUS > obstacle.position.z and point.z - HUMAN_RADIUS < obstacle.end.z:
			result = maxf(result, obstacle.end.y)
	return result

static func _project_human(point: Vector3, geometry: Dictionary) -> Vector3:
	var candidates: Array[float] = [0.0]
	for obstacle: AABB in geometry.supports:
		if obstacle.end.y <= point.y + STEP_HEIGHT and point.x + HUMAN_RADIUS > obstacle.position.x and point.x - HUMAN_RADIUS < obstacle.end.x and point.z + HUMAN_RADIUS > obstacle.position.z and point.z - HUMAN_RADIUS < obstacle.end.z:
			if not candidates.has(obstacle.end.y):
				candidates.append(obstacle.end.y)
	candidates.sort()
	candidates.reverse()
	for height: float in candidates:
		var trial := Vector3(point.x, height, point.z)
		if _fits(trial, geometry):
			return trial
	return Vector3(INF, INF, INF)

static func can_travel(from: Vector3, to: Vector3, human: bool, map_id: String = "house") -> bool:
	if not from.is_finite() or not to.is_finite() or map_id not in ["house", "lobby"]:
		return false
	return _segment(from, to, _geometry(human, map_id))

static func _segment(from: Vector3, to: Vector3, geometry: Dictionary) -> bool:
	if not _fits(from, geometry) or not _fits(to, geometry):
		return false
	if not geometry.human:
		for obstacle: AABB in geometry.expanded:
			if obstacle.intersects_segment(from, to) != null:
				return false
		return true
	var flat_distance: float = Vector2(from.x - to.x, from.z - to.z).length()
	if absf(from.y - to.y) > flat_distance * 0.6 + STEP_HEIGHT:
		return false
	if absf(from.y - to.y) < 0.01:
		for obstacle: AABB in geometry.expanded:
			if obstacle.intersects_segment(from, to) != null:
				return false
		# A clear line above a stairwell is not a walkable bridge.
		for index: int in range(1, maxi(2, int(ceilf(flat_distance / 0.35)))):
			var point: Vector3 = from.lerp(to, float(index) / float(maxi(2, int(ceilf(flat_distance / 0.35)))))
			if not is_finite(_support_height(point, geometry, 0.025)):
				return false
		return true
	var count: int = maxi(2, int(ceilf(flat_distance / 0.20)))
	var previous: Vector3 = from
	for index: int in range(1, count + 1):
		var point: Vector3 = from.lerp(to, float(index) / float(count))
		var floor_y: float = _support_height(point, geometry, STEP_HEIGHT)
		if not is_finite(floor_y) or absf(floor_y - previous.y) > STEP_HEIGHT:
			return false
		point.y = floor_y
		if not _fits(point, geometry):
			return false
		previous = point
	return true

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
	if not from.is_finite() or not to.is_finite() or map_id not in ["house", "lobby"]:
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
