class_name MosquitoPose
extends RefCounted
## Impact anatomy in world metres; independent of the .04 m locomotion sphere.
## Measured from selected B GLB at its .35 runtime scale. Decorative limbs,
## wings, antennae and proboscis deliberately do not contribute collision.
const VISUAL_SCALE := .35
const LOCAL_SEGMENTS := [
	{"key":"thorax", "from":Vector3.ZERO, "to":Vector3.ZERO, "radius":.04},
	{"key":"head", "from":Vector3(0,.004,-.030), "to":Vector3(0,.014,-.038), "radius":.037},
	{"key":"abdomen", "from":Vector3(0,-.002,.0358), "to":Vector3(0,-.0105,.0968), "radius":.016},
]
const BOUND_RADIUS := .114
const CONTACT_EPSILON := .000001
const VIEW_PITCH_LIMIT := PI*.5
const SURFACE_PITCH_LIMIT := 1.35

static func local_segments() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for capsule: Dictionary in LOCAL_SEGMENTS: result.append(capsule.duplicate())
	return result

static func surface_basis(normal: Vector3, forward: Vector3) -> Basis:
	var up := normal.normalized() if normal.is_finite() and normal.length_squared()>.5 else Vector3.UP
	var tangent := forward.slide(up) if forward.is_finite() else Vector3.ZERO
	if tangent.length_squared()<.001: tangent = Vector3.FORWARD.slide(up)
	if tangent.length_squared()<.001: tangent = Vector3.UP.slide(up)
	tangent = tangent.normalized()
	return Basis(tangent.cross(up).normalized(),up,-tangent)

static func surface_view_direction(normal: Vector3, forward: Vector3, pitch: float) -> Vector3:
	return (surface_basis(normal,forward)*Basis(Vector3.RIGHT,clampf(pitch,-SURFACE_PITCH_LIMIT,SURFACE_PITCH_LIMIT))*Vector3.FORWARD).normalized()

static func view_angles(direction: Vector3, fallback_yaw: float=0.0) -> Dictionary:
	var unit := direction.normalized()
	var yaw := atan2(-unit.x,-unit.z) if Vector2(unit.x,unit.z).length_squared()>.00000001 else wrapf(fallback_yaw,-PI,PI)
	return {"yaw":yaw,"pitch":asin(clampf(unit.y,-1.0,1.0))}

## A pure public-state orientation. No frame history, assignments or RNG.
static func orientation(actor: Dictionary) -> Basis:
	var yaw_basis := Basis(Vector3.UP,float(actor.get("yaw",0.0)))
	var state := str(actor.get("state","flying"))
	var normal: Vector3 = actor.get("surface_normal",Vector3.ZERO)
	var surface_forward: Vector3 = actor.get("surface_forward",Vector3.ZERO)
	if state=="perched" and normal.length_squared()>.5 and surface_forward.length_squared()>.5:
		return surface_basis(normal,surface_forward)
	if state in ["biting","perched"] and normal.length_squared()>.5:
		var up := normal.normalized()
		var forward := yaw_basis*Vector3.FORWARD
		forward -= up*forward.dot(up)
		if forward.length_squared()<.001:
			forward = Vector3.UP-up*Vector3.UP.dot(up)
		if forward.length_squared()<.001:
			forward = Vector3.RIGHT-up*Vector3.RIGHT.dot(up)
		forward = forward.normalized()
		return Basis(forward.cross(up).normalized(),up,-forward)
	if state=="stunned":
		return yaw_basis*Basis(Vector3.FORWARD,-1.25)
	var velocity: Vector3 = yaw_basis.inverse()*Vector3(actor.get("velocity",Vector3.ZERO))
	return yaw_basis*Basis.from_euler(Vector3(clampf(velocity.z*.065,-.32,.32)+float(actor.get("pitch",0.0))*.18,0.0,clampf(-velocity.x*.065,-.38,.38)))

static func collision_segments(actor: Dictionary) -> Array[Dictionary]:
	return segments_at(Vector3(actor.get("p",Vector3.ZERO)),orientation(actor))

static func segments_at(position: Vector3, basis: Basis) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for capsule: Dictionary in LOCAL_SEGMENTS:
		result.append({"key":capsule.key,"from":position+basis*Vector3(capsule.from),"to":position+basis*Vector3(capsule.to),"radius":capsule.radius})
	return result

static func closest_axis(point: Vector3, a: Vector3, b: Vector3) -> Vector3:
	var axis := b-a
	return a+axis*clampf((point-a).dot(axis)/maxf(axis.length_squared(),.000000001),0.0,1.0)

## Closest anatomical point inside the finite manual ray's tool corridor.
## Sets depth only: neither the direction nor the point on the ray is rotated.
static func ray_candidates(capsules: Array[Dictionary], eye: Vector3, direction: Vector3, reach: float, tool_radius: float) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	var end := eye+direction*reach
	for capsule: Dictionary in capsules:
		var pair := Geometry3D.get_closest_points_between_segments(eye,end,capsule.from,capsule.to)
		if pair[0].distance_to(pair[1])>tool_radius+float(capsule.radius): continue
		var along: float = (Vector3(pair[1])-eye).dot(direction)
		if along<0.0 or along>reach: continue
		var toward_eye := (eye-Vector3(pair[1])).normalized()
		result.append({"axis":pair[1],"visible":Vector3(pair[1])+toward_eye*float(capsule.radius),"along":along,"radius":capsule.radius})
	return result

## Continuous sphere / linearly moving capsule contact in relative space.
## Conservative advancement uses a proven endpoint-speed distance bound; the
## 1 micrometre tolerance only terminates convergence at tangency. Simulation
## subdivides rotating anatomy with the same <=8 ms gesture intervals.
static func swept_contact(face_a: Vector3, face_b: Vector3, before: Dictionary, after: Dictionary, tool_radius: float) -> Dictionary:
	var a0 := Vector3(before.from)-face_a
	var b0 := Vector3(before.to)-face_a
	var da := Vector3(after.from)-face_b-a0
	var db := Vector3(after.to)-face_b-b0
	var radius: float = tool_radius+float(after.radius)
	var speed: float = maxf(da.length(),db.length())
	var fraction := 0.0
	for iteration: int in range(64):
		var axis := closest_axis(Vector3.ZERO,a0+da*fraction,b0+db*fraction)
		var distance := axis.length()
		if distance<=radius+CONTACT_EPSILON:
			var face := face_a.lerp(face_b,fraction)
			var centre := face+axis
			var surface := centre-axis.normalized()*float(after.radius)
			return {"fraction":fraction,"axis":centre,"surface":surface,"radius":after.radius}
		if speed<.000000001: return {}
		var advance: float = (distance-radius)/speed
		if fraction+advance>1.0: return {}
		fraction += advance
	return {}
