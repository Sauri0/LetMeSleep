extends SceneTree
## Native visual witness for every catalog option from front/profile/back.
## Pixel evidence still requires human review; this fixture only certifies that
## each requested option rendered, stayed finite and preserved gameplay shapes.
const Preview=preload("res://scripts/avatar_preview.gd")
const Cosmetics=preload("res://scripts/cosmetics.gd")
var checks:=0
var failures: Array[String]=[]
var output:=""
var preview:Control
var records: Array[Dictionary]=[]

func _initialize()->void:
	for argument:String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="):output=argument.trim_prefix("--output=")
	_run.call_deferred()

func check(ok:bool,label:String)->bool:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<=30:printerr("REVIEW091_PREVIEW_FAIL "+label)
	return ok

func _shape_signature()->String:
	var result:Array=[]
	var keys:Array=preview.avatar.pose_colliders.keys();keys.sort()
	for key:Variant in keys:
		var body:StaticBody3D=preview.avatar.pose_colliders[key]
		var shape:Shape3D=body.get_child(0).shape
		var row:Array=[str(key),body.position,str(shape.get_class())]
		if shape is CapsuleShape3D:row.append_array([shape.radius,shape.height])
		elif shape is SphereShape3D:row.append(shape.radius)
		elif shape is BoxShape3D:row.append(shape.size)
		result.append(row)
	return JSON.stringify(result)

func _finite_meshes()->Dictionary:
	var visible:=0
	var finite:=true
	for node:Node in preview.avatar.find_children("*","MeshInstance3D",true,false):
		var mesh:=node as MeshInstance3D
		if not mesh.visible or mesh.mesh==null:continue
		visible+=1
		var bounds:=mesh.mesh.get_aabb()
		finite=finite and bounds.position.is_finite() and bounds.end.is_finite() and mesh.global_position.is_finite() and is_finite(mesh.global_basis.determinant())
	return {"visible":visible,"finite":finite}

func _capture(role:String,category:String,index:int,view:String)->Dictionary:
	preview.set_view(view)
	for frame:int in range(3):await process_frame
	await RenderingServer.frame_post_draw
	var image:Image=preview.viewport.get_texture().get_image()
	var filename:="%s-%s-%02d-%s.png"%[role,category,index,view]
	var path:=output.path_join(filename)
	var saved:=image!=null and not image.is_empty() and image.save_png(path)==OK
	check(saved,"%s %s option%d %s capture saved"%[role,category,index,view])
	return {"view":view,"file":filename,"sha256":FileAccess.get_sha256(path) if saved else "","size":image.get_size() if image!=null else Vector2i.ZERO}

func _option(role:String,base:Dictionary,category:String,index:int,shape_baseline:String)->void:
	var appearance:=base.duplicate(true)
	appearance[category]=index
	preview.set_avatar(role,appearance)
	preview.focus_category(category)
	preview.set_process(false)
	var actual:Dictionary=preview.appearance
	check(int(actual.get(category,-1))==index,"%s %s option%d reaches preview"%[role,category,index])
	var mesh_report:=_finite_meshes()
	check(bool(mesh_report.finite) and int(mesh_report.visible)>0,"%s %s option%d has finite visible geometry"%[role,category,index])
	check(_shape_signature()==shape_baseline,"%s %s option%d preserves gameplay collider signature"%[role,category,index])
	var captures:Array=[]
	for view:String in ["front","side","back"]:captures.append(await _capture(role,category,index,view))
	records.append({"role":role,"category":category,"option":index,"name":Cosmetics.option_names(role,category)[index],"appearance":appearance,"visible_meshes":mesh_report.visible,"captures":captures})

func _role(role:String,profile:Dictionary)->void:
	var base:Dictionary=profile[role].duplicate(true)
	preview.set_avatar(role,base)
	preview.set_process(false)
	var shape_baseline:=_shape_signature()
	var first_hashes:Dictionary={}
	for category:String in Cosmetics.category_keys(role):
		var hashes:Dictionary={}
		for index:int in range(Cosmetics.option_count(role,category)):
			await _option(role,base,category,index,shape_baseline)
			var record:Dictionary=records[-1]
			var front:Dictionary=record.captures[0]
			check(not str(front.sha256).is_empty(),"%s %s option%d has readable front evidence"%[role,category,index])
			check(not hashes.has(front.sha256),"%s %s option%d is visually distinct in stable front pose"%[role,category,index])
			hashes[front.sha256]=index
		first_hashes[category]=hashes.keys()
	check(first_hashes.size()==Cosmetics.category_keys(role).size(),role+" complete category catalog captured")

func _run()->void:
	if output.is_empty() or DisplayServer.get_name()=="headless":
		printerr("REVIEW091_PREVIEW_FAIL requires native renderer and --output")
		quit(2);return
	var report_file:=output.path_join("review091-preview-results.json")
	if FileAccess.file_exists(report_file):
		printerr("REVIEW091_PREVIEW_FAIL evidence path already contains a report")
		quit(2);return
	DirAccess.make_dir_recursive_absolute(output)
	root.size=Vector2i(720,720)
	preview=Preview.new()
	preview.size=Vector2(720,720)
	root.add_child(preview)
	await process_frame
	var defaults:=Cosmetics.default_profile()
	check(int(defaults.human.outfit)==0 and int(defaults.human.footwear)==0 and int(defaults.human.accessory)==3,"new human defaults to classic pajamas slippers and nightcap")
	check(Cosmetics.option_names("human","outfit")[0]=="Pijama clásico" and Cosmetics.option_names("human","footwear")[0]=="Pantuflas clásicas" and Cosmetics.option_names("human","accessory")[3]=="Gorro de noche","default IDs retain approved labels")
	check(defaults.human.keys().size()==Cosmetics.category_keys("human").size() and defaults.mosquito.keys().size()==Cosmetics.category_keys("mosquito").size(),"default profile has exact role-specific allowlists")
	await _role("human",defaults)
	await _role("mosquito",defaults)
	var report:Dictionary={"checks":checks,"failures":failures,"records":records,"captures":records.size()*3,"scope":"native AvatarPreview, every option front/side/back, finite visible meshes and invariant gameplay collider signature; image approval/intersection review pending; no UI navigation, authority, network, FPS or WAN claim","renderer":RenderingServer.get_current_rendering_method(),"adapter":RenderingServer.get_video_adapter_name(),"source_sha256":{}}
	for path:String in ["res://tests/review091_preview_views.gd","res://scripts/avatar_preview.gd","res://scripts/actor_view.gd","res://scripts/cosmetics.gd"]:
		report.source_sha256[path]=FileAccess.get_sha256(path)
	var file:=FileAccess.open(report_file,FileAccess.WRITE)
	if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
	else:check(false,"preview report writable")
	for failure:String in failures:print("REVIEW091_PREVIEW_DIAGNOSTIC "+failure)
	print("REVIEW091_PREVIEW_RESULT checks=%d failures=%d records=%d captures=%d"%[checks,failures.size(),records.size(),records.size()*3])
	preview.queue_free();await process_frame
	quit.call_deferred(0 if failures.is_empty() else 1)
