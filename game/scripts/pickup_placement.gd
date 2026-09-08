class_name PickupPlacement
extends RefCounted
const Tools = preload("res://scripts/tool_catalog.gd")

static func resting_offset(tool: String, rotation: Vector3) -> float:
	var box: AABB = Tools.visual(tool).bounds
	var orientation := Basis.from_euler(rotation)
	var lowest := INF
	for i: int in range(8): lowest = minf(lowest,(orientation * box.get_endpoint(i)).y)
	return -lowest + .003

static func resolve(spec: Dictionary) -> Dictionary:
	var result := spec.duplicate(true)
	var rotation: Vector3 = result.get("rotation",Vector3(PI/2,PI/2,0))
	result.rotation = rotation
	result.p = Vector3(result.support_point) + Vector3.UP * resting_offset(str(result.tool),rotation)
	result.yaw = rotation.y
	return result
