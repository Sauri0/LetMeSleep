extends SceneTree
const Doors=preload("res://scripts/door_catalog.gd")
var checks:=0
var failures:=0
var transitions:=false
func _initialize()->void:run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("OCCLUSION07_FAIL "+label)
func settle()->void:
	for frame:int in range(7):await process_frame
	await RenderingServer.frame_post_draw
func run()->void:
	if DisplayServer.get_name()=="headless":quit(1);return
	root.size=Vector2i(1280,720)
	transitions="--transitions" in OS.get_cmdline_user_args()
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world);world.build();world.load_map("house")
	var occluder:OccluderInstance3D=world.map_root.get_node("HouseOcclusion")
	var expected:=PackedVector3Array()
	for mesh:MeshInstance3D in world.map_root.find_children("*","MeshInstance3D",true,false):
		if str(mesh.get_meta("catalog_kind","")) not in ["wall","floor"]:continue
		for vertex:Vector3 in mesh.mesh.get_faces():expected.append(mesh.global_transform*vertex)
	check(expected==occluder.occluder.get_vertices(),"occluder exactly matches final static walls/floors with all openings")
	var identity:int=occluder.get_instance_id();world.load_map("house")
	check(identity==world.map_root.get_node("HouseOcclusion").get_instance_id(),"same map does not rebuild occlusion")
	var camera:=Camera3D.new();world.add_child(camera);camera.near=.025;camera.fov=76;camera.make_current()
	var views:Array[Dictionary]=[
		{"p":Vector3(0,1.62,6.5),"to":Vector3(0,1.62,-6)},
		{"p":Vector3(-1.7,1.62,-7.4),"to":Vector3(-5.5,1.3,-7.4)},
		{"p":Vector3(-8,1.62,-6.5),"to":Vector3(-11,.9,-10)},
		{"p":Vector3(-11,1.62,3.2),"to":Vector3(-11,3,-2.2)},
		{"p":Vector3(0,4.82,6.5),"to":Vector3(0,4.82,-6)},
		{"p":Vector3(3.15,4.82,6.25),"to":Vector3(6,4.2,8.6)},
		{"p":Vector3(-8.7,4.85,-6.4),"to":Vector3(-12,4.1,-9.6)},
		{"p":Vector3(11,4.82,-3),"to":Vector3(11,2.8,2)},
		{"p":Vector3(-4.5,1.62,-7.4),"to":Vector3(0,1.62,-7.4)},
		{"p":Vector3(4.5,4.82,7.4),"to":Vector3(0,4.82,7.4)},
		{"p":Vector3(1.7,4.82,7.4),"to":Vector3(5.5,4.82,7.4)}]
	var folder:String=ProjectSettings.globalize_path("res://../outputs/0.7-rendimiento/oclusion")
	DirAccess.make_dir_recursive_absolute(folder)
	var results:Array=[]
	var angles:Array=[0.0,PI/4,PI/2]
	if transitions:angles=[0.0,PI/12,PI/6,PI/4,PI/3,5*PI/12,PI/2,5*PI/12,PI/3,PI/4,PI/6,PI/12,0.0]
	for angle:float in angles:
		var states:Dictionary={}
		for id:String in Doors.get_doors():states[id]={"angle":angle,"target_angle":angle,"moving":false,"blocked":false,"revision":1}
		world.sync_doors(states,1.0)
		for i:int in range(views.size()):
			if transitions and i not in [1,8,9,10]:continue
			camera.position=views[i].p;camera.look_at(views[i].to)
			root.use_occlusion_culling=false;await settle()
			var before:Image=root.get_texture().get_image();before.convert(Image.FORMAT_RGB8)
			root.use_occlusion_culling=true;await settle()
			var after:Image=root.get_texture().get_image();after.convert(Image.FORMAT_RGB8)
			var metrics:Dictionary=before.compute_image_metrics(after,false)
			# Godot reports byte-domain metrics (white/black max=255), already
			# scaled; multiplying by255 would reject subpixel raster differences.
			var mean:float=float(metrics.mean)
			var rmse:float=float(metrics.root_mean_squared)
			check(mean<.2 and rmse<2.0,"same visible image view%d door%.0f mean=%.4f rmse=%.4f"%[i,rad_to_deg(angle),mean,rmse])
			results.append({"view":i,"door_degrees":rad_to_deg(angle),"mean_abs_byte":mean,"rmse_byte":rmse})
			if mean>=.2 or rmse>=2.0 or angle==PI/4:
				before.save_png(folder.path_join("view%d-door%d-before.png"%[i,roundi(rad_to_deg(angle))]))
				after.save_png(folder.path_join("view%d-door%d-after.png"%[i,roundi(rad_to_deg(angle))]))
	world.load_map("lobby");check(not root.use_occlusion_culling,"lobby resets viewport occlusion")
	var file:=FileAccess.open(folder.path_join("transitions.json" if transitions else "equivalence.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"views":views,"results":results,"checks":checks,"failures":failures},"\t"));file.close()
	world.queue_free();await process_frame;await process_frame
	print("OCCLUSION07_RESULT checks=%d failures=%d"%[checks,failures]);quit(failures)
