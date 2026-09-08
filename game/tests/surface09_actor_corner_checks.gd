extends SceneTree
## Real SurfaceLocomotion, public snapshots at 20Hz and ActorView at 60Hz.
## This is a staged native interpolation regression, not a human/network run.
const Actor = preload("res://scripts/actor_view.gd")
const Arena = preload("res://scripts/arena.gd")
const Surface = preload("res://scripts/surface_locomotion.gd")
const Presentation = preload("res://scripts/surface_presentation.gd")
const Legs = preload("res://scripts/surface_appendage_pose.gd")
class LegacyLinearDisplay extends Presentation:
	func advance(_data: Dictionary,_dt: float,_force_reset: bool=false) -> Dictionary:
		return {}
var checks := 0
var failures := 0
var folder := ""
var verify := true
var footwear := 0
var records: Array = []
var previous_mesh_points: Dictionary = {}
var body_box := AABB(Vector3(-.5,1,-.5),Vector3(1,.6,1))

func helper_checks() -> void:
	# The authored leg2 pole used to flip at this direction during return.
	var rest_knee := Vector3(.084,-.063,.014)
	var rest_tip := Vector3(.108,-.128,-.009)
	var tip_a := (rest_knee.normalized()+Vector3.UP*.000001).normalized()*.13
	var tip_b := (rest_knee.normalized()-Vector3.UP*.000001).normalized()*.13
	var knee_a := Legs.knee(Vector3.ZERO,rest_knee,rest_tip,tip_a)
	var knee_b := Legs.knee(Vector3.ZERO,rest_knee,rest_tip,tip_b)
	check(knee_a.distance_to(knee_b)<.00001,"knee pole remains continuous through prior singular direction")
	check(absf(knee_a.length()-rest_knee.length())<.000001,"transported pole preserves proximal length")
	check(absf(knee_a.distance_to(tip_a)-rest_knee.distance_to(rest_tip))<.000001,"transported pole preserves distal length")
	check(is_equal_approx(Presentation.PUBLIC_STRIDE,Surface.STRIDE),"public phase stride matches authority")
	for packet_rate: float in [10.0,20.0,60.0]:
		var wrapped := Presentation.new()
		var packet := {"p":Vector3(0,.045,0),"state":"perched","surface_normal":Vector3.UP,"surface_forward":Vector3.FORWARD,"motion_phase":6.2}
		wrapped.advance(packet,1.0/60.0)
		var last_phase := wrapped.phase
		for frame: int in range(120):
			if frame%int(60.0/packet_rate)==0:
				packet.p+=Vector3.FORWARD*.65/packet_rate
				packet.motion_phase=fposmod(float(packet.motion_phase)+.65/packet_rate/Surface.STRIDE*TAU,TAU)
			var shown := wrapped.advance(packet,1.0/60.0)
			check(float(shown.motion_phase)>=last_phase and float(shown.motion_phase)-last_phase<1.4,"TAU wrap never reverses or snaps displayed stride")
			last_phase=shown.motion_phase
	var normals := [Vector3.UP,Vector3.DOWN,Vector3.RIGHT,Vector3.LEFT,Vector3.FORWARD,Vector3.BACK]
	for first: Vector3 in normals:
		for second: Vector3 in normals:
			if absf(first.dot(second))>.01: continue
			for rate: float in [30.0,60.0,144.0]:
				var edge := body_box.get_center()+first*((body_box.size*first).length()*.5+.045)+second*((body_box.size*second).length()*.5+.045)
				var helper := Presentation.new()
				var data := {"p":edge-second*.10,"state":"perched","surface_normal":first,"surface_forward":second,"motion_phase":0.0}
				helper.advance(data,1.0/rate)
				data.p=edge-first*.12+first.cross(second)*.06
				data.surface_normal=second
				data.surface_forward=-first
				data.motion_phase=.22/.15*TAU
				var original := data.duplicate(true)
				for frame: int in range(int(rate)):
					var displayed := helper.advance(data,1.0/rate)
					var p: Vector3=displayed.p
					check(not body_box.grow(.04-.00001).has_point(p),"all24 adjacent planes keep expanded-box clearance")
					check(absf((p-edge).dot(first))<.00001 or absf((p-edge).dot(second))<.00001,"display remains on real support polyline")
					check(helper.points.size()<=Presentation.MAX_POINTS,"presentation queue bounded")
				check(data==original,"presentation leaves public snapshot unchanged")
				check(helper.position.distance_to(data.p)<.00001,"presentation reaches latest snapshot without overshoot")
	var helper := Presentation.new()
	var data := {"p":Vector3(0,.045,0),"state":"perched","surface_normal":Vector3.UP,"surface_forward":Vector3.FORWARD}
	helper.advance(data,.016)
	for change: Dictionary in [{"p":Vector3(1,.045,0)},{"surface_normal":Vector3.DOWN},{"p":Vector3(1,.145,0)}]:
		data.merge(change,true)
		var displayed := helper.advance(data,.016)
		check(bool(displayed.presentation_reset) and helper.points.is_empty() and Vector3(displayed.p)==Vector3(data.p),"discontinuous support resets instead of phantom route")
	data.state="flying"
	check(helper.advance(data,.016).is_empty() and not helper.active and helper.points.is_empty(),"takeoff clears surface queue")

func _initialize() -> void: _run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		if failures<16: print("ACTORCORNER09 FAIL "+label)

func snapshot(actor: Dictionary) -> Dictionary:
	return {"p":actor.p,"yaw":actor.yaw,"pitch":actor.pitch,"state":actor.state,"velocity":actor.velocity,"motion_phase":actor.motion_phase,"motion_speed":actor.motion_speed,"surface_normal":actor._surface_normal,"surface_forward":actor._surface_forward,"preview_only":true,"facial_preview":"neutral","appearance":{"footwear":footwear}}

func capture(name: String) -> void:
	if DisplayServer.get_name()=="headless": return
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(folder.path_join(name+".png"))==OK,"native witness "+name)

func triangle_in_box(a: Vector3,b: Vector3,c: Vector3,box: AABB) -> bool:
	var low := a.min(b).min(c)
	var high := a.max(b).max(c)
	if low.x>=box.end.x or low.y>=box.end.y or low.z>=box.end.z or high.x<=box.position.x or high.y<=box.position.y or high.z<=box.position.z: return false
	# Clip the actual deformed triangle against all six box planes. A vertex-
	# only test misses a long edge crossing a corner with both ends outside.
	var polygon: Array[Vector3] = [a,b,c]
	for axis: int in range(3):
		for side: int in [-1,1]:
			var plane: float=box.position[axis] if side<0 else box.end[axis]
			var clipped: Array[Vector3] = []
			for index: int in range(polygon.size()):
				var p: Vector3=polygon[index]
				var q: Vector3=polygon[(index+1)%polygon.size()]
				var inside_p: bool=(p[axis]-plane)*side<=0
				var inside_q: bool=(q[axis]-plane)*side<=0
				if inside_p: clipped.append(p)
				if inside_p!=inside_q: clipped.append(p.lerp(q,(plane-p[axis])/(q[axis]-p[axis])))
			polygon=clipped
			if polygon.size()<3: return false
	return true

func mesh_penetration(actor: ActorView) -> Dictionary:
	var count := 0
	var depth := 0.0
	var sampled := 0
	var foot_count := 0
	var foot_depth := 0.0
	var foot_sampled := 0
	var bad_bones: Dictionary = {}
	var bad_points: Array = []
	var core_step := 0.0
	var foot_step := 0.0
	var core_triangles := 0
	var foot_triangles := 0
	var largest_motion: Dictionary = {}
	for mesh: MeshInstance3D in actor.imported_skin.meshes:
		var core := str(mesh.name)=="mosquito_core"
		var feet := str(mesh.name).begins_with("mosquito_footwear_")
		if not mesh.visible or not (core or feet): continue
		var baked := mesh.bake_mesh_from_current_skeleton_pose()
		for surface: int in range(baked.get_surface_count()):
			var key := str(mesh.name)+":"+str(surface)
			var last: PackedVector3Array=previous_mesh_points.get(key,PackedVector3Array())
			var current := PackedVector3Array()
			var source := mesh.mesh.surface_get_arrays(surface)
			var vertices: PackedVector3Array=baked.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
			for vertex: int in range(vertices.size()):
				var point: Vector3=vertices[vertex]
				if core: sampled+=1
				else: foot_sampled+=1
				var world: Vector3=mesh.global_transform*point
				current.append(world)
				if last.size()==vertices.size():
					if core: core_step=maxf(core_step,world.distance_to(last[vertex]))
					elif world.distance_to(last[vertex])>foot_step:
						foot_step=world.distance_to(last[vertex])
						var binding: int=source[Mesh.ARRAY_BONES][vertex*(source[Mesh.ARRAY_BONES].size()/vertices.size())]
						largest_motion={"bone":str(mesh.skin.get_bind_name(binding)),"from":str(last[vertex]),"to":str(world)}
				if body_box.has_point(world):
					var gap := (world-body_box.position).min(body_box.end-world)
					var amount := minf(gap.x,minf(gap.y,gap.z))
					if core:
						count+=1;depth=maxf(depth,amount)
					elif amount>.00005:
						foot_count+=1;foot_depth=maxf(foot_depth,amount)
						var bones: PackedInt32Array=source[Mesh.ARRAY_BONES]
						var stride: int=bones.size()/vertices.size()
						var bone := str(mesh.skin.get_bind_name(bones[vertex*stride]))
						bad_bones[bone]=int(bad_bones.get(bone,0))+1
						if bad_points.size()<8: bad_points.append({"p":str(world),"bone":bone})
			previous_mesh_points[key]=current
			var indices: PackedInt32Array=source[Mesh.ARRAY_INDEX]
			for triangle: int in range(0,indices.size(),3):
				if triangle_in_box(current[indices[triangle]],current[indices[triangle+1]],current[indices[triangle+2]],body_box.grow(-.00005)):
					if core: core_triangles+=1
					else: foot_triangles+=1
	return {"vertices":count,"depth":depth,"sampled":sampled,"foot_vertices":foot_count,"foot_depth":foot_depth,"foot_sampled":foot_sampled,"bad_bones":bad_bones,"bad_points":bad_points,"core_step":core_step,"foot_step":foot_step,"core_triangles":core_triangles,"foot_triangles":foot_triangles,"largest_motion":largest_motion}

func _run() -> void:
	helper_checks()
	if "--helper-only" in OS.get_cmdline_user_args():
		print("ACTORCORNER09_HELPER %d/%d PASS"%[checks-failures,checks])
		quit(1 if failures else 0)
		return
	if DisplayServer.get_name()=="headless":
		check(false,"mesh gate requires native rendering; use --helper-only for headless")
		quit(1)
		return
	folder=ProjectSettings.globalize_path("res://../outputs/0.9-actor-corner/current")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): folder=arg.trim_prefix("--output=")
		if arg=="--verify": verify=true
		if arg=="--diagnostic": verify=false
		if arg.begins_with("--footwear="): footwear=clampi(int(arg.trim_prefix("--footwear=")),0,2)
	DirAccess.make_dir_recursive_absolute(folder)
	root.size=Vector2i(1120,800)
	var world := Node3D.new();root.add_child(world)
	var envnode := WorldEnvironment.new();var env := Environment.new()
	env.background_mode=Environment.BG_COLOR;env.background_color=Color("2a4050")
	env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;env.ambient_light_color=Color("c2d2df");env.ambient_light_energy=.55
	envnode.environment=env;world.add_child(envnode)
	var light := DirectionalLight3D.new();world.add_child(light)
	light.rotation_degrees=Vector3(-40,150,0);light.light_energy=.7
	var body := StaticBody3D.new();body.collision_layer=1;world.add_child(body)
	body.position=body_box.get_center()
	var collision := CollisionShape3D.new();var shape := BoxShape3D.new();shape.size=body_box.size;collision.shape=shape;body.add_child(collision)
	var box_mesh := BoxMesh.new();box_mesh.size=body_box.size
	Actor.mesh(body,box_mesh,Vector3.ZERO,Actor.material(Color("8cabaa")))
	var camera := Camera3D.new();world.add_child(camera);camera.near=.004;camera.fov=38;camera.make_current()
	var label := Label.new();label.position=Vector2(18,18);label.add_theme_font_size_override("font_size",20);root.add_child(label)
	var id := "actor-corner09-fixture"
	Arena._map_cache[id]={"half_x":5.0,"half_z":5.0,"ceiling":4.0,"obstacles":[body_box]}
	var surface := Surface.new();surface.configure(id)
	await physics_frame
	var ordinal := 0
	for normal: Vector3 in [Vector3.UP,Vector3.DOWN,Vector3.RIGHT,Vector3.LEFT,Vector3.FORWARD,Vector3.BACK]:
		if "--first-face" in OS.get_cmdline_user_args() and ordinal>0: break
		var center := body_box.get_center()+normal*(body_box.size*normal).length()*.5
		var actor := {"p":center+normal*.16,"yaw":0.0,"pitch":0.0,"velocity":Vector3.ZERO,"state":"flying","motion_phase":0.0,"motion_speed":0.0,"grounded":false}
		check(surface.begin(actor,{}),"acquire actual box face")
		for frame: int in range(30):
			if actor.state=="perched": break
			surface.approach(actor,1.0/60.0,{})
		check(actor.state=="perched" and Vector3(actor._surface_normal).is_equal_approx(normal),"real solver face agrees with fixture")
		var model := Actor.new();world.add_child(model);model.build("mosquito","",0);model.preview_only=true;model.name_label.visible=false
		if "--legacy-presentation" in OS.get_cmdline_user_args(): model.surface_presentation=LegacyLinearDisplay.new()
		var published := snapshot(actor)
		for frame: int in range(40): model.update_state(published,1.0/60.0)
		var bad_centers := 0
		var bad_mesh := 0
		var max_depth := 0.0
		var max_error := 0.0
		var max_step := 0.0
		var bad_feet := 0
		var max_foot_depth := 0.0
		var max_core_step := 0.0
		var max_foot_step := 0.0
		var max_corner_foot_step := 0.0
		var max_straight_foot_step := 0.0
		var max_stance_drift := 0.0
		var minimum_supported := 6
		var last_contacts: Dictionary = {}
		previous_mesh_points.clear()
		var seen: Dictionary = {}
		var previous := model.global_position
		var witness := false
		var trace: Array = []
		for frame: int in range(96):
			if frame%3==0:
				surface.step_surface(actor,Vector3.FORWARD,.05,{})
				published=snapshot(actor)
				check(Arena.can_fit_mosquito(actor.p,id),"authority remains outside expanded box")
			model.update_state(published,1.0/60.0)
			await process_frame
			await RenderingServer.frame_post_draw
			seen[str(published.surface_normal)]=true
			var clear := Arena.can_fit_mosquito(model.global_position,id)
			if not clear: bad_centers+=1
			max_error=maxf(max_error,model.global_position.distance_to(actor.p))
			max_step=maxf(max_step,previous.distance_to(model.global_position))
			previous=model.global_position
			var penetration := mesh_penetration(model)
			check(int(penetration.sampled)>100 and int(penetration.foot_sampled)>100,"actual core and footwear vertices sampled")
			if int(penetration.vertices)>0 or int(penetration.core_triangles)>0: bad_mesh+=1
			if int(penetration.foot_vertices)>0 or int(penetration.foot_triangles)>0: bad_feet+=1
			max_depth=maxf(max_depth,float(penetration.depth))
			max_foot_depth=maxf(max_foot_depth,float(penetration.foot_depth))
			max_core_step=maxf(max_core_step,float(penetration.core_step))
			max_foot_step=maxf(max_foot_step,float(penetration.foot_step))
			var contacts: Dictionary=model.imported_skin.surface_contacts
			var supported_count := 0
			check(contacts.size()==6,"all six actual tarsi evaluated during continuous turn")
			for bone: String in contacts:
				var contact: Dictionary=contacts[bone]
				if bool(contact.supported): supported_count+=1
				if not last_contacts.has(bone): continue
				var previous_contact: Dictionary=last_contacts[bone]
				if bool(contact.supported) and bool(contact.stance) and bool(previous_contact.supported) and bool(previous_contact.stance) and float(contact.cycle)>=float(previous_contact.cycle) and Vector3(contact.normal).dot(previous_contact.normal)>.97:
					max_stance_drift=maxf(max_stance_drift,(Vector3(contact.tip)-Vector3(previous_contact.tip)).slide(contact.normal).length())
			minimum_supported=mini(minimum_supported,supported_count)
			last_contacts=contacts.duplicate(true)
			if model.model.global_basis.y.normalized().dot(Vector3(published.surface_normal))<.999:
				max_corner_foot_step=maxf(max_corner_foot_step,float(penetration.foot_step))
			else:
				max_straight_foot_step=maxf(max_straight_foot_step,float(penetration.foot_step))
			trace.append({"frame":frame,"authority":str(actor.p),"display":str(model.global_position),"normal":str(published.surface_normal),"clear":clear,"mesh":penetration,"contacts":model.imported_skin.surface_contacts.duplicate(true),"up":str(model.model.global_basis.y.normalized())})
			var witness_frame := 62 if ordinal<4 else 41
			if not witness and (not clear or int(penetration.vertices)>0 or frame==witness_frame):
				witness=true
				var up: Vector3=published.surface_normal
				var tangent: Vector3=published.surface_forward
				camera.position=model.global_position+up*.50-tangent*.25+tangent.cross(up)*.28
				camera.look_at(model.global_position,up)
				label.text="PRUEBA PREPARADA · esquina convexa · cara %d · patas %d · frame %d"%[ordinal,footwear,frame]
				await capture("face-%d"%ordinal)
		check(seen.size()>=2,"continuous motion crosses actual adjacent face")
		if verify:
			check(bad_centers==0,"displayed travel envelope does not cut convex corner")
			check(bad_mesh==0,"actual core mesh stays outside cube")
			check(bad_feet==0,"actual deformed tarsi stay outside neighbouring face")
			check(max_step<.045,"presentation remains continuous")
			check(max_error<.055,"presentation lag bounded to less than55mm at20Hz")
			check(max_core_step<.045,"actual body motion has no one-frame corner snap")
			check(max_foot_step<.045,"actual tarsus has no one-frame stride or corner snap")
			check(max_stance_drift<.0004,"planted tarsi do not slide tangentially")
		records.append({"normal":str(normal),"center_bad_frames":bad_centers,"mesh_bad_frames":bad_mesh,"maximum_mesh_depth":max_depth,"foot_bad_frames":bad_feet,"maximum_foot_depth":max_foot_depth,"maximum_position_error":max_error,"maximum_step":max_step,"maximum_core_vertex_step":max_core_step,"maximum_foot_vertex_step":max_foot_step,"maximum_corner_foot_step":max_corner_foot_step,"maximum_straight_foot_step":max_straight_foot_step,"maximum_stance_drift":max_stance_drift,"minimum_supported_feet":minimum_supported,"trace":trace})
		world.remove_child(model);model.free();ordinal+=1
	var file := FileAccess.open(folder.path_join("actor-corner09.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"verify":verify,"footwear":footwear,"legacy_presentation_current_skin":"--legacy-presentation" in OS.get_cmdline_user_args(),"snapshot_hz":20,"render_hz":60,"cases":records},"\t"))
	print("ACTORCORNER09 %d/%d PASS"%[checks-failures,checks])
	for row: Dictionary in records: print("CASE ",row.normal," center=",row.center_bad_frames," mesh=",row.mesh_bad_frames," depth=",row.maximum_mesh_depth," feet=",row.foot_bad_frames," footdepth=",row.maximum_foot_depth," step=",row.maximum_step," body_step=",row.maximum_core_vertex_step," foot_step=",row.maximum_foot_vertex_step)
	quit(1 if failures else 0)
