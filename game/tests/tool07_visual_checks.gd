extends SceneTree
## Actual imported tools at their palm sockets; native FPS/TPS prepared poses.
const Actor=preload("res://scripts/actor_view.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Tools=preload("res://scripts/tool_catalog.gd")
var checks:=0
var failures:=0
var folder:String
var actor:ActorView
var camera:Camera3D
var caption:Label
var only_tool:=""
var only_state:=""
func _initialize()->void:_run.call_deferred()
func check(ok:bool,message:String)->void:
	checks+=1
	if not ok:failures+=1;print("TOOL07_VISUAL_FAIL ",message)
func _run()->void:
	root.size=Vector2i(1280,720)
	folder=ProjectSettings.globalize_path("res://../outputs/0.7-herramientas")
	for arg:String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="):folder=arg.trim_prefix("--output=")
		if arg.begins_with("--tool="):only_tool=arg.trim_prefix("--tool=")
		if arg.begins_with("--state="):only_state=arg.trim_prefix("--state=")
	DirAccess.make_dir_recursive_absolute(folder)
	var env:=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR;env.environment.background_color=Color("243a49");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;env.environment.ambient_light_color=Color("b5cbd9");env.environment.ambient_light_energy=.35;root.add_child(env)
	var light:=DirectionalLight3D.new();light.rotation_degrees=Vector3(-45,-135,0);light.light_energy=.8;light.shadow_enabled=true;root.add_child(light)
	if "--no-shadows" in OS.get_cmdline_user_args():light.shadow_enabled=false
	var stage:=Node3D.new();root.add_child(stage)
	var plane:=PlaneMesh.new();plane.size=Vector2(12,12);Actor.mesh(stage,plane,Vector3.ZERO,Actor.material(Color("8b8070")))
	actor=Actor.new();root.add_child(actor);actor.build("human","",0);actor.name_label.hide()
	camera=Camera3D.new();root.add_child(camera);camera.near=.02;camera.make_current()
	caption=Label.new();caption.position=Vector2(14,14);caption.add_theme_font_size_override("font_size",21);root.add_child(caption)
	for tool:String in Tools.IDS:
		if not only_tool.is_empty() and tool!=only_tool:continue
		var states:Array[String]=["rest","inspect","crouch","preparation","contact","return"]
		if Tools.throwable(tool):states.append_array(["charging","release","recovering"])
		for state:String in states:
			if not only_state.is_empty() and state!=only_state:continue
			var data:Dictionary={"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"pitch":0.0,"grounded":true,"state":"human","tool":tool,"crouch_amount":1.0 if state=="crouch" else 0.0,"appearance":{"color":1,"accent":4,"face":1,"hair":0,"outfit":0,"accessory":3,"footwear":0},"preview_only":true,"facial_preview":"neutral"}
			if state in ["inspect","crouch"]: data.pitch=-.85
			if state in ["preparation","contact","return"]:data.strike={"active":true,"hand":"right","tool":tool,"point":Vector3(.03,.98,-.8),"normal":Vector3.BACK,"progress":.1 if state=="preparation" else .46 if state=="contact" else .88,"duration":Tools.DATA[tool].gesture}
			if state in ["charging","release","recovering"]:data.throw_gesture={"state":state,"progress":1.0 if state=="release" else .65,"power":.65,"direction":Vector3(0,.1,-1).normalized(),"tool":tool}
			if state=="recovering":data.tool="hands"
			actor.set_local(false);actor.update_state(data,1.0)
			for frame:int in range(3):await process_frame
			var pose:=Pose.sample(data)
			check(actor.tool_socket.global_position.distance_to(pose.tool_grip)<.0001,tool+" "+state+" socket equals shared grasp centre")
			check(actor.imported_skin.bone_ids.has("thumb_a_r"),"thumb is independently articulated")
			if data.tool=="hands" or state=="release":check(float(actor.imported_skin.hand_grip.r)<.01,"empty or released palm opens")
			else:check(float(actor.imported_skin.hand_grip.r)>.99,"equipped object closes actual finger rig")
			if data.tool!="hands":
				check(actor.held_tool.get_child_count()==1,tool+" imported model exists")
				var visible_contact:Vector3=actor.tool_socket.global_transform*Vector3(Tools.VISUALS[tool].contact)
				check(visible_contact.distance_to(Vector3(pose.tool_grip)+Vector3(pose.tool_direction)*Tools.contact_length(tool))<.0001,tool+" "+state+" face follows shared grip and direction")
				if state in ["rest","crouch"]:
					var bottom:=INF
					for node:Node in actor.held_tool.find_children("*","MeshInstance3D",true,false):
						var mesh:=node as MeshInstance3D
						for surface:int in range(mesh.mesh.get_surface_count()):
							var vertices:PackedVector3Array=mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
							for vertex:Vector3 in vertices:bottom=minf(bottom,(mesh.global_transform*vertex).y)
					check(bottom>=.005,tool+" "+state+" actual mesh above floor "+str(bottom))
			else:check(not is_instance_valid(actor.held_tool) or actor.held_tool.get_child_count()==0,"released tool is no longer rendered in hand")
			if state=="contact":check(Vector3(pose.strike_contact).distance_to(Pose.strike_geometry(data,data.strike.point,data.strike.normal,"right",tool).contact)<.0001,tool+" physical grip reaches resolved impact")
			if state=="release":check(Vector3(pose.tool_grip).distance_to(Pose.throw_origin(data,data.throw_gesture.direction,data.throw_gesture.power))<.0001,tool+" launch from actual final grip")
			for view:String in ["tps","fps","grip","grip-side"]:
				actor.set_local(view=="fps")
				if view=="tps":camera.position=Vector3(1.8,1.55,-3.15);camera.fov=42;camera.look_at(Vector3(0,1.02,0))
				elif view=="fps":
					camera.position=Pose.view_origin(data);camera.fov=76
					camera.look_at(camera.position+Pose.view_direction(data))
				else:
					camera.position=Vector3(pose.tool_grip)+Vector3(.33,.13,-.46) if view=="grip" else Vector3(pose.tool_grip)+Vector3(-.32,.1,-.4);camera.fov=42
					camera.look_at(Vector3(pose.tool_grip))
				caption.text=Tools.DATA[tool].label+" · "+state+" · "+view.to_upper()+" · pose preparada"
				await process_frame
				if DisplayServer.get_name()!="headless":
					await RenderingServer.frame_post_draw
					check(root.get_texture().get_image().save_png(folder.path_join(tool+"-"+state+"-"+view+".png"))==OK,"capture")
	for direction:Vector3 in [Vector3.UP,Vector3.DOWN,Vector3.RIGHT,Vector3.LEFT,Vector3.FORWARD,Vector3.BACK]:
		for yaw:float in [0.0,1.3,-2.1]:
			for crouch:float in [0.0,1.0]:
				for power:float in [0.0,1.0]:
					var data:Dictionary={"p":Vector3(2,3,4),"body_yaw":yaw,"yaw":yaw,"tool":"slipper","crouch_amount":crouch,"throw_gesture":{"state":"release","progress":1.0,"power":power,"direction":direction,"tool":"slipper"}}
					var pose:=Pose.sample(data)
					var world_basis:=Basis(Vector3.UP,yaw)*Pose.tool_basis(pose.tool_direction,pose.tool_normal)
					check(world_basis.is_equal_approx(Tools.launch_basis(direction)),"launch roll remains continuous at release")
					check((Vector3(data.p)+Vector3(pose.tool_grip).rotated(Vector3.UP,yaw)).distance_to(Pose.throw_origin(data,direction,power))<.00001,"release origin equals held grasp across axes")
	print("TOOL07_VISUAL_RESULT checks=%d failures=%d"%[checks,failures]);quit(failures)
