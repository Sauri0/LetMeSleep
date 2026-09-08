extends RefCounted
## Diagnostic only: identical static geometry instanced in bounded spatial cells.
static func apply(map_root:Node3D)->Dictionary:
	var groups:Dictionary={}
	for mesh:MeshInstance3D in map_root.find_children("*","MeshInstance3D",true,false):
		if mesh.mesh==null or mesh.mesh.get_surface_count()!=1 or not mesh.visible:continue
		var owner:Node=mesh;var allowed:=false
		while owner!=map_root:
			if str(owner.get_meta("authored_asset","")) in ["molding","rail_bar","rail_post"]:allowed=true;break
			owner=owner.get_parent()
		if not allowed:continue
		var material:Material=mesh.get_active_material(0)
		var cell:Vector3i=Vector3i((mesh.global_position/3.0).floor())
		var key:String=str(mesh.mesh.get_rid())+":"+str(material.get_rid())+":"+str(cell)+":"+str(mesh.cast_shadow)+":"+str(mesh.layers)
		if not groups.has(key):groups[key]=[]
		groups[key].append(mesh)
	var before:=0;var after:=0
	for members:Array in groups.values():
		if members.size()<2:continue
		var first:MeshInstance3D=members[0]
		var batch:=MultiMeshInstance3D.new();batch.name="StaticBatchProbe"
		batch.material_override=first.get_active_material(0);batch.cast_shadow=first.cast_shadow;batch.layers=first.layers
		batch.multimesh=MultiMesh.new();batch.multimesh.transform_format=MultiMesh.TRANSFORM_3D
		batch.multimesh.mesh=first.mesh;batch.multimesh.instance_count=members.size()
		map_root.add_child(batch)
		for i:int in range(members.size()):
			batch.multimesh.set_instance_transform(i,map_root.global_transform.affine_inverse()*members[i].global_transform)
			members[i].visible=false
		before+=members.size();after+=1
	return {"before":before,"after":after}
