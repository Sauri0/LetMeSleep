extends SceneTree
const Maps=preload("res://scripts/map_catalog.gd")
const Arena=preload("res://scripts/arena.gd")
const Nav=preload("res://scripts/map_navigation.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const Sim=preload("res://scripts/simulation.gd")
const Supports=preload("res://scripts/pickup_supports.gd")
var checks:=0
var failures:=0
var capture:=false
var corrected_only:=false
func _initialize()->void:
	capture="--capture" in OS.get_cmdline_user_args();corrected_only="--corrected-only" in OS.get_cmdline_user_args();run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("PICKUP_SUPPORT_FAIL "+label)
func coord(v:Vector3)->Array:return [v.x,v.y,v.z]
func triangles(node:Node3D)->Array[PackedVector3Array]:
	var result:Array[PackedVector3Array]=[]
	var meshes:Array[Node]=node.find_children("*","MeshInstance3D",true,false)
	if node is MeshInstance3D:meshes.push_front(node)
	for mesh:MeshInstance3D in meshes:
		if mesh.mesh==null:continue
		var faces:PackedVector3Array=mesh.mesh.get_faces()
		for i:int in range(0,faces.size(),3):result.append(PackedVector3Array([mesh.to_global(faces[i]),mesh.to_global(faces[i+1]),mesh.to_global(faces[i+2])]))
	return result
func grid_for(faces:Array[PackedVector3Array])->Dictionary:
	var grid:Dictionary={}
	for tri:PackedVector3Array in faces:
		var normal:Vector3=(tri[1]-tri[0]).cross(tri[2]-tri[0])
		if absf(normal.y)<1e-9:continue
		var low:Vector3=tri[0].min(tri[1]).min(tri[2]);var high:Vector3=tri[0].max(tri[1]).max(tri[2])
		for x:int in range(floori(low.x*10),floori(high.x*10)+1):
			for z:int in range(floori(low.z*10),floori(high.z*10)+1):
				var key:=Vector2i(x,z)
				if not grid.has(key):grid[key]=[]
				grid[key].append(tri)
	return grid
func surface_height(p:Vector3,grid:Dictionary)->float:
	var highest:=-INF
	for tri:PackedVector3Array in grid.get(Vector2i(floori(p.x*10),floori(p.z*10)),[]):
		var hit:Variant=Geometry3D.ray_intersects_triangle(Vector3(p.x,8.0,p.z),Vector3.DOWN,tri[0],tri[1],tri[2])
		if hit!=null:highest=maxf(highest,Vector3(hit).y)
	return highest
func triangle_crossings(tool_faces:Array[PackedVector3Array],support_faces:Array[PackedVector3Array])->int:
	var grid:Dictionary={};var bounds:Array[AABB]=[]
	for i:int in range(support_faces.size()):
		var tri:PackedVector3Array=support_faces[i]
		var low:Vector3=tri[0].min(tri[1]).min(tri[2]);var high:Vector3=tri[0].max(tri[1]).max(tri[2])
		bounds.append(AABB(low,high-low).grow(.00001))
		for x:int in range(floori(low.x*10),floori(high.x*10)+1):
			for z:int in range(floori(low.z*10),floori(high.z*10)+1):
				var key:=Vector2i(x,z)
				if not grid.has(key):grid[key]=[]
				grid[key].append(i)
	var crossed:=0
	for tri:PackedVector3Array in tool_faces:
		var low:Vector3=tri[0].min(tri[1]).min(tri[2]);var high:Vector3=tri[0].max(tri[1]).max(tri[2])
		var box:=AABB(low,high-low).grow(.00001);var candidates:Dictionary={};var hit:=false
		for x:int in range(floori(low.x*10),floori(high.x*10)+1):
			for z:int in range(floori(low.z*10),floori(high.z*10)+1):
				for index:int in grid.get(Vector2i(x,z),[]):candidates[index]=true
		for index:int in candidates:
			if not box.intersects(bounds[index]):continue
			var other:PackedVector3Array=support_faces[index]
			for edge:int in range(3):
				if Geometry3D.segment_intersects_triangle(tri[edge],tri[(edge+1)%3],other[0],other[1],other[2])!=null or Geometry3D.segment_intersects_triangle(other[edge],other[(edge+1)%3],tri[0],tri[1],tri[2])!=null:
					hit=true;break
			if hit:break
		if hit:crossed+=1
	return crossed
func walk_to(destination:Vector3)->bool:
	var path:PackedVector3Array=Nav.path(Maps.HOUSE.human_spawns[0],destination,true)
	if path.is_empty():return false
	var doors:Dictionary={}
	for id:String in Doors.get_doors():doors[id]={"angle":PI/2}
	var actor:Dictionary={"p":Maps.HOUSE.human_spawns[0],"yaw":0.0,"pitch":0.0};var current:=0
	for tick:int in range(2400):
		var delta:Vector3=path[current]-Vector3(actor.p)
		if Vector2(delta.x,delta.z).length()<.12 and absf(delta.y)<.23:
			current+=1
			if current==path.size():return Vector3(actor.p).distance_to(destination)<.26
			delta=path[current]-Vector3(actor.p)
		delta.y=0.0
		Arena.step_human(actor,{"move":delta.normalized()*minf(1.0,delta.length()/(Arena.HUMAN_SPEED/30.0)),"yaw":0.0},1.0/30.0,"house",doors)
		if not Arena.can_fit_human(actor.p,Arena.HUMAN_HEIGHT,"house",doors):return false
	printerr("PICKUP_WALK stuck=",actor.p," destination=",destination)
	return false
func run()->void:
	root.size=Vector2i(1280,720);root.msaa_3d=Viewport.MSAA_2X
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world);world.build();world.load_map("house")
	check(Supports.definitions().size()==4,"four domestic supports are authored")
	for support_box:AABB in Supports.get_boxes():
		var clear:=true
		for existing:AABB in Maps.HOUSE.obstacles:
			if support_box.grow(-.0001).intersects(existing):clear=false;break
		check(clear,"new support piece stays outside existing furniture/walls")
		var leaf_clear:=true
		for definition:Dictionary in Doors.get_doors().values():
			for angle:int in range(0,91,5):
				if Doors.intersects_body(definition,deg_to_rad(float(angle)),support_box):leaf_clear=false
		check(leaf_clear,"new support stays outside every door swing")
		check(support_box in Arena.obstacles(),"support collision is included in authoritative static geometry")
	var specs:Array=Maps.get_map("house").pickups;var state:Dictionary={};var report:Array=[]
	for i:int in range(specs.size()):state[i+1]=specs[i].duplicate(true);state[i+1].holder=0
	world.sync_pickups(state,1.0)
	var camera:=Camera3D.new();root.add_child(camera);camera.fov=70;camera.near=.025;camera.make_current()
	var folder:=ProjectSettings.globalize_path("res://../outputs/0.7-objetos/ubicaciones");DirAccess.make_dir_recursive_absolute(folder)
	for i:int in range(specs.size()):
		var spec:Dictionary=specs[i];var support:Node3D
		for node:Node3D in world.map_root.get_children():
			if str(node.get_meta("catalog_kind","")) in ["furniture","pickup_support"] and AABB(node.get_meta("catalog_box")).position.is_equal_approx(spec.support_origin):support=node;break
		check(support!=null,"support mesh found for instance%d"%(i+1))
		if support==null:continue
		var support_faces:Array[PackedVector3Array]=triangles(support)
		var contact_faces:Array[PackedVector3Array]=support_faces
		if support.has_meta("pickup_support"):
			contact_faces=[]
			for mesh:MeshInstance3D in support.find_children("*","MeshInstance3D",true,false):
				if bool(mesh.get_meta("pickup_contact_surface",false)):contact_faces.append_array(triangles(mesh))
			# Clips are legitimate overhangs. Contacts use the authored tray/seat;
			# intersections below still test every part, including clips and feet.
			check(not contact_faces.is_empty(),"domestic support has actual contact mesh")
			if str(spec.tool)=="broom":check((Basis.from_euler(spec.rotation)*Vector3.UP).dot(Vector3.DOWN)>.999,"broom is upright with bristles down")
			else:check(Vector3(spec.support_point).y-Vector3(spec.approach).y<.5,"slipper rests on a low shoe bench")
		var grid:Dictionary=grid_for(contact_faces);var stand:Node3D=world.pickup_views[i+1]
		var tool_faces:Array[PackedVector3Array]=triangles(stand.get_node("Tool"))
		var points:Dictionary={};var lowest:=INF
		var tool_min:=Vector3(INF,INF,INF);var tool_max:=-tool_min
		for tri:PackedVector3Array in tool_faces:
			for p:Vector3 in tri:points[p.snapped(Vector3.ONE*.000001)]=true;lowest=minf(lowest,p.y)
			for p:Vector3 in tri:tool_min=tool_min.min(p);tool_max=tool_max.max(p)
		var penetrated:=0;var contacts:=0;var lower:=0;var supported:=0;var min_gap:=INF
		for p:Vector3 in points:
			var height:float=surface_height(p,grid)
			if p.y<lowest+.006:lower+=1
			if not is_finite(height):continue
			if p.y<lowest+.006:supported+=1
			var gap:float=p.y-height;min_gap=minf(min_gap,gap)
			if gap<-.001:penetrated+=1
			if gap>=-.001 and gap<=.008:contacts+=1
		check(penetrated==0,"no support/decor mesh penetration instance%d"%(i+1))
		check(min_gap>=.001 and min_gap<=.005,"real mesh contact clearance1..5mm instance%d"%(i+1))
		check(contacts>=3 and supported==lower,"lower vertices have real triangle support instance%d"%(i+1))
		var crossings:int=triangle_crossings(tool_faces,support_faces)
		check(crossings==0,"no triangle edges cross support or decoration instance%d"%(i+1))
		check(Arena.can_fit_human(spec.approach),"approach fits human body instance%d"%(i+1))
		check(Vector3(spec.approach).distance_to(spec.p)<1.45,"pickup range from feet instance%d"%(i+1))
		var direction:Vector3=Vector3(spec.p)-Vector3(spec.approach)
		var yaw:float=atan2(-direction.x,-direction.z)
		var eye:Vector3=Pose.view_origin({"p":spec.approach,"yaw":yaw,"body_yaw":yaw,"pitch":0.0,"crouched":false})
		var closed:Dictionary={}
		for id:String in Doors.get_doors():closed[id]={"angle":0.0}
		check(Arena.clear_segment(eye,spec.p,"house",closed),"eye-to-grip line clear even with closed leaves instance%d"%(i+1))
		var path:PackedVector3Array=Nav.path(Maps.HOUSE.human_spawns[0],spec.approach,true)
		check(not path.is_empty(),"approach has navigation route instance%d"%(i+1))
		check(walk_to(spec.approach),"physical walk reaches approach with initial open doors instance%d"%(i+1))
		var sim:=Sim.new();sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"mode":"blood","human_count":1})
		sim.actors[1].p=spec.approach
		sim.actors[1].yaw=yaw;sim.actors[1].body_yaw=yaw
		check(int(sim._pickup_info(1).get("_id",0))==i+1,"authoritative pickup offers expected instance%d"%(i+1))
		var data:Dictionary={"id":i+1,"tool":spec.tool,"support":spec.support,"support_origin":coord(spec.support_origin),"support_point":coord(spec.support_point),"grip":coord(spec.p),"rotation":coord(spec.rotation),"approach":coord(spec.approach),"min_gap_m":min_gap,"penetrating_vertices":penetrated,"triangle_crossings":crossings,"contact_vertices":contacts,"lower_supported":supported,"lower_count":lower}
		data.camera=coord(eye)
		if capture and (not corrected_only or i in [3,7,8,9]):
			for view:Node3D in world.pickup_views.values():
				var label:Label3D=view.get_node_or_null("PickupLabel")
				if label:label.visible=false
			camera.position=eye;camera.look_at((tool_min+tool_max)*.5);data.camera=coord(eye)
			for j:int in range(12):await process_frame
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(folder.path_join("%02d-%s.png"%[i+1,spec.tool]))
		report.append(data);print("PICKUP_SUPPORT ",JSON.stringify(data))
	var file:=FileAccess.open(folder.path_join("catalogo.json"),FileAccess.WRITE);file.store_string(JSON.stringify({"checks":checks,"failures":failures,"instances":report},"\t"));file.close()
	print("PICKUP_SUPPORT_RESULT checks=",checks," failures=",failures)
	world.queue_free();camera.queue_free();await process_frame;await process_frame;quit(0 if failures==0 else 1)
