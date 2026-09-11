extends SceneTree
const Joinery=preload("res://scripts/frame_joinery.gd")
func _initialize()->void:run.call_deferred()
func run()->void:
	var world:Node3D=load("res://scripts/world.gd").new()
	root.add_child(world);world.build();world.load_map(load("res://scripts/procedural_house.gd").map_id(1))
	var planes:Dictionary={}
	for mesh:MeshInstance3D in world.map_root.find_children("*","MeshInstance3D",true,false):
		if str(mesh.get_meta("catalog_kind",""))!="wall" and not mesh.get_meta("frame_join_wall",false):continue
		var bounds:AABB=mesh.global_transform*mesh.mesh.get_aabb()
		for face:Dictionary in Joinery.rectangles_from_frame(mesh):
			var side:=signf(float(face.coordinate)-bounds.get_center()[int(face.axis)])
			var key:String=face.plane+":"+str(side)
			if not planes.has(key):planes[key]=[]
			planes[key].append({"path":str(mesh.get_path()),"rect":face.rect,"bounds":str(bounds),"skin":mesh.get_meta("frame_join_wall",false)})
	var overlaps:Array[Dictionary]=[]
	for key:String in planes:
		var faces:Array=planes[key]
		for i:int in range(faces.size()):
			for j:int in range(i+1,faces.size()):
				if faces[i].path==faces[j].path:continue
				var area:float=Rect2(faces[i].rect).intersection(faces[j].rect).get_area()
				if area>.0001:overlaps.append({"plane":key,"area":area,"a":faces[i],"b":faces[j]})
	overlaps.sort_custom(func(a:Dictionary,b:Dictionary)->bool:return a.area>b.area)
	print("WALL_DUPLICATES ",overlaps.size())
	for entry:Dictionary in overlaps.slice(0,12): print(JSON.stringify(entry))
	world.queue_free();await process_frame;await process_frame;quit()
