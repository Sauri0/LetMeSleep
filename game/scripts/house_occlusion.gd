extends RefCounted
## Static occluders use the final rendered wall/floor triangles. Doorways,
## windows and stair openings remain holes; moving leaves are never baked.
## This only reduces hidden rendering, not simulation, shadows or light range.
static func build(map_root:Node3D)->Dictionary:
	var existing:Node=map_root.get_node_or_null("HouseOcclusion")
	if existing!=null:return Dictionary(existing.get_meta("geometry",{}))
	var started:=Time.get_ticks_usec()
	var vertices:=PackedVector3Array();var indices:=PackedInt32Array();var count:=0
	for mesh:MeshInstance3D in map_root.find_children("*","MeshInstance3D",true,false):
		if str(mesh.get_meta("catalog_kind","")) not in ["wall","floor"]:continue
		if mesh.mesh==null or not mesh.visible:continue
		var transform:Transform3D=map_root.global_transform.affine_inverse()*mesh.global_transform
		for vertex:Vector3 in mesh.mesh.get_faces():
			indices.append(vertices.size());vertices.append(transform*vertex)
		count+=1
	var node:=OccluderInstance3D.new();node.name="HouseOcclusion"
	var occluder:=ArrayOccluder3D.new();occluder.set_arrays(vertices,indices);node.occluder=occluder
	var report:Dictionary={"meshes":count,"triangles":vertices.size()/3,"build_ms":float(Time.get_ticks_usec()-started)/1000.0}
	node.set_meta("geometry",report);map_root.add_child(node)
	return report
