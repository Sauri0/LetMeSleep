extends SceneTree
## Same house, selected staged A human / B insect, camera and geometry per phase.
const CharacterSkin = preload("res://assets/art/characters/shared/character_skin.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const Arena = preload("res://scripts/arena.gd")
var world: Node3D
var camera: Camera3D
var human: Node3D
var mosquito: Node3D
var lights: Array[Light3D] = []
var originals: Dictionary = {}
var report: Dictionary = {}
var base: String
var checks := 0
var failures := 0
var room_energy := .55
var candidate_only := false
var correct_skin := false
var production := false
var atlas_only := 0

func _initialize() -> void:
	for arg:String in OS.get_cmdline_user_args():
		if arg.begins_with("--energy="):room_energy=float(arg.trim_prefix("--energy="))
		if arg=="--candidate-only":candidate_only=true
		if arg=="--correct-skin":correct_skin=true
		if arg=="--production":production=true
		if arg.begins_with("--atlas="):atlas_only=int(arg.trim_prefix("--atlas="))
	run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1; push_error(message)

func run() -> void:
	root.size = Vector2i(1920,1080)
	root.msaa_3d = Viewport.MSAA_2X
	root.physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	world = load("res://scripts/world.gd").new(); root.add_child(world); world.build(); world.load_map("house")
	camera = Camera3D.new(); root.add_child(camera); camera.near=.025; camera.far=60; camera.make_current()
	for value: Node in world.map_root.find_children("*","Light3D",true,false):
		var light := value as Light3D; lights.append(light)
		originals[light] = {"energy":light.light_energy,"shadow":light.shadow_enabled,"bias":light.shadow_bias,"normal":light.shadow_normal_bias,"blur":light.shadow_blur,"fade":light.distance_fade_enabled}
	human = selected_actor("human","A",Vector3(5.4,3.2,8.6))
	mosquito = selected_actor("mosquito","B",Vector3(4.9,4.38,8.4))
	if correct_skin:restore_authored_skin_hex()
	base = ProjectSettings.globalize_path("res://../outputs/0.7-lighting-ab")
	if not is_equal_approx(room_energy,.85):base=base.path_join("energy-%03d"%roundi(room_energy*100))
	if correct_skin:base=base.path_join("skin-corrected")
	if production:base=base.path_join("production-final")
	DirAccess.make_dir_recursive_absolute(base)
	var views: Array[Dictionary] = [
		{"id":"17-rosa","p":Vector3(3.15,4.85,6.25),"to":Vector3(6,4.2,8.6),"fov":70.0},
		{"id":"world-detail","p":Vector3(4.1,4.9,7.2),"to":Vector3(5.4,4.72,8.6),"fov":48.0}]
	for phase: String in ["current","candidate-1024","candidate-2048"]:
		if candidate_only and phase=="current":continue
		if atlas_only>0 and not phase.ends_with(str(atlas_only)):continue
		configure(phase)
		var folder := base.path_join(phase); DirAccess.make_dir_recursive_absolute(folder)
		var active := 0; var shadows := 0
		for light: Light3D in lights:
			if light is DirectionalLight3D or light.light_energy <= 0: continue
			active += 1
			if light.shadow_enabled: shadows += 1
		if phase != "current":
			check(active==23 and shadows==23,"23 bounded, shadowed lamps including all16 rooms")
			check(active<=int(ProjectSettings.get_setting("rendering/limits/opengl/max_renderable_lights",32)),"Within Compatibility light cap")
			for light: Light3D in lights:
				if light.light_energy>0 and not light is DirectionalLight3D:check(not light.distance_fade_enabled,"Lamp and occlusion remain continuous across camera movement")
		var data: Dictionary = {"active_lights":active,"shadow_lights":shadows,"room_energy":room_energy,"atlas":root.positional_shadow_atlas_size,"depth_16_bits":root.positional_shadow_atlas_16_bits,"views":{}}
		set_doors(0)
		for view: Dictionary in views:
			set_camera(view)
			check(camera_clear(camera.position),"Measured camera is in free air: "+view.id)
			await settle()
			var frame := root.get_texture().get_image()
			frame.save_png(folder.path_join(view.id+".png"))
			var mask := await human_mask()
			data.views[view.id] = pixels(frame,mask)
			data.views[view.id].performance = await measure_frames(180)
			print("LIGHT_AB ",phase," ",view.id," ",JSON.stringify(data.views[view.id]))
		human.visible=false; mosquito.visible=false
		data.door = await door_occlusion(folder)
		human.visible=true; mosquito.visible=true
		report[phase]=data
	var result := FileAccess.open(base.path_join("metrics.json"),FileAccess.WRITE)
	result.store_string(JSON.stringify({"checks":checks,"failures":failures,"renderer":"gl_compatibility","resolution":[1920,1080],"msaa":2,"phases":report},"\t"));result.close()
	print("HOUSE_LIGHT_AB checks=",checks," failures=",failures)
	world.queue_free();human.queue_free();mosquito.queue_free();camera.queue_free()
	await process_frame;await process_frame;quit(0 if failures==0 else 1)

func configure(phase: String) -> void:
	root.positional_shadow_atlas_size = 1024 if phase.ends_with("1024") else 2048
	root.positional_shadow_atlas_16_bits = phase=="current"
	for light: Light3D in lights:
		var saved: Dictionary=originals[light]
		light.light_energy=saved.energy;light.shadow_enabled=saved.shadow;light.shadow_bias=saved.bias;light.shadow_normal_bias=saved.normal;light.shadow_blur=saved.blur;light.distance_fade_enabled=saved.fade
		if phase=="current" or light is DirectionalLight3D:continue
		if light.has_meta("house_window"):light.light_energy=0;continue
		light.shadow_enabled=true;light.distance_fade_enabled=false
		light.shadow_bias=.20;light.shadow_normal_bias=2.0;light.shadow_blur=1.2
		if light.has_meta("house_room"):light.light_energy=room_energy

func selected_actor(role: String, variant: String, p: Vector3) -> Node3D:
	var skin := CharacterSkin.new(); root.add_child(skin)
	var directory := "res://assets/art/samples07/characters/%s/%s/"%[variant,role]
	if production:skin.setup(role)
	else:skin.setup(role,directory+role+"_lms06.glb",directory+"rig_contract.json")
	skin.set_appearance({"color":1,"accent":4,"face":1 if role=="human" else 0,"hair":0,"outfit":0,"accessory":3 if role=="human" else 0,"footwear":0})
	skin.position=p;skin.rotation.y=.9;skin.scale=Vector3.ONE*(1.0 if role=="human" else .35)
	var state:Dictionary={"p":Vector3.ZERO,"yaw":.9,"body_yaw":.9,"pitch":0.0,"state":"human" if role=="human" else "flying","velocity":Vector3.ZERO,"motion_speed":0.0,"grounded":true,"relaxed_pose":true,"pose_time":0.0,"preview_only":true,"facial_preview":"neutral","facial_no_blink":true,"tool":"hands"}
	for i:int in range(30):
		if role=="human":skin.apply_human(Pose.sample(state),state,1.0/30.0)
		else:skin.apply_mosquito(state,0.0,0.0)
	return skin

func restore_authored_skin_hex() -> void:
	var desired:Dictionary={"skin":Color("e3ac83"),"skin_shadow":Color("c88e6a"),"lip":Color("ab735a")}
	var copies:Dictionary={}
	for mesh:MeshInstance3D in human.find_children("*","MeshInstance3D",true,false):
		for i:int in range(mesh.mesh.get_surface_count()):
			var material:Material=mesh.get_active_material(i)
			if material is StandardMaterial3D and desired.has(material.resource_name):
				if not copies.has(material):
					var copy:StandardMaterial3D=material.duplicate()
					print("SKIN_HEX_FIXTURE ",material.resource_name," ",material.albedo_color.to_html(false)," -> ",desired[material.resource_name].to_html(false))
					copy.albedo_color=desired[material.resource_name];copies[material]=copy
				mesh.set_surface_override_material(i,copies[material])

func set_camera(view: Dictionary) -> void:
	camera.fov=float(view.fov);camera.position=view.p;camera.look_at(view.to);camera.reset_physics_interpolation()

func camera_clear(p: Vector3) -> bool:
	for box:AABB in Arena.obstacles():
		if box.grow(.025).has_point(p):return false
	return true

func settle() -> void:
	await physics_frame;await physics_frame
	for i:int in range(12):await process_frame
	await RenderingServer.frame_post_draw

func human_mask() -> Image:
	var material:=StandardMaterial3D.new();material.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;material.albedo_color=Color.WHITE
	var saved:Dictionary={}
	for mesh:MeshInstance3D in human.find_children("*","MeshInstance3D",true,false):
		saved[mesh]={"material":mesh.material_override,"layers":mesh.layers};mesh.material_override=material;mesh.layers=4
	var old_mask:=camera.cull_mask;camera.cull_mask=4
	var old_color:Color=world.scene_environment.background_color;world.scene_environment.background_color=Color.BLACK
	await settle()
	var image:=root.get_texture().get_image()
	for mesh:MeshInstance3D in saved:mesh.material_override=saved[mesh].material;mesh.layers=saved[mesh].layers
	camera.cull_mask=old_mask;world.scene_environment.background_color=old_color
	await settle()
	return image

func pixels(frame: Image, mask: Image) -> Dictionary:
	var values:Array[float]=[];var clipped:=0;var over95:=0
	for y:int in range(frame.get_height()):
		for x:int in range(frame.get_width()):
			if mask.get_pixel(x,y).r<.95:continue
			var c:=frame.get_pixel(x,y);var l:=.2126*c.r+.7152*c.g+.0722*c.b
			values.append(l)
			if minf(c.r,minf(c.g,c.b))>.985:clipped+=1
			if l>.95:over95+=1
	values.sort()
	return {"actor_pixels":values.size(),"white_clipped":clipped,"luma_over_95":over95,"luma_p50":values[int(values.size()*.5)] if not values.is_empty() else 0,"luma_p95":values[int(values.size()*.95)] if not values.is_empty() else 0}

func measure_frames(count: int) -> Dictionary:
	var wall:Array[float]=[];var gpu:Array[float]=[];var cpu:Array[float]=[];var draws:Array[float]=[]
	var previous:=Time.get_ticks_usec()
	for i:int in range(count):
		await process_frame;await RenderingServer.frame_post_draw
		var now:=Time.get_ticks_usec()
		if i>25:
			wall.append(float(now-previous)/1000.0)
			gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()))
			cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()))
			draws.append(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
		previous=now
	wall.sort();gpu.sort();cpu.sort();draws.sort()
	return {"wall_p50_ms":wall[int(wall.size()*.5)],"wall_p95_ms":wall[int(wall.size()*.95)],"gpu_p50_ms":gpu[int(gpu.size()*.5)],"render_cpu_p50_ms":cpu[int(cpu.size()*.5)],"draw_calls_p50":draws[int(draws.size()*.5)]}

func set_doors(angle: float) -> void:
	var states:Dictionary={}
	for id:String in Doors.get_doors():states[id]={"angle":angle,"target_angle":angle,"moving":false,"blocked":false,"revision":1}
	world.sync_doors(states,1.0)

func door_occlusion(folder: String) -> Dictionary:
	# A diagnostic source isolates the actual World leaf/wall from ambient fill.
	# The positive control disables its shadow while keeping geometry, exposure,
	# source, cone, receiver and camera identical. No production source is changed.
	var saved:Dictionary={}
	for light:Light3D in lights:saved[light]=light.light_energy;light.light_energy=0
	var material:=StandardMaterial3D.new();material.albedo_color=Color(.7,.7,.7);material.roughness=1
	var receiver:=MeshInstance3D.new();var plane:=PlaneMesh.new();plane.size=Vector2(.45,.45)
	receiver.mesh=plane;receiver.material_override=material;root.add_child(receiver)
	var source:=SpotLight3D.new();root.add_child(source)
	source.light_energy=.55;source.spot_range=4.0;source.spot_angle=32;source.spot_attenuation=1.0
	source.shadow_bias=.20;source.shadow_normal_bias=2.0;source.shadow_blur=1.2
	source.distance_fade_enabled=false
	var result:Dictionary={"method":"temporary control spot; actual World door geometry; same receiver and exposure", "source_energy":.55,"source_range":4.0,"source_angle":32.0,"source_bias":.20,"source_normal_bias":2.0,"receiver_y":.55,"limit":"qualitative occlusion control, not original lamp intensity or calibrated attenuation; open and unshadowed magnitudes differ"}
	for side:String in ["room","hall"]:
		receiver.position=Vector3(-2.65,.55,-8.2) if side=="room" else Vector3(-1.55,.55,-8.2)
		source.position=Vector3(-.7,1.6,-8.2) if side=="room" else Vector3(-3.4,1.6,-8.2)
		source.look_at(receiver.position)
		check(not Doors.ray_leaf(Doors.DEFINITIONS.kitchen,0,source.position,receiver.position).is_empty(),side+" control ray crosses closed leaf above mosquito gap")
		check(Doors.ray_leaf(Doors.DEFINITIONS.kitchen,PI/2,source.position,receiver.position).is_empty(),side+" open leaf clears control ray")
		check(source.position.distance_to(receiver.position)<source.spot_range,side+" receiver is inside control range and central cone")
		set_camera({"p":Vector3(-3.45,1.6,-8.2) if side=="room" else Vector3(-.7,1.6,-8.2),"to":receiver.position,"fov":60.0})
		check(camera_clear(camera.position),side+" diagnostic camera free")
		for state:String in ["closed","open","unshadowed"]:
			set_doors(PI/2 if state=="open" else 0.0);source.shadow_enabled=state!="unshadowed";await settle()
			var frame:=root.get_texture().get_image();var label:=side+"-"+state
			frame.save_png(folder.path_join("door-"+label+".png"))
			result[label]=sample_receiver(frame,receiver.position)
		check(float(result[side+"-open"])-float(result[side+"-closed"])>.08,side+" actual door measurably occludes direct light")
		check(float(result[side+"-unshadowed"])-float(result[side+"-closed"])>.08,side+" disabling occlusion restores direct light through the closed leaf")
	print("DOOR_LIGHT_CONTROL ",JSON.stringify(result))
	for light:Light3D in lights:light.light_energy=float(saved[light])
	receiver.queue_free();source.queue_free();await process_frame
	return result

func sample_receiver(frame:Image,p:Vector3)->float:
	var at:Vector2=camera.unproject_position(p)*Vector2(frame.get_size())/root.get_visible_rect().size
	var sum:=0.0
	for y:int in range(roundi(at.y)-10,roundi(at.y)+10):
		for x:int in range(roundi(at.x)-10,roundi(at.x)+10):
			var color:=frame.get_pixel(x,y);sum+=.2126*color.r+.7152*color.g+.0722*color.b
	return sum/400.0
