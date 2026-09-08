class_name DoorCatalog
extends RefCounted
## Immutable geometry only. Angles and reservations belong to each simulation.
const GAP := 0.14
const THICKNESS := 0.07
const HEIGHT := 2.45
const OPEN_ANGLE := PI * 0.5
const DEFINITIONS := {
	"kitchen": {"id":"kitchen", "label":"Cocina", "hinge":Vector3(-2.14,0,-7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":PI*0.5, "open_sign":1.0},
	"dining": {"id":"dining", "label":"Comedor", "hinge":Vector3(2.14,0,-7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":PI*0.5, "open_sign":-1.0},
	"entrance": {"id":"entrance", "label":"Recibidor", "hinge":Vector3(-2.14,0,7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":-PI*0.5, "open_sign":-1.0},
	"guest": {"id":"guest", "label":"Cuarto de visitas", "hinge":Vector3(2.14,0,7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":-PI*0.5, "open_sign":1.0},
	"blue_bedroom": {"id":"blue_bedroom", "label":"Dormitorio azul", "hinge":Vector3(-2.14,3.2,-7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":PI*0.5, "open_sign":1.0},
	"sewing": {"id":"sewing", "label":"Taller de costura", "hinge":Vector3(2.14,3.2,-7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":PI*0.5, "open_sign":-1.0},
	"games": {"id":"games", "label":"Sala de juegos", "hinge":Vector3(-2.14,3.2,7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":-PI*0.5, "open_sign":-1.0},
	"pink_bedroom": {"id":"pink_bedroom", "label":"Dormitorio rosa", "hinge":Vector3(2.14,3.2,7.1), "width":2.2, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":-PI*0.5, "open_sign":1.0},
	"bathroom": {"id":"bathroom", "label":"Baño", "hinge":Vector3(-8.14,3.2,-7.2), "width":2.0, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":PI*0.5, "open_sign":1.0},
	"pantry": {"id":"pantry", "label":"Despensa", "hinge":Vector3(8.14,0,7.2), "width":2.0, "height":HEIGHT, "thickness":THICKNESS, "closed_yaw":-PI*0.5, "open_sign":1.0},
}

static func get_doors(map_id: String = "house") -> Dictionary:
	return DEFINITIONS.duplicate(true) if map_id == "house" else {}

static func leaf_transform(definition: Dictionary, angle: float) -> Transform3D:
	return Transform3D(Basis(Vector3.UP, float(definition.closed_yaw) + float(definition.open_sign) * angle), definition.hinge)

static func leaf_box(definition: Dictionary) -> AABB:
	return AABB(Vector3(0,GAP,-float(definition.thickness)*0.5), Vector3(float(definition.width),float(definition.height)-GAP,float(definition.thickness)))

static func handle_point(definition: Dictionary, angle: float) -> Vector3:
	return leaf_transform(definition,angle) * Vector3(float(definition.width)*0.85,1.08,0)

static func intersects_body(definition: Dictionary, angle: float, body: AABB) -> bool:
	# Exact separating axes for a vertical oriented leaf against an axis-aligned
	# body. Unlike its enclosing AABB, this does not block empty air at45degrees.
	var transform := leaf_transform(definition,angle)
	var local := leaf_box(definition)
	var center: Vector3 = transform * local.get_center()
	var half: Vector3 = local.size*0.5
	var body_center: Vector3 = body.get_center()
	var body_half: Vector3 = body.size*0.5
	var delta: Vector3 = body_center-center
	if absf(delta.y) >= half.y+body_half.y-0.000001:
		return false
	for axis: Vector3 in [Vector3.RIGHT,Vector3.BACK,transform.basis.x,transform.basis.z]:
		var leaf_extent: float = absf(axis.dot(transform.basis.x))*half.x+absf(axis.dot(transform.basis.z))*half.z
		var body_extent: float = absf(axis.x)*body_half.x+absf(axis.z)*body_half.z
		if absf(delta.dot(axis)) >= leaf_extent+body_extent-0.000001:
			return false
	return true

static func body_blocked(body: AABB, states: Dictionary, map_id: String = "house") -> bool:
	if map_id != "house":
		return false
	for id: String in states:
		if DEFINITIONS.has(id) and intersects_body(DEFINITIONS[id],float(states[id].get("angle",OPEN_ANGLE)),body):
			return true
	return false

static func ray_leaf(definition: Dictionary, angle: float, from: Vector3, to: Vector3, padding: float = 0.0) -> Dictionary:
	var transform := leaf_transform(definition,angle)
	var inverse := transform.affine_inverse()
	var box := leaf_box(definition).grow(maxf(0.0,padding))
	var hit: Variant = box.intersects_segment(inverse*from,inverse*to)
	if hit == null:
		return {}
	var local_hit: Vector3 = hit
	var normal := Vector3.ZERO
	var closest := INF
	for axis: int in range(3):
		for upper: bool in [false,true]:
			var edge: float = box.end[axis] if upper else box.position[axis]
			if absf(local_hit[axis]-edge) < closest:
				closest = absf(local_hit[axis]-edge)
				normal = Vector3.ZERO
				normal[axis] = 1.0 if upper else -1.0
	var point: Vector3 = transform*local_hit
	return {"p":point,"normal":transform.basis*normal,"distance":from.distance_to(point),"kind":"door","door_id":definition.id}

static func ray_doors(from: Vector3, to: Vector3, states: Dictionary, map_id: String = "house", padding: float = 0.0) -> Dictionary:
	var nearest: Dictionary = {}
	if map_id != "house":
		return nearest
	for id: String in states:
		if not DEFINITIONS.has(id):
			continue
		var hit := ray_leaf(DEFINITIONS[id],float(states[id].get("angle",OPEN_ANGLE)),from,to,padding)
		if not hit.is_empty() and (nearest.is_empty() or float(hit.distance)<float(nearest.distance)):
			nearest = hit
	return nearest
