extends RefCounted
## Surface ownership at door reveals. A jamb and the structural wall used to
## render the same plane. Keep the frame, subtract only its verified flat area
## from the wall skin. No transform, collision, door gap or light is changed.
const EPS:=0.00005

static func plane_key(axis:int,coordinate:float)->String:
	return "%d:%d"%[axis,roundi(coordinate/EPS)]

static func rectangles_from_frame(mesh:MeshInstance3D)->Array[Dictionary]:
	var grouped:Dictionary={}
	var faces:PackedVector3Array=mesh.mesh.get_faces()
	for index:int in range(0,faces.size(),3):
		var a:Vector3=mesh.global_transform*faces[index]
		var b:Vector3=mesh.global_transform*faces[index+1]
		var c:Vector3=mesh.global_transform*faces[index+2]
		var cross:Vector3=(b-a).cross(c-a)
		if cross.length_squared()<1e-16:continue
		var normal:Vector3=cross.normalized();var axis:int=normal.abs().max_axis_index()
		if absf(normal[axis])<.999999:continue
		var u:int=(axis+1)%3;var v:int=(axis+2)%3
		var start:=Vector2(minf(a[u],minf(b[u],c[u])),minf(a[v],minf(b[v],c[v])))
		var end:=Vector2(maxf(a[u],maxf(b[u],c[u])),maxf(a[v],maxf(b[v],c[v])))
		var rect:=Rect2(start,end-start)
		var plane:String=plane_key(axis,(a[axis]+b[axis]+c[axis])/3.0)
		var key:String=plane+":%d,%d,%d,%d"%[roundi(start.x/EPS),roundi(start.y/EPS),roundi(end.x/EPS),roundi(end.y/EPS)]
		if not grouped.has(key):grouped[key]={"plane":plane,"rect":rect,"area":0.0,"axis":axis,"coordinate":a[axis]}
		grouped[key].area+=cross.length()*.5
	var result:Array[Dictionary]=[]
	for group:Dictionary in grouped.values():
		var rect:Rect2=group.rect
		# Main box faces are two triangles covering this exact rectangle.
		# A bevel triangle alone only covers half: never erase its bounding box.
		if rect.get_area()>1e-8 and absf(float(group.area)-rect.get_area())<maxf(1e-7,rect.get_area()*.001):result.append(group)
	return result

static func subtract_rect(source:Rect2,cut:Rect2)->Array[Rect2]:
	var hit:Rect2=source.intersection(cut)
	if not hit.has_area():return [source]
	var result:Array[Rect2]=[]
	if hit.position.x>source.position.x:result.append(Rect2(source.position,Vector2(hit.position.x-source.position.x,source.size.y)))
	if hit.end.x<source.end.x:result.append(Rect2(Vector2(hit.end.x,source.position.y),Vector2(source.end.x-hit.end.x,source.size.y)))
	if hit.position.y>source.position.y:result.append(Rect2(Vector2(hit.position.x,source.position.y),Vector2(hit.size.x,hit.position.y-source.position.y)))
	if hit.end.y<source.end.y:result.append(Rect2(Vector2(hit.position.x,hit.end.y),Vector2(hit.size.x,source.end.y-hit.end.y)))
	return result

static func is_frame(mesh:MeshInstance3D,map_root:Node)->bool:
	var node:Node=mesh
	while node!=null and node!=map_root:
		if str(node.get_meta("authored_asset","")).contains("door_frame"):return true
		node=node.get_parent()
	return false

static func resolve(map_root:Node3D)->Dictionary:
	var started:=Time.get_ticks_usec()
	var masks:Dictionary={};var count:=0
	for node:Node in map_root.find_children("*","MeshInstance3D",true,false):
		var mesh:=node as MeshInstance3D
		if not is_frame(mesh,map_root):continue
		count+=1
		for face:Dictionary in rectangles_from_frame(mesh):
			if not masks.has(face.plane):masks[face.plane]=[]
			masks[face.plane].append(face.rect)
	var report:Dictionary={"frames":count,"wall_meshes":0,"removed_area_m2":0.0,"changed_faces":0,"triangles_before":0,"triangles_after":0}
	for node:Node in map_root.find_children("*","MeshInstance3D",true,false):
		var mesh:=node as MeshInstance3D
		if not mesh.mesh is BoxMesh:continue
		if str(mesh.get_meta("catalog_kind",""))!="wall" and not mesh.get_meta("frame_join_wall",false):continue
		var change:Dictionary=cut_wall(mesh,masks)
		if int(change.faces)>0:
			report.wall_meshes+=1;report.changed_faces+=change.faces;report.removed_area_m2+=change.area
			report.triangles_before+=12;report.triangles_after+=mesh.mesh.get_faces().size()/3
	report.elapsed_ms=float(Time.get_ticks_usec()-started)/1000.0
	map_root.set_meta("frame_joinery",report)
	return report

static func cut_wall(mesh:MeshInstance3D,masks:Dictionary)->Dictionary:
	var box:BoxMesh=mesh.mesh
	var bounds:=AABB(mesh.global_position-box.size*.5,box.size)
	var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var changed:=0;var removed:=0.0
	for axis:int in range(3):
		var u:int=(axis+1)%3;var v:int=(axis+2)%3
		for side:int in [-1,1]:
			var coordinate:float=bounds.position[axis] if side<0 else bounds.end[axis]
			var original:=Rect2(Vector2(bounds.position[u],bounds.position[v]),Vector2(bounds.size[u],bounds.size[v]))
			var rects:Array[Rect2]=[original]
			for cut:Rect2 in masks.get(plane_key(axis,coordinate),[]):
				var next:Array[Rect2]=[]
				for rect:Rect2 in rects:next.append_array(subtract_rect(rect,cut))
				rects=next
			var area:=0.0
			for rect:Rect2 in rects:area+=rect.get_area()
			if original.get_area()-area>1e-7:changed+=1;removed+=original.get_area()-area
			var normal:=Vector3.ZERO;normal[axis]=side
			for rect:Rect2 in rects:
				var corners:Array[Vector2]=[rect.position,Vector2(rect.end.x,rect.position.y),rect.end,Vector2(rect.position.x,rect.end.y)]
				# Cyclic axis choice gives +normal; Godot front faces are clockwise.
				var order:Array=[0,2,1,0,3,2] if side>0 else [0,1,2,0,2,3]
				for index:int in order:
					var p:=Vector3.ZERO;p[axis]=coordinate;p[u]=corners[index].x;p[v]=corners[index].y
					tool.set_normal(normal);tool.set_uv(corners[index]);tool.add_vertex(mesh.to_local(p))
	if changed>0:
		mesh.set_meta("frame_original_box",bounds);mesh.set_meta("frame_removed_area",removed)
		mesh.mesh=tool.commit()
	return {"faces":changed,"area":removed}
