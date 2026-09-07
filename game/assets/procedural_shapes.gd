extends RefCounted
## Original bevelled geometry. Rounded edges stay inside the supplied dimensions.
static var cache: Dictionary = {}

static func rounded(parent: Node3D, at: Vector3, size: Vector3, radius: float, material: Material) -> MeshInstance3D:
	var key: String = str(size)+":"+str(radius)
	if not cache.has(key):
		cache[key] = _rounded_mesh(size,radius)
	var node := MeshInstance3D.new()
	node.mesh = cache[key]
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node

static func _rounded_mesh(size: Vector3, radius: float) -> ArrayMesh:
	var half: Vector3 = size*0.5
	var bevel: float = minf(radius,minf(half.x,minf(half.y,half.z))*0.95)
	var core: Vector3 = half-Vector3.ONE*bevel
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for axis: int in range(3):
		var u_axis: int = (axis+1)%3
		var v_axis: int = (axis+2)%3
		var us: PackedFloat32Array = _coordinates(half[u_axis],bevel)
		var vs: PackedFloat32Array = _coordinates(half[v_axis],bevel)
		for side: float in [-1.0,1.0]:
			for row: int in range(6):
				for col: int in range(6):
					var order: Array[Vector2i] = [Vector2i(col,row),Vector2i(col,row+1),Vector2i(col+1,row),Vector2i(col+1,row),Vector2i(col,row+1),Vector2i(col+1,row+1)]
					if side<0.0:
						order.reverse()
					for coordinate: Vector2i in order:
						var point := Vector3.ZERO
						point[axis] = half[axis]*side
						point[u_axis] = us[coordinate.x]
						point[v_axis] = vs[coordinate.y]
						var clamped: Vector3 = point.clamp(-core,core)
						var normal: Vector3 = (point-clamped).normalized()
						surface.set_normal(normal)
						surface.set_uv(Vector2(point[u_axis]/size[u_axis]+0.5,point[v_axis]/size[v_axis]+0.5))
						surface.add_vertex(clamped+normal*bevel)
	return surface.commit()

static func _coordinates(half: float, radius: float) -> PackedFloat32Array:
	return PackedFloat32Array([-half,-half+radius*0.293,-half+radius,0.0,half-radius,half-radius*0.293,half])
