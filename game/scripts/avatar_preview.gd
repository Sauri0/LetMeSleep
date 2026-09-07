extends SubViewportContainer
## Isolated original avatar studio. It never renders the lobby or game world.
const Actor = preload("res://scripts/actor_view.gd")
var viewport: SubViewport
var studio: Node3D
var camera: Camera3D
var avatar: ActorView
var role := "human"
var appearance: Dictionary = {}
var orbit_yaw := -0.25
var orbit_pitch := -0.08
var zoom := 1.0
var dragging := false
var focus_key := ""
var focus_target := Vector3(0,0.91,0)
var focus_distance := 3.65

func _ready() -> void:
	custom_minimum_size = Vector2(280,300)
	stretch = true
	mouse_filter = Control.MOUSE_FILTER_STOP
	focus_mode = Control.FOCUS_ALL
	viewport = SubViewport.new()
	viewport.own_world_3d = true
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_WHEN_VISIBLE
	viewport.msaa_3d = Viewport.MSAA_2X
	add_child(viewport)
	studio = Node3D.new()
	viewport.add_child(studio)
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("284554")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("c7d4e0")
	environment.ambient_light_energy = 0.65
	environment_node.environment = environment
	studio.add_child(environment_node)
	for side: float in [-1.0,1.0]:
		var light := OmniLight3D.new()
		light.position = Vector3(side*1.8,2.9,2.7)
		light.light_color = Color("ffe0b4") if side<0 else Color("abd4e9")
		light.light_energy = 3.0 if side<0 else 1.1
		light.omni_range = 7.0
		studio.add_child(light)
	var rim := OmniLight3D.new()
	rim.position = Vector3(0,2.3,-2.0)
	rim.light_color = Color("abd4e9")
	rim.light_energy = 1.2
	rim.omni_range = 6.0
	studio.add_child(rim)
	var base := CylinderMesh.new()
	base.top_radius = 0.68
	base.bottom_radius = 0.72
	base.height = 0.10
	base.radial_segments = 48
	Actor.mesh(studio,base,Vector3(0,-0.06,0),Actor.material(Color("bfaa80")))
	camera = Camera3D.new()
	camera.near = 0.02
	camera.fov = 36.0
	studio.add_child(camera)
	camera.make_current()
	set_avatar(role,appearance)

func set_avatar(selected_role: String, data: Dictionary) -> void:
	role = selected_role if selected_role in ["human","mosquito"] else "human"
	appearance = data.duplicate(true)
	if not is_instance_valid(studio):
		return
	if not is_instance_valid(avatar) or avatar.actor_role!=role:
		if is_instance_valid(avatar):
			studio.remove_child(avatar)
			avatar.queue_free()
		avatar = Actor.new()
		studio.add_child(avatar)
		avatar.build(role,"",0)
		avatar.preview_only = true
		avatar.name_label.visible = false
		avatar.scale = Vector3.ONE*(3.1/Actor.MOSQUITO_VISUAL_SCALE if role=="mosquito" else 1.0)
		reset_view()
	avatar.update_state({"p":Vector3(0,0.88 if role=="mosquito" else 0,0),"yaw":PI,"body_yaw":PI,"state":"flying" if role=="mosquito" else "human","relaxed_pose":true,"appearance":appearance},1.0)
	_update_camera()

func reset_view() -> void:
	orbit_yaw = -0.25
	orbit_pitch = -0.08
	zoom = 1.0
	focus_key = ""
	focus_target = Vector3(0,0.91,0)
	focus_distance = 3.65
	_update_camera()

func set_view(view: String) -> void:
	orbit_yaw = 0.0 if view=="front" else PI*0.5 if view=="side" else PI if view=="back" else -0.25
	orbit_pitch = -0.08
	_update_camera()

func focus_category(key: String) -> void:
	focus_key = key
	zoom = 1.0
	if role=="human":
		if key in ["face","hair","accessory"]:
			focus_target = Vector3(0,1.59,0)
			focus_distance = 1.35 if key=="face" else 1.65
		elif key=="footwear":
			focus_target = Vector3(0,0.17,0)
			focus_distance = 1.30
		elif key=="outfit":
			focus_target = Vector3(0,0.95,0)
			focus_distance = 2.65
		else:
			focus_target = Vector3(0,0.91,0)
			focus_distance = 3.65
	else:
		focus_target = Vector3(0,0.98 if key in ["face","hair","accessory"] else 0.74 if key=="footwear" else 0.90,0)
		focus_distance = 2.4 if key in ["face","hair","accessory"] else 3.25
	_update_camera()

func _update_camera() -> void:
	if not is_instance_valid(camera):
		return
	var target: Vector3 = focus_target
	var offset: Vector3 = Vector3(0,0,focus_distance*zoom).rotated(Vector3.RIGHT,orbit_pitch).rotated(Vector3.UP,orbit_yaw)
	camera.position = target+offset
	camera.look_at(target)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index==MOUSE_BUTTON_LEFT:
			dragging = event.pressed
			grab_focus()
			accept_event()
		elif event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP,MOUSE_BUTTON_WHEEL_DOWN]:
			zoom = clampf(zoom+(0.09 if event.button_index==MOUSE_BUTTON_WHEEL_DOWN else -0.09),0.72,1.35)
			_update_camera()
			accept_event()
	elif event is InputEventMouseMotion and dragging:
		orbit_yaw -= event.relative.x*0.012
		orbit_pitch = clampf(orbit_pitch-event.relative.y*0.009,-0.45,0.55)
		_update_camera()
		accept_event()

func _process(dt: float) -> void:
	if dragging and not Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		dragging = false
	if is_instance_valid(avatar):
		avatar.update_state({"p":Vector3(0,0.88 if role=="mosquito" else 0.0,0),"yaw":PI,"body_yaw":PI,"state":"flying" if role=="mosquito" else "human","relaxed_pose":true,"appearance":appearance},dt)
