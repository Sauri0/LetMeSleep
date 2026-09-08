# Frozen pre-optimization DoorState reference for differential tests only.
# Original source SHA256: 26A1C4A8DF4535C7BEFCF35647522986159FE324BC5DE8ED6F619CA18E73FE7C
extends RefCounted
## Per-room authority. No mutable singleton, physics server, or shared cache.
const Catalog = preload("res://scripts/door_catalog.gd")
const Pose = preload("res://scripts/human_pose.gd")
const ArenaData = preload("res://scripts/arena.gd")
const TURN_SPEED := 1.75
const COOLDOWN := 0.5
const SWEEP_TIP_STEP := 0.02
var definitions: Dictionary = {}
var states: Dictionary = {}
var last_toggle: Dictionary = {}

func reset(map_id: String = "house") -> void:
	definitions = Catalog.get_doors(map_id)
	states.clear()
	last_toggle.clear()
	for id: String in definitions:
		states[id] = {"angle":Catalog.OPEN_ANGLE,"target_angle":Catalog.OPEN_ANGLE,"moving":false,"blocked":false,"revision":0}

func toggle(id: String, now: float) -> bool:
	if not states.has(id) or not is_finite(now) or now-float(last_toggle.get(id,-100.0)) < COOLDOWN:
		return false
	var state: Dictionary = states[id]
	if bool(state.moving) and not bool(state.blocked):
		return false
	state.target_angle = 0.0 if float(state.target_angle)>Catalog.OPEN_ANGLE*0.5 else Catalog.OPEN_ANGLE
	state.moving = not is_equal_approx(float(state.angle),float(state.target_angle))
	state.blocked = false
	state.revision = int(state.revision)+1
	last_toggle[id] = now
	return true

func step(dt: float, actors: Dictionary) -> void:
	if not is_finite(dt) or dt<=0.0:
		return
	for id: String in states:
		var state: Dictionary = states[id]
		if is_equal_approx(float(state.angle),float(state.target_angle)):
			state.moving = false
			state.blocked = false
			continue
		state.blocked = false
		var definition: Dictionary = definitions[id]
		var change: float = minf(TURN_SPEED*minf(dt,0.05),absf(float(state.target_angle)-float(state.angle)))
		var parts: int = maxi(1,int(ceil(change*float(definition.width)/SWEEP_TIP_STEP)))
		for part: int in range(parts):
			var candidate: float = move_toward(float(state.angle),float(state.target_angle),change/float(parts))
			if _occupied(definition,candidate,actors):
				state.blocked = true
				if float(state.target_angle)<float(state.angle):
					state.target_angle = Catalog.OPEN_ANGLE
					state.revision = int(state.revision)+1
				break
			state.angle = candidate
		state.moving = not is_equal_approx(float(state.angle),float(state.target_angle))

func _occupied(definition: Dictionary, angle: float, actors: Dictionary) -> bool:
	for actor: Dictionary in actors.values():
		if not bool(actor.get("alive",false)):
			continue
		var position: Vector3 = actor.p
		var human: bool = actor.role=="human"
		var radius: float = 0.60 if human else 0.04
		var height: float = lerpf(1.95,1.40,float(actor.get("crouch_amount",0.0))) if human else radius*2.0
		var body := AABB(position+Vector3(-radius,0 if human else -radius,-radius),Vector3(radius*2,height,radius*2))
		if Catalog.intersects_body(definition,angle,body):
			return true
		if human and Catalog.intersects_body(definition,angle,ArenaData.human_envelope(actor)):
			# An extended hand/foot still belongs to the occupant. Swept closing
			# checks include the shared animated capsules beyond the travel box.
			for capsule: Dictionary in Pose.collision_segments(actor):
				var from: Vector3 = position+Vector3(capsule.from).rotated(Vector3.UP,Pose.body_yaw(actor))
				var to: Vector3 = position+Vector3(capsule.to).rotated(Vector3.UP,Pose.body_yaw(actor))
				var limb_box := AABB(from,Vector3.ZERO).expand(to).grow(float(capsule.radius))
				if Catalog.intersects_body(definition,angle,limb_box):
					return true
	return false

func snapshot() -> Dictionary:
	return states.duplicate(true)
