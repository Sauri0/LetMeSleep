extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
var output:=""
var map_id:=Generator.map_id(1)
var records:Array[Dictionary]=[]
var failures:Array[String]=[]
func _initialize()->void:
	for argument:String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="):output=argument.trim_prefix("--output=")
		if argument.begins_with("--map-id="):map_id=argument.trim_prefix("--map-id=")
	run.call_deferred()
func capture(camera:Camera3D,name:String,at:Vector3,target:Vector3)->void:
	camera.position=at;camera.look_at(target)
	for frame:int in range(8):await process_frame
	await RenderingServer.frame_post_draw
	var image:=root.get_texture().get_image()
	var path:=output.path_join(name+".png")
	var error:=image.save_png(path)
	if error!=OK:failures.append("save "+name+": "+error_string(error));return
	records.append({"file":name+".png","camera":[at.x,at.y,at.z],"target":[target.x,target.y,target.z],
		"sha256":FileAccess.get_sha256(path),"pixels":[image.get_width(),image.get_height()]})
func run()->void:
	if DisplayServer.get_name()=="headless":printerr("ENVIRONMENT091_CAPTURE_FAIL native renderer required");quit(1);return
	if output.is_empty():printerr("ENVIRONMENT091_CAPTURE_FAIL --output required");quit(1);return
	output=ProjectSettings.globalize_path(output).simplify_path()
	if DirAccess.make_dir_recursive_absolute(output)!=OK:printerr("ENVIRONMENT091_CAPTURE_FAIL output directory");quit(1);return
	root.size=Vector2i(1920,1080)
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT;root.content_scale_size=Vector2i(1920,1080)
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world);world.build();world.load_map(map_id)
	if world.current_map!=map_id:printerr("ENVIRONMENT091_CAPTURE_FAIL map rejected: "+map_id);quit(1);return
	var camera:=Camera3D.new();root.add_child(camera);camera.near=.05;camera.far=60;camera.fov=74;camera.make_current()
	for stair:Dictionary in world.map_data.stair_connections:
		var bottom:Vector3=stair.bottom;var top:Vector3=stair.top;var center:Vector3=(bottom+top)*.5
		await capture(camera,str(stair.id)+"-up",bottom+Vector3.UP*1.55,center+Vector3.UP*.65)
		await capture(camera,str(stair.id)+"-down",top+Vector3.UP*1.55,center+Vector3.UP*.65)
	for route:Dictionary in world.map_data.circulation_routes:
		if str(route.kind) not in ["central","stair_side"]:continue
		var from:Vector3=route.from;var to:Vector3=route.to
		await capture(camera,str(route.id)+"-"+str(route.kind),from+Vector3.UP*1.55,to+Vector3.UP*1.25)
	var report:Dictionary={"schema":1,"map_id":map_id,"generator_version":world.map_data.generator_version,
		"resolution":[root.content_scale_size.x,root.content_scale_size.y],"renderer":RenderingServer.get_current_rendering_method(),
		"rendering_driver":RenderingServer.get_current_rendering_driver_name(),"adapter":RenderingServer.get_video_adapter_name(),
		"stair_connections":world.map_data.stair_connections.size(),"circulation_routes":world.map_data.circulation_routes.size(),
		"lighting":world.map_root.get_meta("generated_stair_lighting",{}),"captures":records,"failures":failures,
		"visual_review_status":"pending","scope":"Native source captures for stair lighting, joins and finish clearance; capture success is not visual approval."}
	var manifest:=output.path_join("manifest.json")
	var file:=FileAccess.open(manifest,FileAccess.WRITE)
	if file==null:failures.append("manifest open")
	else:file.store_string(JSON.stringify(report,"\t"));file.close()
	print("ENVIRONMENT091_CAPTURE map=%s captures=%d failures=%d output=%s"%[map_id,records.size(),failures.size(),output])
	camera.queue_free();world.queue_free();await process_frame;await process_frame
	quit(0 if failures.is_empty() else 1)
