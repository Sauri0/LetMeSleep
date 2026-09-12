extends RefCounted
## Repeated static fence geometry keeps its materials/shadows and exact pose.
## Spatial groups allow culling without submitting each plank separately.
static func build(root: Node3D) -> Dictionary:
	var groups: Dictionary = {}
	var sources := 0
	for placement: Node in root.get_children():
		if str(placement.get_meta("authored_asset","")) not in ["alfa_fence_panel","alfa_fence_post"]:
			continue
		for source: MeshInstance3D in placement.find_children("*","MeshInstance3D",true,false):
			if source.mesh == null: continue
			var transform: Transform3D = root.global_transform.affine_inverse()*source.global_transform
			var key := "%s/%d/%d/%d" % [source.mesh.get_instance_id(),floori(transform.origin.x/20.0),floori(transform.origin.z/20.0),source.cast_shadow]
			for surface: int in range(source.mesh.get_surface_count()):
				var material := source.get_active_material(surface)
				key += "/"+str(material.get_instance_id() if material else 0)
			if not groups.has(key): groups[key] = {"prototype":source,"transforms":[]}
			groups[key].transforms.append(transform)
			source.hide()
			sources += 1
	var batches := 0
	for group: Dictionary in groups.values():
		var prototype: MeshInstance3D = group.prototype
		var instance := MultiMeshInstance3D.new()
		var multimesh := MultiMesh.new()
		multimesh.transform_format = MultiMesh.TRANSFORM_3D
		# Duplicate just the mesh resource so per-instance material overrides are
		# retained; source meshes remain owned by their original prop nodes.
		var mesh: Mesh = prototype.mesh.duplicate()
		for surface: int in range(mesh.get_surface_count()):
			mesh.surface_set_material(surface,prototype.get_active_material(surface))
		multimesh.mesh = mesh
		multimesh.instance_count = group.transforms.size()
		for index: int in range(group.transforms.size()):
			multimesh.set_instance_transform(index,group.transforms[index])
		instance.multimesh = multimesh
		instance.cast_shadow = prototype.cast_shadow
		instance.name = "FenceBatch_%03d" % batches
		root.add_child(instance)
		batches += 1
	return {"source_meshes":sources,"batches":batches,"shadows_preserved":true}
