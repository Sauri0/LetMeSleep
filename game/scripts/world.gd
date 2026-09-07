extends Node3D

const ActorModel = preload("res://scripts/actor_view.gd")
const Map = preload("res://scripts/arena.gd")
const AudioEffects = preload("res://scripts/audio_fx.gd")

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

func build() -> void:
	if built:
		return
	built = true
	cream = ActorModel.material(Color("f1e7cb"))
	wood = ActorModel.material(Color("bc835b"))
	teal = ActorModel.material(Color("518f88"))
	coral = ActorModel.material(Color("d98069"))
	ink = ActorModel.material(Color("233e4c"))
	gold = ActorModel.material(Color("e8be68"))
	_build_lighting()
	_build_shell()
	_build_sofa()
	_build_tables()
	_build_window()
	_build_fan()
	_build_decor()
	_build_marker()
	audio_fx = AudioEffects.new()
	add_child(audio_fx)
	audio_fx.setup()
	menu_camera = Camera3D.new()
	menu_camera.position = Vector3(4.6, 2.35, 4.0)
	menu_camera.fov = 74.0
	menu_camera.near = 0.05
	add_child(menu_camera)
	menu_camera.look_at(Vector3(-1.0, 0.85, -1.25))
	menu_camera.current = true
	saved_menu_transform = menu_camera.transform

func _process(dt: float) -> void:
	clock_time += dt
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
		view.update_state(actor_data, dt)
		if is_instance_valid(customization_actor):
			view.visible = false
	if is_instance_valid(audio_fx):
		audio_fx.sync(data, actors, local_id)

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
		customization_actor.scale = Vector3.ONE * (3.5 if role == "mosquito" else 1.0)
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
			var label: Label3D = _label(stand, str(names.get(tool, tool)), Vector3(0, 0.83, 0), 0.0034, Color("ffefba"), true)
			label.font_size = 30
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

func show_assignment(data: Dictionary, camera: Camera3D, mosquito_position: Vector3) -> void:
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
		var authoritative_yaw: float = float(target_state.get("yaw", 0.0))
		var local_point: Vector3 = (point - authoritative_position).rotated(Vector3.UP, -authoritative_yaw)
		point = target_view.global_position + local_point.rotated(Vector3.UP, target_view.rotation.y)
		normal = normal.rotated(Vector3.UP, target_view.rotation.y - authoritative_yaw)
	if normal.length_squared() < 0.5:
		return
	normal = normal.normalized()
	var surface: Vector3 = point + normal * 0.075
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
	marker.visible = true

func _occluded(from: Vector3, to: Vector3, exclude: Array[RID]) -> bool:
	if from.distance_squared_to(to) < 0.0001:
		return false
	var query := PhysicsRayQueryParameters3D.create(from, to, 3, exclude)
	return not get_world_3d().direct_space_state.intersect_ray(query).is_empty()

func play_event(_verb: String) -> void:
	pass

func _build_lighting() -> void:
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("213f55")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("b6cacf")
	environment.ambient_light_energy = 0.73
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment_node.environment = environment
	add_child(environment_node)
	var moon := DirectionalLight3D.new()
	moon.rotation_degrees = Vector3(-44, -24, 0)
	moon.light_color = Color("c9dfed")
	moon.light_energy = 0.8
	moon.shadow_enabled = true
	moon.directional_shadow_max_distance = 20.0
	add_child(moon)
	var lamp := OmniLight3D.new()
	lamp.position = Vector3(-3.1, 2.25, 1.0)
	lamp.light_color = Color("ffd895")
	lamp.light_energy = 1.7
	lamp.omni_range = 7.5
	lamp.shadow_enabled = false
	add_child(lamp)
	var counter_light := OmniLight3D.new()
	counter_light.position = Vector3(3.2, 2.3, -2.2)
	counter_light.light_color = Color("ffe6af")
	counter_light.light_energy = 1.0
	counter_light.omni_range = 5.5
	counter_light.shadow_enabled = false
	add_child(counter_light)

func _build_shell() -> void:
	_box(self, Vector3(0, -0.10, 0), Vector3(12.3, 0.2, 10.3), wood)
	_collider(Vector3(0, -0.10, 0), Vector3(12.3, 0.2, 10.3))
	var wall_mat: StandardMaterial3D = ActorModel.material(Color("cfdbc9"))
	_box(self, Vector3(0, 1.4, -5.08), Vector3(12.2, 2.8, 0.16), wall_mat)
	_box(self, Vector3(0, 1.4, 5.08), Vector3(12.2, 2.8, 0.16), wall_mat)
	_box(self, Vector3(-6.08, 1.4, 0), Vector3(0.16, 2.8, 10.2), wall_mat)
	_box(self, Vector3(6.08, 1.4, 0), Vector3(0.16, 2.8, 10.2), wall_mat)
	_collider(Vector3(0, 1.4, -5.08), Vector3(12.2, 2.8, 0.16))
	_collider(Vector3(0, 1.4, 5.08), Vector3(12.2, 2.8, 0.16))
	_collider(Vector3(-6.08, 1.4, 0), Vector3(0.16, 2.8, 10.2))
	_collider(Vector3(6.08, 1.4, 0), Vector3(0.16, 2.8, 10.2))
	_box(self, Vector3(0, 2.90, 0), Vector3(12.2, 0.2, 10.2), cream)
	_collider(Vector3(0, 2.90, 0), Vector3(12.2, 0.2, 10.2))
	for z: float in [-4.965, 4.965]:
		_box(self, Vector3(0, 0.08, z), Vector3(12, 0.16, 0.055), cream)
		_box(self, Vector3(0, 2.69, z), Vector3(12, 0.12, 0.08), cream)
	for x: float in [-5.965, 5.965]:
		_box(self, Vector3(x, 0.08, 0), Vector3(0.055, 0.16, 10), cream)
		_box(self, Vector3(x, 2.69, 0), Vector3(0.08, 0.12, 10), cream)
	var seam_mat: StandardMaterial3D = ActorModel.material(Color("a76f4f"))
	for x: int in range(-11, 12):
		_box(self, Vector3(float(x) * 0.5, 0.002, 0), Vector3(0.012, 0.005, 10), seam_mat)
	for obstacle: AABB in Map.OBSTACLES:
		_collider(obstacle.get_center(), obstacle.size)
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
	add_child(fan_root)
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
	_label(self, "DEJAME DORMIR", Vector3(-2.0, 2.15, 4.925), 0.006, Color("294851")).rotation.y = PI
	_label(self, "UNA NOCHE. MUCHOS ZUMBIDOS.", Vector3(-2.0, 1.83, 4.925), 0.0025, Color("294851")).rotation.y = PI

func _plant(at: Vector3, height: float) -> void:
	var root := Node3D.new()
	root.position = at
	add_child(root)
	_cone(root, Vector3(0, height * 0.18, 0), height * 0.23, height * 0.17, height * 0.36, coral)
	var green: StandardMaterial3D = ActorModel.material(Color("4c7962"))
	for i: int in range(7):
		var angle: float = float(i) * 2.399
		var tip := Vector3(sin(angle) * height * 0.29, height * (0.70 + float(i % 3) * 0.09), cos(angle) * height * 0.29)
		_segment(root, Vector3(0, height * 0.3, 0), tip, 0.013, green)
		var leaf: MeshInstance3D = _sphere(root, tip, Vector3(height * 0.14, height * 0.29, height * 0.05), green)
		leaf.rotation = Vector3(0.35, angle, sin(angle) * 0.5)

func _build_marker() -> void:
	marker = Node3D.new()
	add_child(marker)
	var mark_mat: StandardMaterial3D = ActorModel.material(Color("f4e982"))
	mark_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mark_mat.no_depth_test = false
	# A diamond plus center dot is distinguishable without hue perception.
	var corners: Array[Vector3] = [Vector3(0, 0.12, 0), Vector3(0.10, 0, 0), Vector3(0, -0.12, 0), Vector3(-0.10, 0, 0)]
	for i: int in range(4):
		_segment(marker, corners[i], corners[(i + 1) % 4], 0.009, mark_mat).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_sphere(marker, Vector3.ZERO, Vector3.ONE * 0.019, mark_mat).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	marker_label = _label(marker, "", Vector3(0, 0.20, 0), 0.0023, Color("fff3a5"), true)
	marker_label.font_size = 28
	marker.visible = false

func _collider(at: Vector3, size: Vector3) -> void:
	var body := StaticBody3D.new()
	body.position = at
	body.collision_layer = 1
	body.collision_mask = 0
	add_child(body)
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
	parent.add_child(result)
	return result

func _box(parent: Node3D, at: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	return ActorModel._box(parent, at, size, mat)

func _sphere(parent: Node3D, at: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	return ActorModel._sphere(parent, at, size, mat)

func _capsule(parent: Node3D, at: Vector3, radius: float, height: float, mat: Material) -> MeshInstance3D:
	return ActorModel._capsule(parent, at, radius, height, mat)

func _segment(parent: Node3D, from: Vector3, to: Vector3, radius: float, mat: Material) -> MeshInstance3D:
	return ActorModel._segment(parent, from, to, radius, mat)

func _disc(parent: Node3D, at: Vector3, radius: float, height: float, mat: Material) -> MeshInstance3D:
	return _cone(parent, at, radius, radius, height, mat)

func _cone(parent: Node3D, at: Vector3, top: float, bottom: float, height: float, mat: Material) -> MeshInstance3D:
	var geometry := CylinderMesh.new()
	geometry.top_radius = top
	geometry.bottom_radius = bottom
	geometry.height = height
	geometry.radial_segments = 20
	return ActorModel.mesh(parent, geometry, at, mat)
