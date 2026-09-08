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

## Exact value signature for an owning Simulation's bounded cache. No shared
## mutable actor state or hash-only equality is kept in this static pose module.
static func cache_key(actor: Dictionary) -> Array:
	var key: Array = []
	for field: String in ["p","yaw","body_yaw","pitch","crouch_amount","motion_phase","motion_speed","grounded","sprinting","motion_blend","air_blend","land_blend","motion_stride","motion_direction","pose_time","tool","relaxed_pose"]:
		key.append(actor.get(field,null))
	key.append(Dictionary(actor.get("strike",{})).duplicate(true))
	return key

## Updated by authority after collision resolution. These small public values are
## the animation state; renderers never invent a second pose for a bite surface.
static func advance_motion(actor: Dictionary, displacement: Vector3, dt: float, was_grounded: bool) -> void:
	if dt <= 0.0:
		return
	var distance := Vector2(displacement.x,displacement.z).length()
	var speed := distance / dt
	var grounded: bool = bool(actor.get("grounded",true))
	var crouch := clampf(float(actor.get("crouch_amount",0.0)),0.0,1.0)
	actor.pose_time = fposmod(float(actor.get("pose_time",0.0))+dt,TAU*100.0)
	actor.motion_speed = speed
	actor.motion_blend = move_toward(float(actor.get("motion_blend",0.0)),clampf(speed/.65,0.0,1.0),dt*(7.0 if speed>.1 else 5.0))
	var cycle := lerpf(1.55 if bool(actor.get("sprinting",false)) else 1.15,.72,crouch)
	actor.motion_stride = lerpf(float(actor.get("motion_stride",cycle)),cycle,1.0-exp(-10.0*dt))
	actor.motion_phase = float(actor.get("motion_phase",0.0))
	if grounded:
		actor.motion_phase = fposmod(float(actor.motion_phase)+distance*TAU/float(actor.motion_stride),TAU)
	var local_travel := Vector3(displacement.x,0,displacement.z).rotated(Vector3.UP,-body_yaw(actor))
	var direction: Vector3 = actor.get("motion_direction",Vector3.FORWARD)
	if distance>.00001:
		direction = direction.lerp(local_travel.normalized(),1.0-exp(-18.0*dt)).normalized()
	actor.motion_direction = direction
	actor.air_blend = move_toward(float(actor.get("air_blend",0.0)),0.0 if grounded else 1.0,dt*(10.0 if grounded else 7.0))
	var landing := float(actor.get("land_blend",0.0))
	if grounded and not was_grounded:
		landing = clampf(absf(float(actor.get("_pose_vertical_speed",0.0)))/4.6,.15,1.0)
	actor.land_blend = move_toward(landing,0.0,dt*3.8)
	actor._pose_vertical_speed = Vector3(actor.get("velocity",Vector3.ZERO)).y
	var turn := wrapf(body_yaw(actor)-float(actor.get("_pose_body_yaw",body_yaw(actor))),-PI,PI)/dt
	actor._pose_body_yaw = body_yaw(actor)
	actor.turn_blend = lerpf(float(actor.get("turn_blend",0.0)),clampf(turn/6.0,-1.0,1.0),1.0-exp(-9.0*dt))

## The contact interval remains inside the authoritative .08–.25 second window.
## A short preparation, fast palm arrival, tiny hold and soft recovery replace
## the old symmetric sine, without modifying the requested world-space point.
static func strike_weight(progress: float) -> float:
	if progress < .14:
		return 0.0
	if progress < .42:
		return smoothstep(.14,.42,progress)
	if progress < .50:
		return 1.0
	return 1.0-smoothstep(.50,1.0,progress)

static func _foot_cycle(phase: float, duty: float, distance: float, height: float) -> Vector3:
	var t := fposmod(phase/TAU,1.0)
	var span := distance*duty
	if t<duty:
		# During support this derivative cancels the actor's forward travel.
		return Vector3(span*.5-distance*t,0,0)
	var swing := (t-duty)/(1.0-duty)
	return Vector3(lerpf(-span*.5,span*.5,smoothstep(0.0,1.0,swing)),sin(swing*PI)*height,sin(swing*PI)*.16)

## Solve the grip and striking face together. All returned points are actor-local.
## The requested ray point remains fixed whenever the arm and shaft can reach it.
static func strike_geometry(actor: Dictionary, point_world: Vector3, normal_world: Vector3, hand: String, tool: String) -> Dictionary:
	var side := -1.0 if hand=="left" else 1.0
	var crouch := clampf(float(actor.get("crouch_amount",0.0)),0.0,1.0)
	var shoulder := Vector3(side*(.28 if bool(actor.get("relaxed_pose",false)) else .285),1.30-crouch*.45,crouch*.06)
	var point := (point_world-Vector3(actor.get("p",Vector3.ZERO))).rotated(Vector3.UP,-body_yaw(actor))
	var normal := normal_world.rotated(Vector3.UP,-body_yaw(actor)).normalized()
	if normal.length_squared()<.5:
		normal = -view_direction(actor).rotated(Vector3.UP,-body_yaw(actor))
	var length := float(TOOL_LENGTHS.get(tool,0.0))
	var direct := (point-shoulder).normalized()
	if direct.length_squared()<.5: direct = Vector3.DOWN
	var tangent := (point-shoulder)-normal*(point-shoulder).dot(normal)
	if tangent.length_squared()<.0001:
		tangent = Vector3.DOWN-normal*Vector3.DOWN.dot(normal)
	if tangent.length_squared()<.0001: tangent = Vector3.RIGHT
	var direction := tangent.normalized() if length>0.0 else direct
	if (point-direction*length).distance_to(shoulder)>ARM_REACH and length>0.0:
		var low := 0.0
		var high := 1.0
		for iteration: int in range(14):
			var fraction := (low+high)*.5
			var candidate := direction.slerp(direct,fraction).normalized()
			if (point-candidate*length).distance_to(shoulder)>ARM_REACH: low = fraction
			else: high = fraction
		direction = direction.slerp(direct,high).normalized()
	var grip := point-direction*length
	var reachable := grip.distance_to(shoulder)<=ARM_REACH+.0001
	grip = shoulder+(grip-shoulder).limit_length(ARM_REACH)
	var face_normal := normal-direction*normal.dot(direction)
	var neutral_normal := Basis(Quaternion(Vector3.DOWN,direction))*Vector3.BACK
	if face_normal.length_squared()<.0001: face_normal = neutral_normal
	face_normal = face_normal.normalized()
	if face_normal.dot(neutral_normal)<0.0: face_normal = -face_normal
	return {"hand":grip,"elbow":shoulder.lerp(grip,.50)+Vector3(side*.08,-.06,-.10),"direction":direction,"normal":face_normal,"contact":grip+direction*length,"reachable":reachable}

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
	var blend := clampf(float(actor.get("motion_blend",speed)),0.0,1.0)
	var air := clampf(float(actor.get("air_blend",0.0 if grounded else 1.0)),0.0,1.0)
	var landing := clampf(float(actor.get("land_blend",0.0)),0.0,1.0)
	var stride := float(actor.get("motion_stride",lerpf(1.55 if running else 1.15,.72,crouch)))
	var duty := lerpf(.32 if running else .44,.50,crouch)
	var gait_direction: Vector3 = actor.get("motion_direction",Vector3.FORWARD)
	var pose_time := float(actor.get("pose_time",0.0))
	var settle := -.012*blend*(.5+.5*cos(phase*2.0))-.025*landing
	var breath := sin(pose_time*1.7)*.003*(1.0-blend)
	var strike: Dictionary = actor.get("strike", {})
	var tool: String = str(strike.get("tool", actor.get("tool", "hands"))) if bool(strike.get("active", false)) else str(actor.get("tool", "hands"))
	var progress := clampf(float(strike.get("progress",1.0)),0.0,1.0)
	var swing: float = strike_weight(progress) if bool(strike.get("active", false)) else 0.0
	var torso := Vector3(0, 1.09 - crouch * 0.45+settle+breath, crouch * 0.06)
	var head := Vector3(0, 1.55 - crouch * 0.55, -crouch * 0.02)
	var result: Dictionary = {
		"torso": torso, "head": head, "eye": (view_origin(actor) - Vector3(actor.get("p", Vector3.ZERO))).rotated(Vector3.UP, -body_yaw(actor)),
		"pelvis": Vector3(0, 0.73 - crouch * 0.25+settle, crouch * 0.10),
		"head_basis": Basis(Vector3.UP, clampf(wrapf(float(actor.get("yaw", 0.0)) - body_yaw(actor), -PI, PI), -VIEW_YAW_LIMIT, VIEW_YAW_LIMIT)) * Basis(Vector3.RIGHT, clampf(float(actor.get("pitch", 0.0)), HUMAN_PITCH_MIN, HUMAN_PITCH_MAX) * 0.25),
		"torso_height": lerpf(0.68, 0.52, crouch),
		"torso_basis": Basis(Vector3.UP,sin(phase)*blend*(1.0-air)*.028),
	}
	for side: float in [-1.0, 1.0]:
		var suffix: String = "_l" if side < 0.0 else "_r"
		var foot := _foot_cycle(phase+(PI if side>0 else 0.0),duty,stride,lerpf(.11 if running else .075,.050,crouch))
		var step := foot.x*blend*(1.0-air)
		var lift := foot.y*blend*(1.0-air)
		var travel := gait_direction*step
		result["hip" + suffix] = Vector3(side * 0.135, 0.75 - crouch * 0.25+settle, -0.11 + crouch * 0.10)
		result["knee" + suffix] = Vector3(side * 0.135, 0.42 - crouch * 0.12+settle*.5 + lift * 0.5 + air * 0.04, -0.11 - crouch * 0.12 - air * .055)+travel*.35
		result["ankle" + suffix] = Vector3(side * 0.135, 0.10 + lift + air * .065, 0)+travel
		result["foot_direction"+suffix] = Vector3.FORWARD.rotated(Vector3.RIGHT,foot.z*blend*(1.0-air))
		result["shoulder" + suffix] = Vector3(side * .285, 1.30 - crouch * 0.45, crouch * 0.06)
		var counter_swing := sin(phase+(PI if side>0 else 0.0))*blend*(1.0-air)
		var elbow := Vector3(side * 0.25, 1.065 - crouch * 0.45+settle*.4+counter_swing*.03, -.22 + step * .13)
		var hand := Vector3(side * 0.23, .845 - crouch * 0.40+settle*.35+counter_swing*.045, -.41 + step * .12)
		if bool(actor.get("relaxed_pose",false)):
			# Lobby/editor presentation only: no bite reservations or combat
			# happen there. Active-round pose/contact contracts stay unchanged.
			result["shoulder"+suffix].x = side*.28
			elbow = Vector3(side*.31,1.02-crouch*.45+absf(step)*.10,.015+step*.45)
			hand = Vector3(side*.31,.75-crouch*.40+absf(step)*.22,-.015+step*.8)
		var active_hand: bool = str(strike.get("hand", "right")) == ("left" if side < 0 else "right")
		if active_hand and bool(strike.get("active",false)) and progress<.14:
			var anticipation := sin(progress/.14*PI)
			hand += Vector3(side*.009,.018,.012)*anticipation
			elbow += Vector3(side*.005,.010,.010)*anticipation
		if active_hand and swing > 0.0:
			var geometry := strike_geometry(actor,strike.point,strike.get("normal",Vector3.ZERO),str(strike.get("hand","right")),tool)
			elbow = elbow.lerp(geometry.elbow, swing)
			hand = hand.lerp(geometry.hand, swing)
			if side > 0.0:
				var up: Vector3 = -Vector3(geometry.direction)
				var back: Vector3 = geometry.normal
				var target_basis := Basis(up.cross(back).normalized(),up,back)
				var current_basis := Basis(Quaternion.IDENTITY.slerp(target_basis.get_rotation_quaternion(),swing))
				result.tool_direction = -current_basis.y
				result.tool_normal = current_basis.z
		result["elbow" + suffix] = elbow
		result["hand" + suffix] = hand
	if not result.has("tool_direction"):
		result.tool_direction = Vector3.DOWN
		result.tool_normal = Vector3.BACK
	var suffix: String = "_l" if str(strike.get("hand", "right")) == "left" else "_r"
	result.strike_contact = Vector3(result["hand" + suffix]) + Vector3(result.tool_direction) * float(TOOL_LENGTHS.get(tool, 0.0))
	return result

static func zone_pose(actor: Dictionary, zone: Dictionary, posed: Dictionary = {}, shared_capsules: Array[Dictionary] = [], rest_pose: Dictionary = {}) -> Dictionary:
	var pose: Dictionary = sample(actor) if posed.is_empty() else posed
	var rest: Dictionary = sample({}) if rest_pose.is_empty() else rest_pose
	var bone: String = str(zone.get("bone", "torso"))
	var anchor: Vector3 = _anchor(pose, bone)
	var rest_anchor: Vector3 = _anchor(rest, bone)
	var delta_basis: Basis = _basis(pose, bone) * _basis(rest, bone).inverse()
	var local_point: Vector3 = anchor + delta_basis * (Vector3(zone.p) - rest_anchor)
	var local_normal: Vector3 = delta_basis * (Vector3.BACK if bool(zone.get("rear", false)) else Vector3.FORWARD)
	var capsules: Array[Dictionary] = collision_segments(actor,pose) if shared_capsules.is_empty() else shared_capsules
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
		var along: float = clampf((local_point-Vector3(capsule.from)).dot(segment)/maxf(segment.length_squared(),.000001),0.0,1.0)
		if bone.begins_with("forearm_"):
			# A forearm mark belongs on its shaft, not on the inflated wrist cap
			# reached by projecting a distant front seed along a bent arm.
			along = .83
		var center: Vector3 = Vector3(capsule.from)+segment*along
		local_normal = local_point-center
		if bone.begins_with("forearm_"):
			# The inner-front face stays within the comfortable inspection cone
			# through a stride, so following the point does not turn it away.
			local_normal = local_normal.normalized()+Vector3(-signf(center.x)*.20,0,0)
			local_normal -= segment.normalized()*local_normal.dot(segment.normalized())
		local_normal = local_normal.normalized()
		local_point = center + local_normal * float(capsule.radius)
		break
	# Resolve actual interpenetration at joined capsules. A separate arm crossing
	# an outward ray must not relocate an abdomen reservation across an air gap.
	for iteration: int in range(4):
		var changed := false
		for capsule: Dictionary in capsules:
			if _striking_limb(actor,str(capsule.key)) and not str(capsule.key).begins_with(surface_key): continue
			var axis: Vector3 = Vector3(capsule.to)-Vector3(capsule.from)
			var closest: Vector3 = Vector3(capsule.from)+axis*clampf((local_point-Vector3(capsule.from)).dot(axis)/maxf(axis.length_squared(),.000001),0.0,1.0)
			if local_point.distance_to(closest)>=float(capsule.radius)-.0001: continue
			var hit: Dictionary = ray_capsule(local_point+local_normal*.50,local_point,capsule)
			if not hit.is_empty():
				local_point = hit.p
				local_normal = hit.normal
				changed = true
		if not changed: break
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
	if bone=="torso":
		return pose.get("torso_basis",Basis.IDENTITY)
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

static func collision_segments(actor: Dictionary, posed: Dictionary = {}) -> Array[Dictionary]:
	var pose: Dictionary = sample(actor) if posed.is_empty() else posed
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
		var foot_direction: Vector3 = pose.get("foot_direction"+suffix,Vector3.FORWARD)
		result.append({"key": "foot" + suffix, "from": Vector3(pose["ankle" + suffix])-foot_direction*.02, "to": Vector3(pose["ankle" + suffix])+foot_direction*.20, "radius": 0.105})
	return result
