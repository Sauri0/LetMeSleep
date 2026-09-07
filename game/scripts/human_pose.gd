class_name HumanPose
extends RefCounted

const SWING_SECONDS := {"hands": 0.80, "swatter": 0.60, "racket": 1.05, "newspaper": 0.43, "broom": 1.20}
const SWING_GESTURE_SECONDS := 0.36

## Shared deterministic skeletal points. No local wall clock or render delta enters
## this pose: body meshes, private zones and attached insects use the same snapshot.
static func sample(actor: Dictionary) -> Dictionary:
	var crouch: float = clampf(float(actor.get("crouch_amount", 0.0)), 0.0, 1.0)
	var phase: float = float(actor.get("motion_phase", 0.0))
	var speed: float = clampf(float(actor.get("motion_speed", 0.0)) / 3.1, 0.0, 1.0)
	var grounded: bool = bool(actor.get("grounded", true))
	var running: bool = bool(actor.get("sprinting", false))
	var stride: float = (0.14 if running else 0.10) * speed * (0.5 if crouch > 0.5 else 1.0) if grounded else 0.0
	var gait: float = sin(phase) * stride
	var lift_l: float = maxf(0.0, sin(phase)) * stride * 0.45
	var lift_r: float = maxf(0.0, -sin(phase)) * stride * 0.45
	var tool: String = str(actor.get("tool", "hands"))
	var swing_left: float = maxf(0.0, float(actor.get("swing", 0.0)))
	var swing_duration: float = float(SWING_SECONDS.get(tool, 0.8))
	var swing: float = sin(clampf((swing_duration - swing_left) / SWING_GESTURE_SECONDS, 0.0, 1.0) * PI) if swing_left > 0.0 else 0.0
	var torso := Vector3(0, 1.09 - crouch * 0.45, crouch * 0.06)
	var head := Vector3(0, 1.55 - crouch * 0.55, -crouch * 0.02)
	var result: Dictionary = {
		"torso": torso, "head": head, "eye": head,
		"pelvis": Vector3(0, 0.73 - crouch * 0.25, crouch * 0.10),
		"head_basis": Basis(Vector3.RIGHT, clampf(float(actor.get("pitch", 0.0)), -1.48, 1.48) * 0.25),
		"torso_height": lerpf(0.68, 0.52, crouch),
	}
	for side: float in [-1.0, 1.0]:
		var suffix: String = "_l" if side < 0.0 else "_r"
		var step: float = gait * (-side)
		var lift: float = lift_l if side < 0.0 else lift_r
		var tuck: float = 0.0 if grounded else 1.0
		result["hip" + suffix] = Vector3(side * 0.135, 0.75 - crouch * 0.25, 0.01 + crouch * 0.10)
		result["knee" + suffix] = Vector3(side * 0.135, 0.42 - crouch * 0.12 + lift * 0.5 + tuck * 0.04, 0.01 - crouch * 0.12 + step * 0.6 - tuck * 0.055)
		result["ankle" + suffix] = Vector3(side * 0.135, 0.10 + lift + tuck * 0.05, -step)
		result["shoulder" + suffix] = Vector3(side * 0.31, 1.30 - crouch * 0.45, crouch * 0.06)
		var arm_swing: float = swing if tool == "hands" or side > 0.0 else 0.0
		var elbow := Vector3(side * 0.33, 1.07 - crouch * 0.45, -0.005 + step * 0.3)
		var hand := Vector3(side * 0.33, 0.86 - crouch * 0.40, -0.025 + step * 0.3)
		var target_elbow := Vector3(side * 0.29, 1.14 - crouch * 0.43, -0.23)
		var target_hand := Vector3(side * 0.08 if tool == "hands" else side * 0.27, 1.15 - crouch * 0.40, -0.36)
		result["elbow" + suffix] = elbow.lerp(target_elbow, arm_swing)
		result["hand" + suffix] = hand.lerp(target_hand, arm_swing)
	return result

static func zone_pose(actor: Dictionary, zone: Dictionary) -> Dictionary:
	var pose: Dictionary = sample(actor)
	var rest: Dictionary = sample({})
	var bone: String = str(zone.get("bone", "torso"))
	var anchor: Vector3 = _anchor(pose, bone)
	var rest_anchor: Vector3 = _anchor(rest, bone)
	var delta_basis: Basis = _basis(pose, bone) * _basis(rest, bone).inverse()
	var local_point: Vector3 = anchor + delta_basis * (Vector3(zone.p) - rest_anchor)
	var local_normal: Vector3 = delta_basis * (Vector3.BACK if bool(zone.get("rear", false)) else Vector3.FORWARD)
	var capsules: Array[Dictionary] = collision_segments(actor)
	var surface_key: String = bone
	if bone.begins_with("shoulder_"):
		surface_key = "upperarm" + bone.right(2)
	elif bone.begins_with("knee_"):
		surface_key = "shin" + bone.right(2)
	elif bone.begins_with("ankle_"):
		surface_key = "foot" + bone.right(2)
	for capsule: Dictionary in capsules:
		if str(capsule.key) != surface_key:
			continue
		var segment: Vector3 = Vector3(capsule.to) - Vector3(capsule.from)
		var center: Vector3 = Vector3(capsule.from) + segment * clampf((local_point - Vector3(capsule.from)).dot(segment) / maxf(segment.length_squared(), 0.000001), 0.0, 1.0)
		local_normal = (local_point - center).normalized()
		local_point = center + local_normal * float(capsule.radius)
		break
	# Joined body capsules overlap at elbows, hips and crouched pelvis. A point
	# on one capsule may still be buried in its neighbour. Use the first skin of
	# their union, preserving a real outward normal instead of a floating seed.
	var outside: Vector3 = local_point + local_normal * 0.50
	var inside: Vector3 = local_point - local_normal * 0.03
	var first_distance := INF
	for capsule: Dictionary in capsules:
		var hit: Dictionary = ray_capsule(outside, inside, capsule)
		if not hit.is_empty() and float(hit.distance) < first_distance:
			first_distance = float(hit.distance)
			local_point = hit.p
			local_normal = hit.normal
	var yaw: float = float(actor.get("yaw", 0.0))
	return {"p": Vector3(actor.get("p", Vector3.ZERO)) + local_point.rotated(Vector3.UP, yaw), "normal": local_normal.rotated(Vector3.UP, yaw).normalized(), "label": str(zone.get("label", "Zona"))}

static func ray_capsule(from: Vector3, to: Vector3, capsule: Dictionary) -> Dictionary:
	var nearest: PackedVector3Array = Geometry3D.get_closest_points_between_segments(from, to, capsule.from, capsule.to)
	var radius: float = capsule.radius
	if nearest[0].distance_squared_to(nearest[1]) > radius * radius:
		return {}
	var travel: Vector3 = to - from
	var high: float = clampf((nearest[0] - from).dot(travel) / maxf(travel.length_squared(), 0.000001), 0.0, 1.0)
	var low := 0.0
	var axis: Vector3 = Vector3(capsule.to) - Vector3(capsule.from)
	for iteration: int in range(14):
		var middle: float = (low + high) * 0.5
		var point: Vector3 = from + travel * middle
		var center: Vector3 = Vector3(capsule.from) + axis * clampf((point - Vector3(capsule.from)).dot(axis) / maxf(axis.length_squared(), 0.000001), 0.0, 1.0)
		if point.distance_squared_to(center) <= radius * radius:
			high = middle
		else:
			low = middle
	var point: Vector3 = from + travel * high
	var center: Vector3 = Vector3(capsule.from) + axis * clampf((point - Vector3(capsule.from)).dot(axis) / maxf(axis.length_squared(), 0.000001), 0.0, 1.0)
	return {"p": point, "normal": (point - center).normalized(), "distance": from.distance_to(point)}

static func _anchor(pose: Dictionary, bone: String) -> Vector3:
	if bone.begins_with("thigh_"):
		var suffix: String = bone.right(2)
		return Vector3(pose["hip" + suffix]).lerp(pose["knee" + suffix], 0.5)
	if bone.begins_with("forearm_"):
		var suffix: String = bone.right(2)
		return Vector3(pose["elbow" + suffix]).lerp(pose["hand" + suffix], 0.4)
	return Vector3(pose.get(bone, pose.torso))

static func _basis(pose: Dictionary, bone: String) -> Basis:
	if bone == "head":
		return pose.head_basis
	if bone.begins_with("forearm_"):
		var suffix: String = bone.right(2)
		var segment: Vector3 = Vector3(pose["hand" + suffix]) - Vector3(pose["elbow" + suffix])
		var up: Vector3 = -segment.normalized()
		# Keep the elbow's lateral axis stable when a swing points straight ahead;
		# projecting BACK instead would twist the mark sideways near that pose.
		var right: Vector3 = (Vector3.RIGHT - up * Vector3.RIGHT.dot(up)).normalized()
		return Basis(right, up, right.cross(up))
	return Basis.IDENTITY

static func body_boxes(actor: Dictionary) -> Array[AABB]:
	var pose: Dictionary = sample(actor)
	var torso_half := Vector3(0.25, float(pose.torso_height) * 0.5, 0.25)
	var head_half := Vector3(0.20, 0.18, 0.20)
	return [AABB(Vector3(pose.torso) - torso_half, torso_half * 2), AABB(Vector3(pose.head) - head_half, head_half * 2)]

static func collision_segments(actor: Dictionary) -> Array[Dictionary]:
	var pose: Dictionary = sample(actor)
	var torso_half_axis: float = maxf(0.0, float(pose.torso_height) * 0.5 - 0.24)
	var result: Array[Dictionary] = [
		{"key": "torso", "from": Vector3(pose.torso) - Vector3.UP * torso_half_axis, "to": Vector3(pose.torso) + Vector3.UP * torso_half_axis, "radius": 0.24},
		{"key": "head", "from": pose.head, "to": pose.head, "radius": 0.235},
		{"key": "pelvis", "from": pose.pelvis, "to": pose.pelvis, "radius": 0.20},
	]
	for suffix: String in ["_l", "_r"]:
		result.append({"key": "thigh" + suffix, "from": pose["hip" + suffix], "to": pose["knee" + suffix], "radius": 0.105})
		result.append({"key": "shin" + suffix, "from": pose["knee" + suffix], "to": pose["ankle" + suffix], "radius": 0.105})
		result.append({"key": "upperarm" + suffix, "from": pose["shoulder" + suffix], "to": pose["elbow" + suffix], "radius": 0.103})
		result.append({"key": "forearm" + suffix, "from": pose["elbow" + suffix], "to": pose["hand" + suffix], "radius": 0.078})
		result.append({"key": "hand" + suffix, "from": pose["hand" + suffix], "to": pose["hand" + suffix], "radius": 0.088})
		result.append({"key": "foot" + suffix, "from": Vector3(pose["ankle" + suffix]) + Vector3(0, 0, 0.02), "to": Vector3(pose["ankle" + suffix]) + Vector3(0, 0, -0.20), "radius": 0.105})
	return result
