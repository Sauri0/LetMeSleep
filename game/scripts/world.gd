extends Node3D

const ActorModel = preload("res://scripts/actor_view.gd")
const Map = preload("res://scripts/arena.gd")
const AudioEffects = preload("res://scripts/audio_fx.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Furnishings = preload("res://assets/art/house/house_library.gd")
const SculptedShape = preload("res://assets/procedural_shapes.gd")
const HouseDetails = preload("res://scripts/house_details.gd")
const DoorViewScript = preload("res://scripts/door_view.gd")
const VideoSettings = preload("res://scripts/video_settings.gd")

var menu_camera: Camera3D
var actors: Dictionary = {}
var actor_state: Dictionary = {}
var pickup_views: Dictionary = {}
var local_actor_id: int = 0
var marker: Node3D
var marker_label: Label3D
var fan_blades: Node3D
var clock_time: float = 0.0
var built: bool = false
var cream: StandardMaterial3D
var ceiling_paint: StandardMaterial3D
var wood: StandardMaterial3D
var teal: StandardMaterial3D
var coral: StandardMaterial3D
var ink: StandardMaterial3D
var gold: StandardMaterial3D
var audio_fx: Node3D
var local_role: String = "human"
var customization_root: Node3D
var customization_actor: ActorView
var customization_role: String = ""
var customization_appearance: Dictionary = {}
var saved_menu_transform: Transform3D
var saved_menu_fov: float = 74.0
var customization_age: float = 0.0
var map_root: Node3D
var current_map: String = ""
var map_data: Dictionary = {}
var marker_ring: Array[MeshInstance3D] = []
var marker_ready_material: StandardMaterial3D
var marker_charge_material: StandardMaterial3D
var marker_quiet_material: StandardMaterial3D
var surface_shader: Shader
var surface_materials: Dictionary = {}
var station_labels: Array[Label3D] = []
var scene_environment: Environment
var contact_material: StandardMaterial3D
var door_views: Node3D
var _house_windows: Array[Dictionary] = []

func build() -> void:
	if built:
		return
	built = true
	add_to_group("video_settings")
	cream = ActorModel.material(Color("f1e7cb"))
	ceiling_paint = ActorModel.material(Color("eddfc8"))
	ceiling_paint.emission_enabled = true
	ceiling_paint.emission = Color("c5aa87")
	ceiling_paint.emission_energy_multiplier = 0.10
	wood = ActorModel.material(Color("bc835b"))
	teal = ActorModel.material(Color("518f88"))
	coral = ActorModel.material(Color("d98069"))
	ink = ActorModel.material(Color("233e4c"))
	gold = ActorModel.material(Color("e8be68"))
	_build_environment()
	_build_marker()
	audio_fx = AudioEffects.new()
	add_child(audio_fx)
	audio_fx.setup()
	menu_camera = Camera3D.new()
	menu_camera.fov = 74.0
	menu_camera.near = 0.05
	add_child(menu_camera)
	load_map("lobby")
	menu_camera.current = true
	saved_menu_transform = menu_camera.transform

func load_map(id: String) -> void:
	if not built:
		build()
	if id == current_map and is_instance_valid(map_root):
		return
	var requested: Dictionary = Maps.get_map(id)
	if requested.is_empty():
		return
	end_customization()
	if is_instance_valid(map_root):
		# Remove from the scene tree first: old colliders cannot survive until the
		# deferred deletion and obstruct a new map for one extra physics frame.
		remove_child(map_root)
		map_root.queue_free()
	fan_blades = null
	station_labels.clear()
	door_views = null
	map_data = requested
	current_map = str(map_data.get("id", "lobby"))
	audio_fx.set_context(current_map)
	scene_environment.ambient_light_energy = 0.38 if current_map == "house" and map_data.has("structures") else 0.73
	scene_environment.ambient_light_color = Color("adc4e4") if current_map == "house" else Color("b6cacf")
	map_root = Node3D.new()
	map_root.name = "Map_" + current_map
	add_child(map_root)
	_build_map_colliders()
	_build_lighting()
	if current_map == "house":
		if map_data.has("structures"):
			_build_catalog_house()
			var spawn: Vector3 = map_data.human_spawns[0]
			menu_camera.position = spawn + Vector3(2.0, 2.2, 2.0)
			menu_camera.look_at(spawn + Vector3(0, 1.1, -1.0))
		else:
			_build_shell()
			_build_sofa()
			_build_tables()
			_build_window()
			_build_fan()
			_build_decor()
			menu_camera.position = Vector3(4.6, 2.35, 4.0)
			menu_camera.look_at(Vector3(-1.0, 0.85, -1.25))
	else:
		_build_lobby()
		menu_camera.position = Vector3(3.25, 2.65, 2.45)
		menu_camera.look_at(Vector3(-0.1, 1.0, -0.65))
	door_views = DoorViewScript.new()
	map_root.add_child(door_views)
	door_views.setup(current_map)
	menu_camera.fov = 74.0
	saved_menu_transform = menu_camera.transform
	saved_menu_fov = menu_camera.fov
	if is_instance_valid(marker):
		marker.visible = false
	apply_video_settings()

func apply_video_settings() -> void:
	VideoSettings.apply_world(self)

func _build_map_colliders() -> void:
	var half_x: float = float(map_data.get("half_x", 4.0))
	var half_z: float = float(map_data.get("half_z", 3.0))
	var ceiling: float = float(map_data.get("ceiling", 4.0))
	_collider(Vector3(0, -0.10, 0), Vector3(half_x * 2 + 0.3, 0.2, half_z * 2 + 0.3))
	_collider(Vector3(0, ceiling + 0.1, 0), Vector3(half_x * 2 + 0.3, 0.2, half_z * 2 + 0.3))
	for side: float in [-1.0, 1.0]:
		_collider(Vector3(side * (half_x + 0.08), ceiling * 0.5, 0), Vector3(0.16, ceiling, half_z * 2 + 0.3))
		_collider(Vector3(0, ceiling * 0.5, side * (half_z + 0.08)), Vector3(half_x * 2 + 0.3, ceiling, 0.16))
	for obstacle: AABB in map_data.get("obstacles", []):
		_collider(obstacle.get_center(), obstacle.size)

func _process(dt: float) -> void:
	clock_time += dt
	var active_camera: Camera3D = get_viewport().get_camera_3d()
	for actor: Variant in actors.values():
		(actor as ActorView).update_name_visibility(active_camera)
		_update_help_icon(actor as ActorView,active_camera)
	for label: Label3D in station_labels:
		if is_instance_valid(label) and is_instance_valid(active_camera):
			_update_world_label(label,active_camera)
	if is_instance_valid(customization_actor):
		customization_age += dt
		_update_customization(dt)
	if is_instance_valid(fan_blades):
		fan_blades.rotation.z += dt * 3.5
	for key: Variant in pickup_views:
		var view: Node3D = pickup_views[key]
		if view.visible:
			var tool_mesh: Node3D = view.get_node("Tool")
			tool_mesh.rotation.y = clock_time * 0.7 + float(int(key))
			var label: Label3D = view.get_node("PickupLabel")
			_update_world_label(label,active_camera)

func _update_help_icon(view: ActorView, camera: Camera3D) -> void:
	if not is_instance_valid(view.help_icon):
		return
	view.help_icon.visible = false
	if view.movement_state!="stunned" or not view.visible or view.local_view or not is_instance_valid(camera) or not actors.has(local_actor_id):
		return
	var observer: ActorView = actors[local_actor_id]
	if observer.actor_role!="mosquito" or not observer.visible or observer.movement_state=="stunned":
		return
	var distance: float = observer.global_position.distance_to(view.global_position)
	if distance>2.5 or distance<0.10 or camera.is_position_behind(view.help_icon.global_position):
		return
	var exclusions: Array[RID] = observer.body_collision_rids()
	exclusions.append_array(view.body_collision_rids())
	if _occluded(camera.global_position,view.help_icon.global_position,exclusions) or _occluded(observer.global_position,view.global_position,exclusions):
		return
	var camera_distance: float = camera.global_position.distance_to(view.help_icon.global_position)
	var focal: float = get_viewport().get_visible_rect().size.y*0.5/tan(deg_to_rad(camera.fov*0.5))
	view.help_icon.pixel_size = minf(0.0015,16.0*camera_distance/(32.0*focal))
	view.help_icon.visible = true

func _update_world_label(label: Label3D, camera: Camera3D) -> void:
	if not is_instance_valid(camera):
		label.visible = false
		return
	var distance: float = camera.global_position.distance_to(label.global_position)
	var screen: Vector2 = camera.unproject_position(label.global_position)
	var viewport_size: Vector2 = get_viewport().get_visible_rect().size
	var inside: bool = Rect2(Vector2(viewport_size.x*0.10,viewport_size.y*0.10),viewport_size*0.8).has_point(screen)
	label.visible = distance>1.25 and distance<3.0 and inside and not camera.is_position_behind(label.global_position) and not _occluded(camera.global_position,label.global_position,[])

func sync_actors(data: Dictionary, local_id: int, dt: float) -> void:
	local_actor_id = local_id
	actor_state = data
	for key: Variant in actors.keys():
		if not data.has(key):
			var stale: Node3D = actors[key]
			stale.queue_free()
			actors.erase(key)
	for key: Variant in data:
		var actor_data: Dictionary = data[key]
		var desired_role: String = str(actor_data.get("role", "mosquito"))
		if actors.has(key) and (actors[key] as ActorView).actor_role != desired_role:
			(actors[key] as ActorView).queue_free()
			actors.erase(key)
		if not actors.has(key):
			var fresh: ActorView = ActorModel.new()
			add_child(fresh)
			fresh.build(str(actor_data.get("role", "mosquito")), str(actor_data.get("name", "Amigo")), int(key))
			fresh.set_local(int(key) == local_id and local_role != "lobby")
			actors[key] = fresh
		var view: ActorView = actors[key]
		var visual_data: Dictionary = actor_data
		if current_map == "lobby" and local_role == "lobby" and desired_role == "human":
			visual_data = actor_data.duplicate()
			visual_data.relaxed_pose = true
		view.update_state(visual_data, dt)
		if is_instance_valid(customization_actor):
			view.visible = false
	if is_instance_valid(audio_fx):
		audio_fx.sync(data, actors, local_id)

func sync_doors(states: Dictionary, dt: float) -> void:
	if is_instance_valid(door_views):
		door_views.sync(states,dt)

func get_actor(id: int) -> Node3D:
	return actors.get(id, null) as Node3D

func set_local_role(id: int, role: String) -> void:
	local_actor_id = id
	local_role = role
	for key: Variant in actors:
		var view: ActorView = actors[key]
		view.set_local(int(key) == id and role != "lobby")

func show_customization(role: String, appearance: Dictionary) -> void:
	if role != "human" and role != "mosquito":
		return
	if not built:
		build()
	if not is_instance_valid(customization_root):
		saved_menu_transform = menu_camera.transform
		saved_menu_fov = menu_camera.fov
		customization_root = Node3D.new()
		customization_root.name = "CustomizationPreview"
		customization_root.position = Vector3(-1.2, 0, 1.1)
		add_child(customization_root)
		_disc(customization_root, Vector3(0, 0.026, 0), 0.72, 0.05, teal)
		_disc(customization_root, Vector3(0, 0.054, 0), 0.64, 0.010, cream)
		var lamp := OmniLight3D.new()
		lamp.position = Vector3(1.0, 1.8, -1.3)
		lamp.light_color = Color("fff0db")
		lamp.light_energy = 0.8
		lamp.omni_range = 4.5
		lamp.shadow_enabled = false
		customization_root.add_child(lamp)
	if not is_instance_valid(customization_actor) or customization_role != role:
		if is_instance_valid(customization_actor):
			customization_actor.get_parent().remove_child(customization_actor)
			customization_actor.queue_free()
		customization_actor = ActorModel.new()
		customization_root.add_child(customization_actor)
		customization_actor.build(role, "", 0)
		customization_actor.preview_only = true
		customization_actor.set_local(false)
		customization_actor.name_label.visible = false
		customization_actor.scale = Vector3.ONE * (3.5 / ActorModel.MOSQUITO_VISUAL_SCALE if role == "mosquito" else 1.0)
		customization_age = 0.0
	customization_role = role
	customization_appearance = appearance.duplicate(true)
	_update_customization(1.0)
	for value: Variant in actors.values():
		(value as Node3D).visible = false
	marker.visible = false
	menu_camera.fov = 44.0
	menu_camera.global_position = customization_root.global_position + Vector3(1.3, 1.45, -3.1)
	# Offset the focus so the actor occupies the left/center beside the UI panel.
	menu_camera.look_at(customization_root.global_position + Vector3(-0.82, 0.98, 0.15))
	menu_camera.make_current()

func _update_customization(dt: float) -> void:
	if not is_instance_valid(customization_actor):
		return
	var position: Vector3 = customization_root.global_position + Vector3(0, 1.05 if customization_role == "mosquito" else 0.06, 0)
	var yaw: float = (-0.72 if customization_role == "mosquito" else -0.22) + sin(customization_age * 0.6) * 0.26
	customization_actor.update_state({"p":position, "yaw":yaw, "state":"flying" if customization_role == "mosquito" else "human", "alive":true, "appearance":customization_appearance}, dt)

func end_customization() -> void:
	if not is_instance_valid(customization_root):
		return
	customization_root.queue_free()
	customization_root = null
	customization_actor = null
	customization_role = ""
	customization_appearance = {}
	for key: Variant in actors:
		(actors[key] as Node3D).visible = bool(Dictionary(actor_state.get(key, {})).get("alive", true))
	menu_camera.transform = saved_menu_transform
	menu_camera.fov = saved_menu_fov
	menu_camera.make_current()

func sync_pickups(pickups: Dictionary) -> void:
	for key: Variant in pickup_views.keys():
		if not pickups.has(key):
			var stale: Node3D = pickup_views[key]
			stale.queue_free()
			pickup_views.erase(key)
	for key: Variant in pickups:
		var data: Dictionary = pickups[key]
		var tool: String = str(data.get("tool", "swatter"))
		if not pickup_views.has(key):
			var stand := Node3D.new()
			add_child(stand)
			var halo: MeshInstance3D = _disc(stand, Vector3(0, 0.025, 0), 0.29, 0.022, teal)
			halo.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			var tool_model: Node3D = ActorModel.make_tool(tool)
			tool_model.name = "Tool"
			tool_model.position.y = 0.66 if tool == "broom" else 0.46
			tool_model.rotation.z = -0.6
			stand.add_child(tool_model)
			var names: Dictionary = {"swatter": "MATAMOSCAS", "racket": "RAQUETA", "newspaper": "DIARIO", "broom": "ESCOBA"}
			var label: Label3D = _label(stand, str(names.get(tool, tool)), Vector3(0, 0.68, 0), 0.00155, Color("ffefba"), true)
			label.name = "PickupLabel"
			label.font_size = 28
			pickup_views[key] = stand
		var view: Node3D = pickup_views[key]
		view.position = data.get("p", Vector3.ZERO)
		view.rotation.y = float(data.get("yaw", 0.0))
		view.visible = int(data.get("holder", 0)) == 0

func clear_actors() -> void:
	if is_instance_valid(audio_fx):
		audio_fx.clear()
	for value: Variant in actors.values():
		(value as Node3D).queue_free()
	actors.clear()
	actor_state = {}
	for value: Variant in pickup_views.values():
		(value as Node3D).queue_free()
	pickup_views.clear()
	if is_instance_valid(marker):
		marker.visible = false

func show_assignment(data: Dictionary, camera: Camera3D, mosquito_position: Vector3, focus: Dictionary = {}) -> void:
	if not is_instance_valid(marker):
		return
	marker.visible = false
	if data.is_empty() or not is_instance_valid(camera):
		return
	var point: Vector3 = data.get("p", Vector3.ZERO)
	var normal: Vector3 = data.get("normal", Vector3.FORWARD)
	var target_id: int = int(data.get("human", 0))
	# Render the server's assigned point on the interpolated target transform.
	# This keeps the private diamond attached while that human walks or turns.
	if actors.has(target_id) and actor_state.has(target_id):
		var target_view: Node3D = actors[target_id]
		var target_state: Dictionary = actor_state[target_id]
		var authoritative_position: Vector3 = target_state.get("p", Vector3.ZERO)
		var authoritative_yaw: float = float(target_state.get("body_yaw", target_state.get("yaw", 0.0)))
		var local_point: Vector3 = (point - authoritative_position).rotated(Vector3.UP, -authoritative_yaw)
		point = target_view.global_position + local_point.rotated(Vector3.UP, target_view.rotation.y)
		normal = normal.rotated(Vector3.UP, target_view.rotation.y - authoritative_yaw)
	if normal.length_squared() < 0.5:
		return
	normal = normal.normalized()
	var surface: Vector3 = point + normal * 0.009
	# Both the insect and its third-person camera must see the assigned side.
	# The target's body participates in these rays, so a camera around a torso
	# cannot reveal a point through that torso. No public state is consulted here.
	if normal.dot(mosquito_position - point) < -0.015:
		return
	if normal.dot(camera.global_position - point) <= 0.005:
		return
	if camera.is_position_behind(surface):
		return
	var exclude: Array[RID] = []
	if actors.has(local_actor_id):
		exclude = (actors[local_actor_id] as ActorView).body_collision_rids()
	if _occluded(camera.global_position, surface, exclude):
		return
	if mosquito_position.distance_to(surface) > 0.12 and _occluded(mosquito_position, surface, exclude):
		return
	marker.global_position = surface
	var up: Vector3 = Vector3.UP if absf(normal.dot(Vector3.UP)) < 0.95 else Vector3.FORWARD
	marker.look_at(surface + normal, up)
	marker.scale = Vector3.ONE
	# The HUD names the zone; keep the body mark free of overlapping name labels.
	marker_label.text = ""
	var state: String = str(focus.get("state", ""))
	var progress: float = 1.0 if state == "biting" else clampf(float(focus.get("progress", 0.0)), 0.0, 1.0)
	for i: int in range(marker_ring.size()):
		var sector: MeshInstance3D = marker_ring[i]
		sector.visible = bool(focus.get("can_focus", false)) or progress > 0.0
		sector.material_override = marker_charge_material if float(i + 1) / marker_ring.size() <= progress else marker_quiet_material
	marker.visible = true

func _occluded(from: Vector3, to: Vector3, exclude: Array[RID]) -> bool:
	if from.distance_squared_to(to) < 0.0001:
		return false
	var query := PhysicsRayQueryParameters3D.create(from, to, 3, exclude)
	return not get_world_3d().direct_space_state.intersect_ray(query).is_empty()

func play_event(_verb: String) -> void:
	pass

func _build_environment() -> void:
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	scene_environment = environment
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("213f55")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("b6cacf")
	environment.ambient_light_energy = 0.73
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment_node.environment = environment
	add_child(environment_node)

func _build_lighting() -> void:
	var moon := DirectionalLight3D.new()
	moon.rotation_degrees = Vector3(-44, -24, 0)
	moon.light_color = Color("c9dfed")
	# An outdoor directional lamp with a 20m shadow range illuminated interior
	# walls beyond the cascade. House moonlight now comes from actual windows.
	moon.light_energy = 0.0 if current_map == "house" else 0.35
	moon.shadow_enabled = current_map != "house"
	moon.directional_shadow_max_distance = 20.0
	map_root.add_child(moon)
	if current_map == "house" and map_data.has("structures"):
		# Key lamps are warm, bounded and directional. Four distance-faded maps
		# serve the three focal rooms and stairs; other rooms use cheap fill.
		for room: Dictionary in map_data.get("rooms", []):
			var bounds: AABB = room.bounds
			var spot := SpotLight3D.new()
			spot.position = Vector3(bounds.get_center().x,bounds.position.y+(2.49 if int(room.floor)==0 else 2.69),bounds.get_center().z)
			spot.light_color = Color("f6d5ad")
			spot.light_energy = 1.85
			spot.spot_range = 4.2
			spot.spot_angle = 62.0
			spot.shadow_enabled = str(room.name) in ["Dormitorio rosa","Sala de estar","Cocina"]
			spot.shadow_bias = 0.1
			spot.shadow_normal_bias = 1.0
			spot.distance_fade_enabled = true
			spot.distance_fade_begin = 6.5
			spot.distance_fade_shadow = 5.5
			spot.distance_fade_length = 1.5
			map_root.add_child(spot)
			spot.rotation.x=-PI/2
		var stair_light := SpotLight3D.new()
		stair_light.position = Vector3(-11.0,5.9,-1.4)
		stair_light.light_color = Color("d1e6f0")
		stair_light.light_energy = 1.2
		stair_light.spot_range = 7.0
		stair_light.spot_angle = 62.0
		stair_light.shadow_enabled = true
		stair_light.shadow_bias = 0.1
		stair_light.shadow_normal_bias = 1.0
		stair_light.distance_fade_enabled = true
		stair_light.distance_fade_begin = 7.0
		stair_light.distance_fade_shadow = 6.5
		stair_light.distance_fade_length = 1.0
		map_root.add_child(stair_light)
		stair_light.look_at(Vector3(-11.0,1.0,-2.4))
		for floor_index: int in range(2):
			for z: float in [-7.8,0.0,7.8]:
				var hall_lamp := SpotLight3D.new()
				hall_lamp.position = Vector3(0,2.45+floor_index*3.2,z)
				hall_lamp.light_color = Color("ffd09a")
				hall_lamp.light_energy = 0.9
				hall_lamp.spot_range = 3.4
				hall_lamp.spot_angle = 60.0
				hall_lamp.rotation.x=-PI/2
				hall_lamp.shadow_enabled = false
				map_root.add_child(hall_lamp)
		return
	var lamp := OmniLight3D.new()
	lamp.position = Vector3(-3.1, 2.25, 1.0) if current_map == "house" else Vector3(0, 3.0, -1.8)
	lamp.light_color = Color("ffd895")
	lamp.light_energy = 1.7 if current_map == "house" else 0.65
	lamp.omni_range = 7.5
	lamp.shadow_enabled = false
	map_root.add_child(lamp)
	var counter_light := OmniLight3D.new()
	counter_light.position = Vector3(3.2, 2.3, -2.2) if current_map == "house" else Vector3(-2.8, 2.5, 1.2)
	counter_light.light_color = Color("ffe6af")
	counter_light.light_energy = 1.0 if current_map == "house" else 0.35
	counter_light.omni_range = 5.5
	counter_light.shadow_enabled = false
	map_root.add_child(counter_light)

func _surface_material(tint: Color, kind: String) -> Material:
	var key: String = tint.to_html() + ":" + kind
	if surface_materials.has(key):
		return surface_materials[key]
	if not is_instance_valid(surface_shader):
		surface_shader = Shader.new()
		surface_shader.code = """shader_type spatial;
render_mode diffuse_burley;
uniform vec4 tint : source_color = vec4(1.0);
uniform float surface_kind = 0.0;
varying vec3 p;
varying vec3 n;
void vertex(){ p = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; n = mat3(MODEL_MATRIX) * NORMAL; }
float hash(vec2 v){return fract(sin(dot(v,vec2(127.1,311.7))) * 43758.5453);}
void fragment(){
 float shade = 0.98;
 if(surface_kind < 0.5){
  vec3 face = abs(normalize(n));
  vec2 plane = face.y > 0.5 ? p.xz : (face.z > 0.5 ? p.xy : p.zy);
  vec2 board = vec2(plane.x*3.2,plane.y*0.5);
  board.y += hash(vec2(floor(board.x),0.0));
  vec2 f = fract(board);
  float seam = step(0.985,f.x) + step(0.985,f.y);
  float grain = sin(plane.x*22.0 + sin(plane.y*2.8)*1.3)*0.012;
  shade = 0.96 + hash(floor(board))*0.055 + grain - min(seam,1.0)*0.10;
 } else if(surface_kind < 1.5){
  shade = 0.98 + (hash(floor(p.xy*18.0+p.zy*13.0))-0.5)*0.02;
 } else if(surface_kind < 2.5){
  shade = 0.98 + sin(UV.x*70.0)*sin(UV.y*70.0)*0.012;
 } else if(surface_kind < 3.5){
  float top = step(0.5,normalize(n).y);
  shade = mix(0.66,0.99,top) + sin(UV.x*32.0+sin(UV.y*5.0))*0.025;
 } else if(surface_kind < 4.5){
  vec3 face = abs(normalize(n));
  vec2 plane = face.y>0.5 ? p.xz : (face.z>0.5 ? p.xy : p.zy);
  vec2 grid = plane*4.0;
  vec2 tile = fract(grid);
  vec2 distance_to_joint = min(tile,1.0-tile);
  vec2 footprint = max(fwidth(grid),vec2(0.002));
  vec2 joint = 1.0-smoothstep(vec2(0.018),vec2(0.018)+footprint,distance_to_joint);
  float seam = max(joint.x,joint.y);
  shade = 1.0-seam*0.22;
 } else {
  vec3 face = abs(normalize(n));
  float horizontal = face.z>0.5 ? p.x : p.z;
  float u = fract(horizontal*1.55);
  shade = .96 - step(.974,u)*.12 + step(.02,u)*step(u,.035)*.04;
 }
 ALBEDO = tint.rgb * shade;
 ROUGHNESS = 0.88;
}"""
	var material := ShaderMaterial.new()
	material.shader = surface_shader
	material.set_shader_parameter("tint", tint)
	material.set_shader_parameter("surface_kind", 0.0 if kind == "wood" else 1.0 if kind == "wall" else 3.0 if kind == "step" else 4.0 if kind == "tile" else 5.0 if kind == "panel" else 2.0)
	surface_materials[key] = material
	return material

func _build_catalog_house() -> void:
	_house_windows = HouseDetails.window_specs(map_data)
	# Every solid starts with the same AABB the server uses. Doorways and both
	# stair wells are actual gaps in the catalog, not decorative painted doors.
	for structure: Dictionary in map_data.get("structures", []):
		var bounds: AABB = structure.box
		var kind: String = str(structure.get("kind", "wall"))
		var tint: Color = structure.get("color", Color("d2d5c2"))
		if kind == "floor":
			tint = tint.darkened(0.16)
		elif kind == "step":
			tint = Color("967150")
		if kind == "furniture":
			_furniture_from_catalog(structure)
			continue
		var material: Material = _surface_material(tint, "wood" if kind == "floor" else "step" if kind == "step" else "wall")
		var visual_parts: Array[AABB] = []
		if kind=="wall":
			visual_parts = HouseDetails.wall_pieces(bounds,_house_windows)
		elif kind=="floor":
			# Keep light selection local: one house-wide mesh exceeded the eight
			# Compatibility spot lights per object even with bounded lamps.
			visual_parts = HouseDetails.floor_pieces(bounds)
		else:
			visual_parts.append(bounds)
		for visual_box: AABB in visual_parts:
			var piece: MeshInstance3D = _box(self,visual_box.get_center(),visual_box.size,material)
			piece.set_meta("catalog_box",bounds)
			piece.set_meta("catalog_kind",kind)
		if kind == "floor" and bounds.position.y > 1.0:
			_box(self, Vector3(bounds.get_center().x, bounds.position.y - 0.006, bounds.get_center().z), Vector3(bounds.size.x, 0.01, bounds.size.z), ceiling_paint)
		if str(structure.get("label",""))=="Dintel":
			# The apparent triangles at corridor door tops are visible lintel
			# undersides (confirmed by physics rays), not holes in the shell.
			_box(self,Vector3(bounds.get_center().x,bounds.position.y+.008,bounds.get_center().z),Vector3(bounds.size.x,.020,bounds.size.z),wood)
		if kind == "wall" and bounds.size.y > 1.0:
			HouseDetails.trim(self,bounds)
		elif kind == "step":
			var nosing_size := Vector3(bounds.size.x, 0.033, minf(bounds.size.z, 0.055))
			var front_z: float = bounds.size.z - 0.026 if bounds.get_center().x > 0.0 else 0.026
			_box(self, bounds.position + Vector3(bounds.size.x * 0.5, bounds.size.y - 0.016, front_z), nosing_size, gold)
	# A quiet ceiling closes the top floor. The catalog ceiling is a physical
	# limit, and the lower floor slabs already form the ground-floor ceilings.
	_box(self, Vector3(0, float(map_data.ceiling) + 0.075, 0), Vector3(float(map_data.half_x) * 2.0, 0.15, float(map_data.half_z) * 2.0), ceiling_paint)
	for room: Dictionary in map_data.get("rooms", []):
		_build_room_details(room)
	_finish_house_art()
	for station: Dictionary in map_data.get("stations", []):
		_build_station_at(station)
	for floor_index: int in range(2):
		var y: float = float(floor_index) * 3.2
		for side: float in [-1.0, 1.0]:
			var label: Label3D = _label(self, "01 / PLANTA BAJA" if floor_index == 0 else "02 / PLANTA ALTA", Vector3(side * 11.0, y + 2.35, 4.0 if side < 0 else -4.0), 0.0045, Color("264c56"))
			label.rotation.y = PI if side < 0 else 0.0

func _furniture_from_catalog(data: Dictionary) -> void:
	var bounds: AABB = data.box
	var tint: Color = data.get("color", Color("91b1ad"))
	var detail: Dictionary = data.duplicate()
	for room: Dictionary in map_data.get("rooms", []):
		if AABB(room.bounds).has_point(bounds.get_center()):
			detail.room_center = AABB(room.bounds).get_center()
			detail.room_name = str(room.name)
			if "Cocina" in str(room.name): tint = Color("93aca0")
			elif "Dormitorio" in str(room.name): tint = _room_color(room).lightened(.25)
			if str(data.get("style", "")) == "sofa":
				tint = Color("648c88") if int(room.floor)==0 else Color("b77b64")
			break
	var root := Node3D.new()
	root.position = bounds.position
	root.set_meta("catalog_box", bounds)
	root.set_meta("catalog_kind", "furniture")
	map_root.add_child(root)
	_add_contact_shadow(bounds)
	Furnishings.build(root,detail,tint)
func _build_room_details(room: Dictionary) -> void:
	HouseDetails.build_room(self,room)

func _dress_window(window: Node3D, tint: Color, short_curtain: bool = false) -> void:
	_segment(window,Vector3(-1.04,0.85,0.13),Vector3(1.04,0.85,0.13),0.025,gold)
	for side: float in [-1.0,1.0]:
		var curtain := Furnishings.instantiate_asset("curtain")
		curtain.position = Vector3(side*.81,-.73 if not short_curtain else -.11,.11)
		curtain.scale = Vector3(1.0,.99 if not short_curtain else .60,1.0)
		window.add_child(curtain)
		Furnishings._tint_cloth(curtain,tint.lightened(.25))
		_sphere(window,Vector3(side*1.06,.85,.13),Vector3.ONE*.045,gold)
	var glass := ActorModel.material(Color("172e55"))
	glass.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_box(window,Vector3(0,0,.04),Vector3(1.30,1.20,.006),glass)
	var star := ActorModel.material(Color("bccde3"))
	star.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_sphere(window,Vector3(-.35,.29,.047),Vector3(.105,.105,.003),star)
	for point: Vector2 in [Vector2(.40,.39),Vector2(.22,-.28),Vector2(-.47,-.19)]:
		_sphere(window,Vector3(point.x,point.y,.048),Vector3(.012,.012,.002),star)
	var moonlight := SpotLight3D.new()
	moonlight.position = Vector3(0,.05,.10)
	moonlight.light_color = Color("90b8ff")
	moonlight.light_energy = .65
	moonlight.spot_range = 4.5
	moonlight.spot_angle = 48
	moonlight.shadow_enabled = false
	window.add_child(moonlight)
	moonlight.look_at(window.to_global(Vector3(0,-1.0,3.0)))

func _finish_house_art() -> void:
	HouseDetails.finish(self)

func _room_color(room: Dictionary) -> Color:
	var label: String = str(room.get("label", ""))
	if "ROSA" in label:
		return Color("a7707b")
	if "VERDE" in label:
		return Color("557d70")
	if "AZUL" in label:
		return Color("627f9e")
	return Color(room.get("color",Color("6c9e98"))).darkened(0.10)

func _room_wall_finish(bounds: AABB, color: Color, kitchen: bool) -> void:
	# Paint only existing vertical wall faces. Door/lintel AABBs remain gaps;
	# no panels bridge the doorways or alter the catalogue collision envelope.
	for structure: Dictionary in map_data.structures:
		if str(structure.kind)!="wall":
			continue
		var solid: AABB = structure.box
		if solid.position.y>=bounds.end.y or solid.end.y<=bounds.position.y:
			continue
		for axis: int in [0,2]:
			var other: int = 2 if axis==0 else 0
			var low: float = maxf(solid.position[other],bounds.position[other])
			var high: float = minf(solid.end[other],bounds.end[other])
			if high-low<0.12:
				continue
			var surface: float = 0.0
			if absf(solid.end[axis]-bounds.position[axis])<0.2:
				surface = solid.end[axis]+0.005
			elif absf(solid.position[axis]-bounds.end[axis])<0.2:
				surface = solid.position[axis]-0.005
			else:
				continue
			var at := Vector3.ZERO
			at[axis] = surface
			at[other] = (low+high)*0.5
			var paint_low:float=maxf(solid.position.y,bounds.position.y+.12)
			var paint_high:float=minf(solid.end.y,bounds.end.y-.02)
			if paint_high<=paint_low:continue
			at.y = (paint_low+paint_high)*.5
			var size := Vector3(0.007,paint_high-paint_low,0.007)
			size[other] = high-low
			_finished_wall_box(at,size,_surface_material(Color("a4b8ac") if kitchen else color.lightened(0.20),"wall")).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			if solid.position.y>bounds.position.y+.2:continue
			at.y = bounds.position.y+0.71
			size.y = 1.18
			at[axis] += 0.002 if surface>solid.end[axis] else -0.002
			_finished_wall_box(at,size,_surface_material(Color("c5cec0") if kitchen else color.darkened(0.32),"tile" if kitchen else "panel")).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			at.y = bounds.position.y+1.32
			size.y = 0.035
			size[axis] = 0.022
			_finished_wall_box(at,size,wood).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

func _finished_wall_box(at: Vector3, size: Vector3, material: Material) -> MeshInstance3D:
	var final_piece: MeshInstance3D
	for piece: AABB in HouseDetails.wall_pieces(AABB(at-size*.5,size),_house_windows):
		final_piece=_box(self,piece.get_center(),piece.size,material)
		final_piece.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return final_piece

func _add_contact_shadow(bounds: AABB) -> void:
	if not is_instance_valid(contact_material):
		var image := Image.create(64,64,false,Image.FORMAT_RGBA8)
		for y: int in range(64):
			for x: int in range(64):
				var uv := Vector2((float(x)+0.5)/32.0-1.0,(float(y)+0.5)/32.0-1.0)
				var fade: float = 1.0-smoothstep(0.69,1.0,maxf(absf(uv.x),absf(uv.y)))
				image.set_pixel(x,y,Color(0.12,0.09,0.08,fade*0.24))
		image.generate_mipmaps()
		contact_material = ActorModel.material(Color.WHITE)
		contact_material.albedo_texture = ImageTexture.create_from_image(image)
		contact_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		contact_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		contact_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	var quad := QuadMesh.new()
	quad.size = Vector2(bounds.size.x+0.32,bounds.size.z+0.32)
	var shadow: MeshInstance3D = ActorModel.mesh(map_root,quad,Vector3(bounds.get_center().x,bounds.position.y+0.025,bounds.get_center().z),contact_material)
	shadow.rotation.x = -PI/2.0
	shadow.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

func _room_wall_surface(bounds: AABB) -> Dictionary:
	var origin := Vector3(bounds.get_center().x,bounds.position.y+2.12,bounds.get_center().z)
	for direction: Vector3 in [Vector3.FORWARD,Vector3.BACK,Vector3.LEFT,Vector3.RIGHT]:
		var length: float = (bounds.size.z if absf(direction.z)>0.5 else bounds.size.x)*0.5+0.4
		var nearest: Variant = null
		var distance: float = length+1.0
		for structure: Dictionary in map_data.structures:
			if str(structure.kind) != "wall":
				continue
			var box: AABB = structure.box
			var hit: Variant = box.intersects_segment(origin,origin+direction*length)
			if hit != null and origin.distance_to(hit)<distance:
				nearest = hit
				distance = origin.distance_to(hit)
		if nearest != null:
			return {"p":nearest,"normal":-direction}
	return {}

func _build_station_at(station: Dictionary) -> void:
	var point: Vector3 = station.p
	var label: String = str(station.get("label", "TAREA"))
	var root := Node3D.new()
	root.position = point
	map_root.add_child(root)
	_disc(root, Vector3(0, 0.012, 0), 0.33, 0.018, teal)
	station_labels.append(_label(root, label, Vector3(0, 0.72, 0), 0.00155, Color("fff1bf"), true))
	if "VENTIL" in label:
		_segment(root, Vector3(0, 0.05, 0), Vector3(0, 1.15, 0), 0.035, ink)
		_sphere(root, Vector3(0, 1.23, 0), Vector3(0.29, 0.29, 0.04), teal)
		for angle: float in [0.0, TAU/3.0, TAU*2.0/3.0]:
			var blade: MeshInstance3D = _sphere(root, Vector3(sin(angle) * 0.12, 1.23 + cos(angle) * 0.12, -0.05), Vector3(0.065, 0.17, 0.017), cream)
			blade.rotation.z = -angle
	elif label in ["VENTANA","MOSQUITERO"]:
		# A low, clear interaction cue points toward the real wall window; do
		# not invent a bottle-shaped 'window' in the middle of the floor.
		for side: float in [-1.0,1.0]:
			_segment(root,Vector3(side*0.13,0.033,-0.06),Vector3(0,0.033,-0.20),0.018,cream)
		_segment(root,Vector3(0,0.033,0.10),Vector3(0,0.033,-0.18),0.019,cream)
	elif label in ["MANTAS","SÁBANAS"]:
		for layer: int in range(3):
			SculptedShape.rounded(root,Vector3(layer*0.014,0.07+layer*0.075,0),Vector3(0.40,0.08,0.28),0.037,cream if layer%2 else coral)
	elif label=="VAJILLA":
		for layer: int in range(5):
			_disc(root,Vector3(0,0.04+layer*0.025,0),0.16,0.022,cream)
	elif label=="EQUIPO":
		HouseDetails.place_station_radio(self,point)
		# The floor disc marks the working position beside the actual furniture.
		for side:float in [-1.0,1.0]:
			_segment(root,Vector3(side*.11,.033,-.03),Vector3(0,.033,-.15),.013,cream)
	else:
		_capsule(root,Vector3(0,0.16,0),0.115,0.29,teal)
		_disc(root,Vector3(0,0.315,0),0.062,0.06,gold)
		_box(root,Vector3(0.03,0.356,0),Vector3(0.16,0.027,0.06),cream)
		_box(root,Vector3(0,0.18,-0.112),Vector3(0.10,0.11,0.012),cream)

func _build_shell() -> void:
	_box(self, Vector3(0, -0.10, 0), Vector3(12.3, 0.2, 10.3), wood)
	var wall_mat: StandardMaterial3D = ActorModel.material(Color("cfdbc9"))
	_box(self, Vector3(0, 1.4, -5.08), Vector3(12.2, 2.8, 0.16), wall_mat)
	_box(self, Vector3(0, 1.4, 5.08), Vector3(12.2, 2.8, 0.16), wall_mat)
	_box(self, Vector3(-6.08, 1.4, 0), Vector3(0.16, 2.8, 10.2), wall_mat)
	_box(self, Vector3(6.08, 1.4, 0), Vector3(0.16, 2.8, 10.2), wall_mat)
	_box(self, Vector3(0, 2.90, 0), Vector3(12.2, 0.2, 10.2), cream)
	for z: float in [-4.965, 4.965]:
		_box(self, Vector3(0, 0.08, z), Vector3(12, 0.16, 0.055), cream)
		_box(self, Vector3(0, 2.69, z), Vector3(12, 0.12, 0.08), cream)
	for x: float in [-5.965, 5.965]:
		_box(self, Vector3(x, 0.08, 0), Vector3(0.055, 0.16, 10), cream)
		_box(self, Vector3(x, 2.69, 0), Vector3(0.08, 0.12, 10), cream)
	var seam_mat: StandardMaterial3D = ActorModel.material(Color("a76f4f"))
	for x: int in range(-11, 12):
		_box(self, Vector3(float(x) * 0.5, 0.002, 0), Vector3(0.012, 0.005, 10), seam_mat)
	var rug_mat: StandardMaterial3D = ActorModel.material(Color("dfb77e"))
	var rug: MeshInstance3D = _disc(self, Vector3(-2.8, 0.014, 0.1), 1.4, 0.022, teal)
	rug.scale = Vector3(1.45, 1, 1.15)
	var rug_inner: MeshInstance3D = _disc(self, Vector3(-2.8, 0.027, 0.1), 1.3, 0.008, rug_mat)
	rug_inner.scale = Vector3(1.44, 1, 1.14)
	for z: float in [-0.50, 0, 0.5]:
		_box(self, Vector3(-2.8, 0.035, z), Vector3(2.1, 0.003, 0.025), cream)

func _build_sofa() -> void:
	# All principal upholstery remains inside the shared sofa AABB.
	_box(self, Vector3(-4.3, 0.27, 2.025), Vector3(2.3, 0.28, 0.92), coral)
	_sphere(self, Vector3(-4.3, 0.64, 2.37), Vector3(1.13, 0.155, 0.15), coral)
	for x: float in [-4.91, -4.3, -3.69]:
		_sphere(self, Vector3(x, 0.44, 2.00), Vector3(0.34, 0.125, 0.40), coral)
		_sphere(self, Vector3(x, 0.64, 2.26), Vector3(0.33, 0.15, 0.13), coral)
	for x: float in [-5.35, -3.25]:
		_sphere(self, Vector3(x, 0.52, 2.03), Vector3(0.15, 0.23, 0.50), coral)
	for x: float in [-5.14, -3.45]:
		var pillow: MeshInstance3D = _sphere(self, Vector3(x, 0.62, 1.98), Vector3(0.21, 0.18, 0.1), cream)
		pillow.rotation.z = -0.2 if x < -4 else 0.25
	for x: float in [-5.1, -3.5]:
		for z: float in [1.65, 2.4]:
			_disc(self, Vector3(x, 0.095, z), 0.055, 0.19, ink)
	# Arc floor lamp and warm cone, kept near the wall away from play space.
	_disc(self, Vector3(-5.55, 0.045, 3.4), 0.28, 0.07, ink)
	_segment(self, Vector3(-5.55, 0.05, 3.4), Vector3(-5.55, 1.85, 3.4), 0.025, gold)
	_segment(self, Vector3(-5.55, 1.85, 3.4), Vector3(-4.9, 2.03, 3.2), 0.025, gold)
	_cone(self, Vector3(-4.9, 1.90, 3.2), 0.18, 0.30, 0.33, cream)

func _build_tables() -> void:
	var tabletop: MeshInstance3D = _disc(self, Vector3(-4.0, 0.455, -0.1), 0.70, 0.09, wood)
	tabletop.scale.z = 0.70
	for x: float in [-4.5, -3.5]:
		for z: float in [-0.4, 0.2]:
			_segment(self, Vector3(x, 0, z), Vector3(x, 0.44, z), 0.035, ink)
	_box(self, Vector3(-3.94, 0.515, -0.10), Vector3(0.34, 0.034, 0.25), teal)
	_box(self, Vector3(-3.92, 0.535, -0.11), Vector3(0.30, 0.007, 0.22), cream)
	_disc(self, Vector3(-4.37, 0.57, -0.10), 0.075, 0.14, cream)
	_disc(self, Vector3(-4.37, 0.642, -0.10), 0.057, 0.006, ink)
	_box(self, Vector3(4.4, 0.57, 2.6), Vector3(1.0, 1.04, 1.8), teal)
	_box(self, Vector3(4.4, 1.065, 2.6), Vector3(1.0, 0.07, 1.8), wood)
	for z: float in [2.15, 3.0]:
		_box(self, Vector3(3.888, 0.61, z), Vector3(0.02, 0.70, 0.76), teal)
		_sphere(self, Vector3(3.864, 0.67, z), Vector3(0.025, 0.027, 0.027), gold)
	# Task counter and original glass bottle silhouette.
	_box(self, Vector3(3.75, 0.38, -3.35), Vector3(1.9, 0.76, 0.9), coral)
	_box(self, Vector3(3.75, 0.775, -3.35), Vector3(1.9, 0.05, 0.9), cream)
	for x: float in [3.2, 4.2]:
		_box(self, Vector3(x, 0.40, -2.89), Vector3(0.78, 0.51, 0.02), coral)
		_segment(self, Vector3(x - 0.10, 0.54, -2.864), Vector3(x + 0.10, 0.54, -2.864), 0.015, gold)
	_disc(self, Vector3(3.20, 0.83, -3.23), 0.26, 0.03, teal)
	_capsule(self, Vector3(3.20, 1.0, -3.23), 0.10, 0.29, teal)
	_disc(self, Vector3(3.20, 1.175, -3.23), 0.055, 0.08, gold)
	_box(self, Vector3(3.20, 1.00, -3.325), Vector3(0.11, 0.13, 0.01), cream)
	_label(self, "REPELENTE", Vector3(3.72, 1.70, -4.94), 0.005, Color("294851"))
	_label(self, "03  /  MEZCLAR Y LISTO", Vector3(3.72, 1.44, -4.94), 0.0027, Color("294851"))
	_box(self, Vector3(3.75, 2.10, -4.78), Vector3(2.4, 0.09, 0.40), wood)
	for x: float in [2.91, 3.26, 3.66, 4.15, 4.51]:
		_capsule(self, Vector3(x, 2.25, -4.78), 0.07, 0.24, cream if x < 3.7 else teal)

func _build_window() -> void:
	var night: StandardMaterial3D = ActorModel.material(Color("203c60"))
	night.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_box(self, Vector3(-2.5, 1.70, -4.962), Vector3(2.6, 1.48, 0.018), night)
	var moon_mat: StandardMaterial3D = ActorModel.material(Color("f9e6ad"))
	moon_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_sphere(self, Vector3(-3.22, 2.05, -4.932), Vector3(0.15, 0.15, 0.012), moon_mat)
	for pos: Vector2 in [Vector2(-2.95, 1.48), Vector2(-1.48, 2.19), Vector2(-2.3, 2.28), Vector2(-1.9, 1.70)]:
		_sphere(self, Vector3(pos.x, pos.y, -4.94), Vector3(0.012, 0.012, 0.008), moon_mat)
	for x: float in [-3.85, -2.5, -1.15]:
		_box(self, Vector3(x, 1.70, -4.895), Vector3(0.08, 1.63, 0.12), cream)
	for y: float in [0.9, 1.7, 2.5]:
		_box(self, Vector3(-2.5, y, -4.895), Vector3(2.78, 0.07, 0.12), cream)
	_box(self, Vector3(-2.5, 0.875, -4.79), Vector3(2.95, 0.1, 0.35), wood)
	for x: float in [-4.07, -0.93]:
		_sphere(self, Vector3(x, 1.64, -4.80), Vector3(0.19, 0.89, 0.11), teal)
	_segment(self, Vector3(-4.3, 2.58, -4.72), Vector3(-0.7, 2.58, -4.72), 0.025, ink)
	_label(self, "01  /  VENTANA", Vector3(-2.5, 0.62, -4.91), 0.0038, Color("294851"))

func _build_fan() -> void:
	var fan_root := Node3D.new()
	fan_root.position = Vector3(5.66, 1.76, -0.6)
	fan_root.rotation.y = -PI / 2.0
	map_root.add_child(fan_root)
	_box(fan_root, Vector3(0, -0.16, -0.04), Vector3(0.10, 0.49, 0.12), teal)
	_sphere(fan_root, Vector3(0, 0, -0.11), Vector3(0.18, 0.18, 0.15), teal)
	fan_blades = Node3D.new()
	fan_blades.position.z = 0.02
	fan_root.add_child(fan_blades)
	for angle: float in [0.0, TAU / 3, TAU * 2 / 3]:
		var blade: MeshInstance3D = _sphere(fan_blades, Vector3(sin(angle) * 0.19, cos(angle) * 0.19, 0), Vector3(0.11, 0.23, 0.025), cream)
		blade.rotation.z = -angle + 0.3
	var rim := TorusMesh.new()
	rim.inner_radius = 0.39
	rim.outer_radius = 0.411
	rim.rings = 32
	rim.ring_segments = 6
	var ring: MeshInstance3D = ActorModel.mesh(fan_root, rim, Vector3(0, 0, 0.055), ink)
	ring.rotation.x = PI / 2.0
	for angle: float in [0, PI / 4, PI / 2, PI * 3 / 4]:
		_segment(fan_root, Vector3(sin(angle) * -0.39, cos(angle) * -0.39, 0.08), Vector3(sin(angle) * 0.39, cos(angle) * 0.39, 0.08), 0.007, ink)
	_sphere(fan_root, Vector3(0, 0, 0.10), Vector3(0.075, 0.075, 0.025), coral)
	var label: Label3D = _label(self, "02  /  VENTILADOR", Vector3(5.94, 1.07, -0.6), 0.0034, Color("294851"))
	label.rotation.y = -PI / 2.0

func _build_decor() -> void:
	# Door, original graphic poster, clock and plants bring a lived-in identity.
	_box(self, Vector3(1.9, 1.1, 4.966), Vector3(1.2, 2.2, 0.025), teal)
	for x: float in [1.27, 2.53]:
		_box(self, Vector3(x, 1.14, 4.935), Vector3(0.08, 2.28, 0.09), cream)
	_box(self, Vector3(1.9, 2.27, 4.935), Vector3(1.35, 0.08, 0.09), cream)
	_sphere(self, Vector3(2.30, 1.06, 4.91), Vector3(0.05, 0.05, 0.025), gold)
	_box(self, Vector3(-5.93, 1.78, -0.35), Vector3(0.11, 1.13, 1.50), wood)
	_box(self, Vector3(-5.862, 1.78, -0.35), Vector3(0.02, 0.98, 1.35), ink)
	var poster: Label3D = _label(self, "NOCHES\nDE VERANO", Vector3(-5.843, 1.83, -0.35), 0.0052, Color("f5d69a"))
	poster.rotation.y = PI / 2.0
	var small_poster: Label3D = _label(self, "ABRÍ LA VENTANA.\nO MEJOR, NO.", Vector3(-5.838, 1.43, -0.35), 0.0022, Color("e4a488"))
	small_poster.rotation.y = PI / 2.0
	var clock_face: MeshInstance3D = _disc(self, Vector3(0.10, 2.05, -4.925), 0.31, 0.045, cream)
	clock_face.rotation.x = PI / 2.0
	_segment(self, Vector3(0.10, 2.05, -4.889), Vector3(0.10, 2.26, -4.889), 0.012, ink)
	_segment(self, Vector3(0.10, 2.05, -4.889), Vector3(0.27, 2.01, -4.889), 0.013, coral)
	_plant(Vector3(-5.45, 0, -3.95), 0.82)
	_plant(Vector3(4.4, 1.10, 3.1), 0.46)
	_plant(Vector3(5.46, 0, 4.45), 0.95)
	# Wall lights and a ceiling shade are rounded, lightweight meshes.
	_disc(self, Vector3(0, 2.775, 0.6), 0.28, 0.05, gold)
	_cone(self, Vector3(0, 2.59, 0.6), 0.14, 0.37, 0.32, cream)
	_label(self, "LET ME SLEEP", Vector3(-2.0, 2.15, 4.925), 0.006, Color("294851")).rotation.y = PI

func _plant(at: Vector3, height: float) -> void:
	var model := Furnishings.instantiate_asset("plant")
	var bounds: AABB = Furnishings.bounds_cache["plant"]
	model.scale = Vector3.ONE*height/bounds.size.y
	model.position = at-Vector3(0,bounds.position.y,0)*model.scale
	map_root.add_child(model)

func _legacy_plant(at: Vector3, height: float) -> void:
	var root := Node3D.new()
	root.position = at
	map_root.add_child(root)
	_cone(root, Vector3(0, height * 0.18, 0), height * 0.23, height * 0.17, height * 0.36, coral)
	var green: StandardMaterial3D = ActorModel.material(Color("4c7962"))
	for i: int in range(7):
		var angle: float = float(i) * 2.399
		var tip := Vector3(sin(angle) * height * 0.29, height * (0.70 + float(i % 3) * 0.09), cos(angle) * height * 0.29)
		_segment(root, Vector3(0, height * 0.3, 0), tip, 0.013, green)
		var leaf: MeshInstance3D = _sphere(root, tip, Vector3(height * 0.14, height * 0.29, height * 0.05), green)
		leaf.rotation = Vector3(0.35, angle, sin(angle) * 0.5)

func _build_lobby() -> void:
	# A separate outdoor waiting place: tiled courtyard, benches and the closed
	# front door. Its walkable dimensions/colliders come from MapCatalog.
	var facade: StandardMaterial3D = ActorModel.material(Color("568e92"))
	var grout: StandardMaterial3D = ActorModel.material(Color("334b52"))
	var tile_a: StandardMaterial3D = ActorModel.material(Color("d2c3a5"))
	var tile_b: StandardMaterial3D = ActorModel.material(Color("b1bbb0"))
	_box(self, Vector3(0, -0.10, 0), Vector3(8.3, 0.2, 6.3), grout)
	for x: int in range(8):
		for z: int in range(6):
			_box(self, Vector3(-3.5 + x, 0.005, -2.5 + z), Vector3(0.965, 0.015, 0.965), tile_a if (x + z) % 2 == 0 else tile_b)
	_box(self, Vector3(0, 2.0, -3.085), Vector3(8.2, 4.0, 0.17), facade)
	_box(self, Vector3(0, 0.11, -2.95), Vector3(8, 0.22, 0.10), ink)
	_box(self, Vector3(0, 3.65, -2.94), Vector3(8.1, 0.20, 0.14), cream)
	# Big double door, rounded brass handles and an illustrated house number.
	_box(self, Vector3(0, 1.30, -2.969), Vector3(2.05, 2.6, 0.028), ink)
	for side: float in [-1.0, 1.0]:
		_box(self, Vector3(side * 0.48, 1.29, -2.93), Vector3(0.91, 2.45, 0.075), coral)
		_box(self, Vector3(side * 0.48, 1.73, -2.875), Vector3(0.67, 1.06, 0.035), wood)
		_box(self, Vector3(side * 0.48, 0.56, -2.875), Vector3(0.67, 0.59, 0.035), wood)
		_segment(self, Vector3(side * 0.18, 1.08, -2.82), Vector3(side * 0.18, 1.35, -2.82), 0.034, gold)
		_box(self, Vector3(side * 1.10, 1.35, -2.88), Vector3(0.13, 2.70, 0.20), cream)
	_box(self, Vector3(0, 2.69, -2.87), Vector3(2.34, 0.14, 0.20), cream)
	_label(self, "LET ME SLEEP", Vector3(0, 3.12, -2.90), 0.007, Color("fff0cf"))
	_label(self, "TODOS LISTOS, ENTRAMOS", Vector3(0, 2.89, -2.89), 0.0028, Color("fff0cf"))
	# The awning silhouettes the entrance without adding a walkable obstacle.
	for index: int in range(8):
		_box(self, Vector3(-1.47 + index * 0.42, 3.47, -2.58), Vector3(0.42, 0.11, 0.78), cream if index % 2 == 0 else coral)
		_sphere(self, Vector3(-1.47 + index * 0.42, 3.38, -2.19), Vector3(0.21, 0.14, 0.055), cream if index % 2 == 0 else coral)
	var glass: StandardMaterial3D = ActorModel.material(Color("e5c885"))
	glass.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	for side: float in [-1.0, 1.0]:
		_box(self, Vector3(side * 2.70, 1.92, -2.966), Vector3(1.23, 1.18, 0.025), ink)
		_box(self, Vector3(side * 2.70, 1.92, -2.944), Vector3(1.05, 1.00, 0.025), glass)
		_box(self, Vector3(side * 2.70, 1.92, -2.910), Vector3(0.055, 1.05, 0.035), cream)
		_box(self, Vector3(side * 2.70, 1.92, -2.910), Vector3(1.07, 0.055, 0.035), cream)
		_box(self, Vector3(side * 2.70, 1.31, -2.80), Vector3(1.37, 0.10, 0.30), wood)
	# Benches exactly fit the catalog obstacles; no extra backrest blocks jumping.
	for obstacle: AABB in map_data.get("obstacles", []):
		var center: Vector3 = obstacle.get_center()
		for strip: int in range(3):
			_box(self, Vector3(obstacle.position.x + obstacle.size.x * (float(strip) + 0.5) / 3.0, obstacle.end.y - 0.045, center.z), Vector3(obstacle.size.x / 3.0 - 0.012, 0.09, obstacle.size.z), wood)
		for side: float in [-1.0, 1.0]:
			_box(self, Vector3(center.x, obstacle.position.y + obstacle.size.y * 0.43, center.z + side * obstacle.size.z * 0.35), Vector3(obstacle.size.x * 0.70, obstacle.size.y * 0.86, 0.075), ink)
	# Courtyard fences make a clear visual boundary; the catalog defines the
	# full arena edge for both server movement and the third-person camera.
	for side: float in [-1.0, 1.0]:
		_box(self, Vector3(side * 4.04, 0.63, 0), Vector3(0.16, 1.26, 6.16), facade)
		_box(self, Vector3(side * 3.97, 1.30, 0), Vector3(0.18, 0.12, 6.16), cream)
		for z: float in [-2.8, 0.0, 2.8]:
			_box(self, Vector3(side * 3.94, 1.04, z), Vector3(0.17, 2.08, 0.17), ink)
			_sphere(self, Vector3(side * 3.94, 2.10, z), Vector3.ONE * 0.12, gold)
	_box(self, Vector3(0, 0.48, 3.04), Vector3(8.1, 0.96, 0.16), facade)
	_box(self, Vector3(0, 1.56, 3.03), Vector3(8.0, 0.07, 0.09), cream)
	for x: int in range(19):
		_box(self, Vector3(-3.78 + float(x) * 0.42, 1.24, 3.02), Vector3(0.035, 0.65, 0.045), ink)
	# Small skyline and moon belong to this original porch scene only.
	var night: StandardMaterial3D = ActorModel.material(Color("263f53"))
	for index: int in range(9):
		var height: float = 2.4 + float(index % 3) * 0.70
		_box(self, Vector3(-9.0 + index * 2.2, height * 0.5 - 0.8, 8.8), Vector3(1.9, height, 1.2), night)
		for window: int in range(3):
			_box(self, Vector3(-9.55 + index * 2.2 + float(window) * 0.55, height * 0.60 - 0.8, 8.185), Vector3(0.18, 0.28, 0.012), glass)
	_sphere(self, Vector3(-5.0, 6.8, 7.0), Vector3.ONE * 0.67, glass)
	for index: int in range(15):
		_sphere(self, Vector3(-9.0 + float(index) * 1.31, 5.5 + float(index % 4) * 0.74, 9.0), Vector3.ONE * 0.026, glass)
	var previous_point := Vector3(-3.8, 3.35, -2.2)
	for index: int in range(1, 13):
		var fraction: float = float(index) / 12.0
		var point: Vector3 = Vector3(-3.8, 3.35, -2.2).lerp(Vector3(3.8, 3.45, 2.6), fraction)
		point.y -= sin(fraction * PI) * 0.46
		_segment(self, previous_point, point, 0.012, ink)
		_sphere(self, point - Vector3(0, 0.09, 0), Vector3(0.055, 0.075, 0.055), glass)
		previous_point = point

func _build_marker() -> void:
	marker = Node3D.new()
	add_child(marker)
	var mark_mat: StandardMaterial3D = ActorModel.material(Color("f4e982"))
	mark_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mark_mat.no_depth_test = false
	# A diamond plus center dot is distinguishable without hue perception.
	marker_ready_material = mark_mat
	marker_charge_material = ActorModel.material(Color("93f3de"))
	marker_charge_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	marker_quiet_material = ActorModel.material(Color("435d63"))
	marker_quiet_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	var corners: Array[Vector3] = [Vector3(0, 0.056, 0), Vector3(0.048, 0, 0), Vector3(0, -0.056, 0), Vector3(-0.048, 0, 0)]
	for i: int in range(4):
		_segment(marker, corners[i], corners[(i + 1) % 4], 0.004, mark_mat).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_sphere(marker, Vector3.ZERO, Vector3.ONE * 0.008, mark_mat).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	for i: int in range(24):
		var start_angle: float = PI * 0.5 - TAU * float(i) / 24.0
		var end_angle: float = start_angle - TAU * 0.84 / 24.0
		var start := Vector3(cos(start_angle), sin(start_angle), 0) * 0.074
		var end := Vector3(cos(end_angle), sin(end_angle), 0) * 0.074
		var sector: MeshInstance3D = _segment(marker, start, end, 0.006, marker_quiet_material)
		sector.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		sector.visible = false
		marker_ring.append(sector)
	marker_label = _label(marker, "", Vector3(0, 0.20, 0), 0.0023, Color("fff3a5"), true)
	marker_label.font_size = 28
	marker.visible = false

func _collider(at: Vector3, size: Vector3) -> void:
	var body := StaticBody3D.new()
	body.position = at
	body.collision_layer = 1
	body.collision_mask = 0
	map_root.add_child(body)
	var shape := BoxShape3D.new()
	shape.size = size
	var collision := CollisionShape3D.new()
	collision.shape = shape
	body.add_child(collision)

func _label(parent: Node3D, text_value: String, at: Vector3, pixel: float, color: Color, billboard: bool = false) -> Label3D:
	var result := Label3D.new()
	result.text = text_value
	result.position = at
	result.font_size = 40
	result.pixel_size = pixel
	result.modulate = color
	result.outline_modulate = Color("203b48")
	result.outline_size = 4 if billboard else 0
	result.billboard = BaseMaterial3D.BILLBOARD_ENABLED if billboard else BaseMaterial3D.BILLBOARD_DISABLED
	result.no_depth_test = false
	_map_parent(parent).add_child(result)
	return result

func _box(parent: Node3D, at: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	return ActorModel._box(_map_parent(parent), at, size, mat)

func _sphere(parent: Node3D, at: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	return ActorModel._sphere(_map_parent(parent), at, size, mat)

func _capsule(parent: Node3D, at: Vector3, radius: float, height: float, mat: Material) -> MeshInstance3D:
	return ActorModel._capsule(_map_parent(parent), at, radius, height, mat)

func _segment(parent: Node3D, from: Vector3, to: Vector3, radius: float, mat: Material) -> MeshInstance3D:
	return ActorModel._segment(_map_parent(parent), from, to, radius, mat)

func _disc(parent: Node3D, at: Vector3, radius: float, height: float, mat: Material) -> MeshInstance3D:
	return _cone(parent, at, radius, radius, height, mat)

func _cone(parent: Node3D, at: Vector3, top: float, bottom: float, height: float, mat: Material) -> MeshInstance3D:
	var geometry := CylinderMesh.new()
	geometry.top_radius = top
	geometry.bottom_radius = bottom
	geometry.height = height
	geometry.radial_segments = 20
	return ActorModel.mesh(_map_parent(parent), geometry, at, mat)

func _map_parent(parent: Node3D) -> Node3D:
	return map_root if parent == self and is_instance_valid(map_root) else parent
