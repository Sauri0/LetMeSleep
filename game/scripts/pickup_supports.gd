class_name PickupSupports
extends RefCounted
const Maps = preload("res://scripts/map_catalog.gd")

## Four small domestic supports. Each part is shared by rendering and collision.
## No bodies are created here: World owns its static colliders, Arena its boxes.
static func definitions(map_id: String = "house") -> Array:
	return Maps.HOUSE.get("pickup_supports", []) if map_id == "house" else []

static func _part(name: String, box: AABB, color: String, contact: bool = false) -> Dictionary:
	return {"name":name,"box":box,"color":Color(color),"contact":contact}

static func parts(spec: Dictionary) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if str(spec.kind) == "broom_rack":
		result.append(_part("Bandeja",AABB(Vector3.ZERO,Vector3(.48,.035,.24)),"9c7455",true))
		result.append(_part("Respaldo",AABB(Vector3(.09,.21,.215),Vector3(.30,.88,.025)),"b99368"))
		result.append(_part("ClipFondo",AABB(Vector3(.19,.964,.164),Vector3(.10,.032,.051)),"477b77"))
		result.append(_part("ClipIzquierdo",AABB(Vector3(.19,.964,.055),Vector3(.018,.032,.109)),"477b77"))
		result.append(_part("ClipDerecho",AABB(Vector3(.272,.964,.055),Vector3(.018,.032,.109)),"477b77"))
		result.append(_part("LabioIzquierdo",AABB(Vector3(0,.035,0),Vector3(.025,.026,.24)),"ba9369"))
		result.append(_part("LabioDerecho",AABB(Vector3(.455,.035,0),Vector3(.025,.026,.24)),"ba9369"))
	else:
		result.append(_part("Asiento",AABB(Vector3(0,.29,0),Vector3(.9,.05,.40)),"ba9369",true))
		result.append(_part("Estante",AABB(Vector3(.05,.09,.035),Vector3(.8,.035,.33)),"9c7455"))
		for x: float in [.055,.78]:
			for z: float in [.04,.285]:
				result.append(_part("Pata",AABB(Vector3(x,0,z),Vector3(.065,.29,.075)),"77573f"))
		result.append(_part("Travesano",AABB(Vector3(.075,.22,.31),Vector3(.75,.045,.055)),"9c7455"))
		var depth_scale:float=float(spec.get("depth",.4))/.4
		for part:Dictionary in result:
			var box:AABB=part.box
			box.position.z*=depth_scale;box.size.z*=depth_scale
			part.box=box
	return result

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
