extends SceneTree
## Measures the actual deformed selected mesh using an isolated concave collider.
const CharacterSkin=preload("res://assets/art/characters/shared/character_skin.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Simulation=preload("res://scripts/simulation.gd")
var use_selected:=true
var verify:=false
var checks:=0
var failures:=0
var folder:String
var rows:Array[Dictionary]=[]
func _initialize()->void:_run.call_deferred()
func _run()->void:
	root.size=Vector2i(1280,720)
	folder=ProjectSettings.globalize_path("res://../outputs/0.7-integracion/selected-audit")
	for arg:String in OS.get_cmdline_user_args():
		if arg=="--production":use_selected=false
		if arg=="--verify":verify=true
		if arg.begins_with("--output="):folder=arg.trim_prefix("--output=")
	DirAccess.make_dir_recursive_absolute(folder)
	var skin:=CharacterSkin.new();root.add_child(skin)
	var path:="res://assets/art/samples07/characters/A/human/"
	skin.setup("human",path+"human_lms06.glb" if use_selected else "",path+"rig_contract.json" if use_selected else "")
	skin.set_appearance({"color":1,"accent":4,"face":1,"hair":0,"outfit":0,"accessory":3,"footwear":0})
	var env_node:=WorldEnvironment.new();var env:=Environment.new();env.background_mode=Environment.BG_COLOR;env.background_color=Color("293f50");env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;env.ambient_light_color=Color("bed2dc");env.ambient_light_energy=.5;env_node.environment=env;root.add_child(env_node)
	var key:=DirectionalLight3D.new();key.rotation_degrees=Vector3(-40,-145,0);key.light_energy=.65;root.add_child(key)
	var camera:=Camera3D.new();root.add_child(camera);camera.near=.02;camera.fov=40;camera.make_current()
	var body:=StaticBody3D.new();body.collision_layer=8;body.collision_mask=0;root.add_child(body)
	var shape:=CollisionShape3D.new();body.add_child(shape)
	var label:=Label.new();label.position=Vector2(14,14);label.add_theme_font_size_override("font_size",23);root.add_child(label)
	var postures:Array[String]=["stand","walk","run","crouch","crouch_run","jump","land"]
	for case_index:int in range(postures.size()*3):
		var outfit:int=case_index/postures.size()
		var posture:String=postures[case_index%postures.size()]
		skin.set_appearance({"color":1,"accent":4,"face":1,"hair":0,"outfit":outfit,"accessory":3,"footwear":0})
		var data:Dictionary={"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"grounded":posture!="jump","state":"human","tool":"hands","pose_time":.5,"preview_only":true,"facial_preview":"neutral","velocity":Vector3(0,2.0 if posture=="jump" else 0,0),"motion_phase":1.2 if posture=="walk" else 2.4,"motion_speed":5.0 if posture in ["run","crouch_run"] else 3.1 if posture=="walk" else 0.0,"sprinting":posture in ["run","crouch_run"],"crouch_amount":1.0 if posture in ["crouch","crouch_run"] else 0.0,"land_blend":1.0 if posture=="land" else 0.0}
		skin.set_first_person(false);skin.apply_human(Pose.sample(data),data,1.0)
		for frame:int in range(3):await process_frame
		camera.position=Vector3(1.5,1.3,-3.4);camera.look_at(Vector3(0,.94,0));label.text=("Humano A elegido" if use_selected else "Producción")+" · "+posture+" · atuendo "+str(outfit)+" · malla real"
		if DisplayServer.get_name()!="headless":
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(folder.path_join("outfit%d-"%outfit+posture+".png"))
		skin.set_first_person(true)
		var faces:=PackedVector3Array()
		for mesh:MeshInstance3D in skin.meshes:
			if not mesh.visible:continue
			var baked:ArrayMesh=mesh.bake_mesh_from_current_skeleton_pose()
			for point:Vector3 in baked.get_faces():faces.append(mesh.global_transform*point)
		var concave:=ConcavePolygonShape3D.new();concave.backface_collision=true;concave.set_faces(faces);shape.shape=concave
		await physics_frame;await process_frame
		for zone_id:int in range(Simulation.BODY_ZONES.size()):
			var zone:Dictionary=Pose.zone_pose(data,Simulation.BODY_ZONES[zone_id])
			var from:Vector3=zone.p+zone.normal*.30
			var query:=PhysicsRayQueryParameters3D.create(from,zone.p-zone.normal*.15,8)
			var hit:Dictionary=body.get_world_3d().direct_space_state.intersect_ray(query)
			var offset:float=from.distance_to(hit.position)-.30 if not hit.is_empty() else INF
			var target:Vector3=zone.p+zone.normal*Simulation.ATTACH_OFFSET
			query=PhysicsRayQueryParameters3D.create(Pose.view_origin(data),target,8)
			var own_hit:Dictionary=body.get_world_3d().direct_space_state.intersect_ray(query)
			var visible:=own_hit.is_empty() or Pose.view_origin(data).distance_to(own_hit.position)>=Pose.view_origin(data).distance_to(target)-.015
			rows.append({"posture":posture,"outfit":outfit,"zone":zone_id,"surface_offset_m":offset if is_finite(offset) else 999.0,"own_target_visible":visible,"triangles":faces.size()/3})
			if verify:
				checks+=1
				if not is_finite(offset) or offset<-.007 or offset>.015:failures+=1
				if zone_id<8:
					checks+=1
					if not visible:failures+=1
			print("SELECTED07_MESH ",posture," zone=",zone_id," offset=",snappedf(offset,.0001)," visible=",visible)
	var file:=FileAccess.open(folder.path_join("mesh-surfaces.json"),FileAccess.WRITE);file.store_string(JSON.stringify(rows,"\t"));file.close()
	print("SELECTED07_MESH_DONE records=",rows.size()," selected=",use_selected," checks=",checks," failures=",failures)
	skin.queue_free();body.queue_free();camera.queue_free();label.queue_free();env_node.queue_free();key.queue_free();await process_frame;await process_frame;quit(failures)
