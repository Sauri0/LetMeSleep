# Frozen body-query baseline; only class_name removed.
# Original source SHA256: 995AEE9689DEBC47357BF984F010B178FEC2256CA50A30E5A734CE542C5B4FF7
extends RefCounted
const Geometry=preload("res://scripts/door_geometry.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const GeometryCache=preload("res://scripts/geometry_cache.gd")
static var _cache_order: Array[String]=[]
static var _generated: Dictionary={}
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
	return _definitions(map_id).duplicate(true)

static func _definitions(map_id: String) -> Dictionary:
	if map_id=="house": return DEFINITIONS
	GeometryCache.touch(_cache_order,map_id,24,[_generated])
	if not _generated.has(map_id): _generated[map_id]=Maps.get_map(map_id).get("doors",{})
	return _generated[map_id]

static func leaf_transform(definition: Dictionary, angle: float) -> Transform3D:
	return Geometry.leaf_transform(definition,angle)

static func leaf_box(definition: Dictionary) -> AABB:
	return Geometry.leaf_box(definition)

static func handle_point(definition: Dictionary, angle: float) -> Vector3:
	return Geometry.handle_point(definition,angle)

static func intersects_body(definition: Dictionary, angle: float, body: AABB) -> bool:
	return Geometry.intersects_body(definition,angle,body)

static func body_blocked(body: AABB, states: Dictionary, map_id: String = "house") -> bool:
	var definitions:=_definitions(map_id)
	for id: String in states:
		if definitions.has(id) and intersects_body(definitions[id],float(states[id].get("angle",OPEN_ANGLE)),body):
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
	var definitions:=_definitions(map_id)
	for id: String in states:
		if not definitions.has(id):
			continue
		var hit := ray_leaf(definitions[id],float(states[id].get("angle",OPEN_ANGLE)),from,to,padding)
		if not hit.is_empty() and (nearest.is_empty() or float(hit.distance)<float(nearest.distance)):
			nearest = hit
	return nearest
