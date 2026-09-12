class_name NavigationGeometry
extends RefCounted
## Shared physical route predicates. No map catalog dependency, so generated
## blueprints can be validated with the exact predicates used by runtime bots.
const HUMAN_RADIUS := 0.60
const HUMAN_HEIGHT := 1.95
const MOSQUITO_RADIUS := 0.04
const STEP_HEIGHT := 0.221

## The playable lot can extend above and beyond the building. Roofs and walls
## belong to obstacles; only the outer lot bounds constrain all movement.
static func world_bounds(data: Dictionary) -> AABB:
	if data.has("bounds"):
		return data.bounds
	return AABB(Vector3(-float(data.half_x), 0, -float(data.half_z)), Vector3(float(data.half_x) * 2, float(data.ceiling), float(data.half_z) * 2))

static func create(data: Dictionary, human: bool, extra: Array[AABB] = []) -> Dictionary:
	var expanded: Array[AABB] = []
	var obstacles: Array[AABB] = []
	for box: AABB in data.obstacles: obstacles.append(box)
	obstacles.append_array(extra)
	for obstacle: AABB in obstacles:
		if human:
			expanded.append(AABB(obstacle.position - Vector3(HUMAN_RADIUS, HUMAN_HEIGHT - .003, HUMAN_RADIUS), obstacle.size + Vector3(HUMAN_RADIUS * 2, HUMAN_HEIGHT - .006, HUMAN_RADIUS * 2)))
		else: expanded.append(obstacle.grow(MOSQUITO_RADIUS))
	var supports: Array[AABB] = []
	for key: String in ["floors", "steps"]:
		for box: AABB in data.get(key, []): supports.append(box)
	return {"data":data,"bounds":world_bounds(data),"expanded":expanded,"supports":supports,"human":human,"nodes":[],"edges":[],"invalid_edges":[]}

static func _fits(point: Vector3, geometry: Dictionary) -> bool:
	var bounds: AABB = geometry.bounds
	var radius: float = HUMAN_RADIUS if geometry.human else MOSQUITO_RADIUS
	var top: float = HUMAN_HEIGHT if geometry.human else MOSQUITO_RADIUS
	var bottom: float = 0.0 if geometry.human else MOSQUITO_RADIUS
	if point.x - radius < bounds.position.x or point.x + radius > bounds.end.x or point.z - radius < bounds.position.z or point.z + radius > bounds.end.z or point.y < bounds.position.y + bottom - 0.001 or point.y + top > bounds.end.y + 0.001:
		return false
	for obstacle: AABB in geometry.expanded:
		if obstacle.has_point(point):
			return false
	return true

static func _support_height(point: Vector3, geometry: Dictionary, tolerance: float) -> float:
	var ground: float = geometry.bounds.position.y
	var result: float = ground if absf(point.y - ground) <= tolerance else -INF
	for obstacle: AABB in geometry.supports:
		if obstacle.end.y > point.y + tolerance or obstacle.end.y < point.y - tolerance:
			continue
		if point.x + HUMAN_RADIUS > obstacle.position.x and point.x - HUMAN_RADIUS < obstacle.end.x and point.z + HUMAN_RADIUS > obstacle.position.z and point.z - HUMAN_RADIUS < obstacle.end.z:
			result = maxf(result, obstacle.end.y)
	return result

static func _project_human(point: Vector3, geometry: Dictionary) -> Vector3:
	var candidates: Array[float] = [float(geometry.bounds.position.y)]
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

