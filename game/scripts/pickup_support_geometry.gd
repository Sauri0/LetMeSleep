class_name PickupSupportGeometry
extends RefCounted
## Pure support geometry shared by generated blueprints and rendering.
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

