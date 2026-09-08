class_name PickupSupports
extends RefCounted
const SupportGeometry = preload("res://scripts/pickup_support_geometry.gd")
const Maps = preload("res://scripts/map_catalog.gd")

## Four small domestic supports. Each part is shared by rendering and collision.
## No bodies are created here: World owns its static colliders, Arena its boxes.
static func definitions(map_id: String = "house") -> Array:
	return Maps.get_map(map_id).get("pickup_supports", []) if Maps.is_playable(map_id) else []

static func parts(spec: Dictionary) -> Array[Dictionary]:
	return SupportGeometry.parts(spec)

static func get_boxes(map_id: String = "house") -> Array[AABB]:
	var result: Array[AABB] = []
	for spec: Dictionary in definitions(map_id):
		for part: Dictionary in parts(spec):
			var box: AABB = part.box
			box.position += Vector3(spec.origin)
			result.append(box)
	return result

static func build(parent: Node3D, map_id: String = "house") -> void:
	for spec: Dictionary in definitions(map_id):
		var node := Node3D.new()
		node.name = "PickupSupport_" + str(spec.id)
		node.position = spec.origin
		parent.add_child(node)
		var size := Vector3(.48,1.09,.24) if str(spec.kind)=="broom_rack" else Vector3(.9,.34,float(spec.get("depth",.4)))
		node.set_meta("catalog_kind","pickup_support")
		node.set_meta("catalog_box",AABB(spec.origin,size))
		node.set_meta("pickup_support",str(spec.id))
		for part: Dictionary in parts(spec):
			var instance := MeshInstance3D.new()
			instance.name = str(part.name)
			var box: AABB = part.box
			var mesh := BoxMesh.new()
			mesh.size = box.size
			instance.mesh = mesh
			instance.position = box.get_center()
			var material := StandardMaterial3D.new()
			material.albedo_color = part.color
			material.roughness = .84
			instance.material_override = material
			instance.set_meta("pickup_contact_surface",bool(part.contact))
			node.add_child(instance)
