class_name HumanPose
extends RefCounted

const SWING_SECONDS := {"hands": 0.80, "swatter": 0.60, "racket": 1.05, "newspaper": 0.43, "broom": 1.20}
const SWING_GESTURE_SECONDS := 0.36
const INSPECT_ENTER := -0.70
const INSPECT_EXIT := -0.45
const VIEW_YAW_LIMIT := 75.0 * PI / 180.0
const HUMAN_PITCH_MIN := -1.92
const HUMAN_PITCH_MAX := 1.30
const ARM_REACH := 0.93
# Distance from grip to the visible striking face, not the end of the handle.
const TOOL_LENGTHS := {"hands": 0.0, "swatter": 0.46, "racket": 0.51, "newspaper": 0.30, "broom": 0.88}

static func body_yaw(actor: Dictionary) -> float:
	return float(actor.get("body_yaw", actor.get("yaw", 0.0)))

static func clamp_view_yaw(_actor: Dictionary, yaw: float, _pitch: float) -> float:
	# The neck's comfortable range controls torso follow, never mouse input.
	# Keep this shared entry point so input and manual attack use the same ray.
	return wrapf(yaw, -PI, PI)

static func apply_view(actor: Dictionary, yaw: float, pitch: float, dt: float = 0.0) -> void:
	if not is_finite(yaw) or not is_finite(pitch):
		return
	actor.body_yaw = body_yaw(actor)
	actor.pitch = clampf(pitch, HUMAN_PITCH_MIN, HUMAN_PITCH_MAX)
	actor.yaw = clamp_view_yaw(actor, yaw, actor.pitch)
	actor.inspecting = actor.pitch < INSPECT_ENTER or (bool(actor.get("inspecting", false)) and actor.pitch < INSPECT_EXIT)
	if dt > 0.0:
		var relative: float = wrapf(float(actor.yaw) - float(actor.body_yaw), -PI, PI)
		var follow: float = relative
		if bool(actor.inspecting):
			follow = signf(relative) * maxf(0.0, absf(relative) - VIEW_YAW_LIMIT)
		var turn: float = clampf(follow * (1.0 - exp(-12.0 * dt)), -dt * 6.0, dt * 6.0)
		actor.body_yaw = wrapf(float(actor.body_yaw) + turn, -PI, PI)

static func view_origin(actor: Dictionary) -> Vector3:
	var crouch: float = clampf(float(actor.get("crouch_amount", 0.0)), 0.0, 1.0)
	var local := Vector3(0, 1.63 - crouch * 0.51, -0.38 + crouch * 0.06)
	return Vector3(actor.get("p", Vector3.ZERO)) + local.rotated(Vector3.UP, body_yaw(actor))

static func view_direction(actor: Dictionary) -> Vector3:
	return Vector3.FORWARD.rotated(Vector3.RIGHT, float(actor.get("pitch", 0.0))).rotated(Vector3.UP, float(actor.get("yaw", 0.0)))

static func aim_angles(actor: Dictionary, point: Vector3) -> Vector2:
	var direction: Vector3 = (point - view_origin(actor)).normalized()
	var yaw: float = atan2(-direction.x, -direction.z)
	var pitch: float = asin(clampf(direction.y, -1.0, 1.0))
	var alternative_yaw: float = wrapf(yaw + PI, -PI, PI)
	var alternative_pitch: float = -PI - pitch
	if alternative_pitch >= HUMAN_PITCH_MIN and absf(wrapf(alternative_yaw - body_yaw(actor), -PI, PI)) < absf(wrapf(yaw - body_yaw(actor), -PI, PI)):
		yaw = alternative_yaw
		pitch = alternative_pitch
	return Vector2(yaw, pitch)

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
	var strike: Dictionary = actor.get("strike", {})
	var tool: String = str(strike.get("tool", actor.get("tool", "hands"))) if bool(strike.get("active", false)) else str(actor.get("tool", "hands"))
	var swing: float = sin(clampf(float(strike.get("progress", 1.0)), 0.0, 1.0) * PI) if bool(strike.get("active", false)) else 0.0
	var torso := Vector3(0, 1.09 - crouch * 0.45, crouch * 0.06)
	var head := Vector3(0, 1.55 - crouch * 0.55, -crouch * 0.02)
	var result: Dictionary = {
		"torso": torso, "head": head, "eye": (view_origin(actor) - Vector3(actor.get("p", Vector3.ZERO))).rotated(Vector3.UP, -body_yaw(actor)),
		"pelvis": Vector3(0, 0.73 - crouch * 0.25, crouch * 0.10),
		"head_basis": Basis(Vector3.UP, clampf(wrapf(float(actor.get("yaw", 0.0)) - body_yaw(actor), -PI, PI), -VIEW_YAW_LIMIT, VIEW_YAW_LIMIT)) * Basis(Vector3.RIGHT, clampf(float(actor.get("pitch", 0.0)), HUMAN_PITCH_MIN, HUMAN_PITCH_MAX) * 0.25),
		"torso_height": lerpf(0.68, 0.52, crouch),
	}
	for side: float in [-1.0, 1.0]:
		var suffix: String = "_l" if side < 0.0 else "_r"
		var step: float = gait * (-side)
		var lift: float = lift_l if side < 0.0 else lift_r
		var tuck: float = 0.0 if grounded else 1.0
		result["hip" + suffix] = Vector3(side * 0.135, 0.75 - crouch * 0.25, -0.11 + crouch * 0.10)
		result["knee" + suffix] = Vector3(side * 0.135, 0.42 - crouch * 0.12 + lift * 0.5 + tuck * 0.04, -0.11 - crouch * 0.12 + step * 0.6 - tuck * 0.055)
		result["ankle" + suffix] = Vector3(side * 0.135, 0.10 + lift + tuck * 0.05, -step)
		result["shoulder" + suffix] = Vector3(side * 0.31, 1.30 - crouch * 0.45, crouch * 0.06)
		var elbow := Vector3(side * 0.25, 1.09 - crouch * 0.45, -0.24 + step * 0.015)
		var hand := Vector3(side * 0.23, 0.89 - crouch * 0.40, -0.41 + step * 0.015)
		if bool(actor.get("relaxed_pose",false)):
			# Lobby/editor presentation only: no bite reservations or combat
			# happen there. Active-round pose/contact contracts stay unchanged.
			result["shoulder"+suffix].x = side*.28
			elbow = Vector3(side*.31,1.02-crouch*.45,.015+step*.8)
			hand = Vector3(side*.31,.75-crouch*.40,-.015+step*1.5)
		var active_hand: bool = str(strike.get("hand", "right")) == ("left" if side < 0 else "right")
		if active_hand and swing > 0.0:
			var contact: Vector3 = (Vector3(strike.point) - Vector3(actor.get("p", Vector3.ZERO))).rotated(Vector3.UP, -body_yaw(actor))
			var shoulder: Vector3 = result["shoulder" + suffix]
			var direction: Vector3 = (contact - shoulder).normalized()
			var target_hand: Vector3 = contact - direction * float(TOOL_LENGTHS.get(tool, 0.0))
			target_hand = shoulder + (target_hand - shoulder).limit_length(ARM_REACH)
			var target_elbow: Vector3 = shoulder.lerp(target_hand, 0.50) + Vector3(side * 0.08, -0.06, -0.10)
			elbow = elbow.lerp(target_elbow, swing)
			hand = hand.lerp(target_hand, swing)
			if side > 0.0:
				result.tool_direction = Vector3.DOWN.slerp(direction, swing).normalized()
		result["elbow" + suffix] = elbow
		result["hand" + suffix] = hand
	if not result.has("tool_direction"):
		result.tool_direction = Vector3.DOWN
	var suffix: String = "_l" if str(strike.get("hand", "right")) == "left" else "_r"
	result.strike_contact = Vector3(result["hand" + suffix]) + Vector3(result.tool_direction) * float(TOOL_LENGTHS.get(tool, 0.0))
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
		if _striking_limb(actor, str(capsule.key)) and not str(capsule.key).begins_with(surface_key):
			continue
		var hit: Dictionary = ray_capsule(outside, inside, capsule)
		if not hit.is_empty() and float(hit.distance) < first_distance:
			first_distance = float(hit.distance)
			local_point = hit.p
			local_normal = hit.normal
	var yaw: float = body_yaw(actor)
	return {"p": Vector3(actor.get("p", Vector3.ZERO)) + local_point.rotated(Vector3.UP, yaw), "normal": local_normal.rotated(Vector3.UP, yaw).normalized(), "label": str(zone.get("label", "Zona"))}

static func _striking_limb(actor: Dictionary, key: String) -> bool:
	var strike: Dictionary = actor.get("strike", {})
	var suffix: String = "_l" if str(strike.get("hand", "right")) == "left" else "_r"
	return bool(strike.get("active", false)) and key in ["upperarm" + suffix, "forearm" + suffix, "hand" + suffix]

static func ray_body(actor: Dictionary, from: Vector3, to: Vector3, own_view: bool = false, ignore_striking_limb: bool = false) -> Dictionary:
	var position: Vector3 = actor.get("p", Vector3.ZERO)
	var yaw: float = body_yaw(actor)
	var local_from: Vector3 = (from - position).rotated(Vector3.UP, -yaw)
	var local_to: Vector3 = (to - position).rotated(Vector3.UP, -yaw)
	var first: Dictionary = {}
	for capsule: Dictionary in collision_segments(actor):
		if own_view and str(capsule.key) == "head":
			continue
		if ignore_striking_limb and _striking_limb(actor, str(capsule.key)):
			continue
		var hit: Dictionary = ray_capsule(local_from, local_to, capsule)
		if not hit.is_empty() and (first.is_empty() or float(hit.distance) < float(first.distance)):
			first = hit
			first.key = capsule.key
	if not first.is_empty():
		first.p = position + Vector3(first.p).rotated(Vector3.UP, yaw)
		first.normal = Vector3(first.normal).rotated(Vector3.UP, yaw)
	return first

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
