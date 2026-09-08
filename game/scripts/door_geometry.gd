extends RefCounted
## Exact oriented leaf predicates shared by blueprint validation and runtime.
const GAP := 0.14
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

