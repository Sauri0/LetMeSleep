class_name ActorView
extends Node3D

# Original procedural characters. All transforms are cosmetic; the server owns play.
var actor_role: String = ""
var local_view: bool = false
var current_tool: String = "hands"
var model: Node3D
var head: Node3D
var left_arm: Node3D
var right_arm: Node3D
var tool_socket: Node3D
var held_tool: Node3D
var left_wing: Node3D
var right_wing: Node3D
var name_label: Label3D
var body_shapes: Array[StaticBody3D] = []
var clock_time: float = 0.0
var last_position: Vector3 = Vector3.ZERO
var initialized: bool = false

func build(role: String, display_name: String, tint_index: int = 0) -> void:
	actor_role = role
	model = Node3D.new()
	add_child(model)
	if role == "human":
		_build_human(tint_index)
	else:
		_build_mosquito()
	name_label = Label3D.new()
	name_label.text = display_name
	name_label.position.y = 1.98 if role == "human" else 0.38
	name_label.font_size = 30 if role == "human" else 24
	name_label.pixel_size = 0.005 if role == "human" else 0.0025
	name_label.modulate = Color("fff6d7")
	name_label.outline_modulate = Color("193c45")
	name_label.outline_size = 7
	name_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	name_label.no_depth_test = false
	add_child(name_label)

func set_local(value: bool) -> void:
	local_view = value
	if is_instance_valid(head):
		head.visible = not value
	if actor_role == "human" and is_instance_valid(left_arm):
		left_arm.position.y = 1.37 if value else 1.30
		right_arm.position.y = 1.37 if value else 1.30
		left_arm.position.z = -0.07 if value else 0.0
		right_arm.position.z = -0.07 if value else 0.0
	if is_instance_valid(name_label):
		name_label.visible = not value

func body_collision_rids() -> Array[RID]:
	var result: Array[RID] = []
	for body: StaticBody3D in body_shapes:
		result.append(body.get_rid())
	return result

func update_state(data: Dictionary, dt: float) -> void:
	clock_time += dt
	var target: Vector3 = data.get("p", Vector3.ZERO)
	if not initialized or global_position.distance_to(target) > 2.2:
		global_position = target
		initialized = true
	else:
		global_position = global_position.lerp(target, 1.0 - exp(-18.0 * dt))
	rotation.y = lerp_angle(rotation.y, float(data.get("yaw", 0.0)), 1.0 - exp(-20.0 * dt))
	visible = bool(data.get("alive", true))
	for body: StaticBody3D in body_shapes:
		body.collision_layer = 2 if visible else 0
	var speed: float = global_position.distance_to(last_position) / maxf(dt, 0.001)
	last_position = global_position
	if actor_role == "human":
		var tool: String = str(data.get("tool", "hands"))
		if tool != current_tool:
			_equip_tool(tool)
		var swing: float = float(data.get("swing", 0.0))
		var swing_amount: float = minf(swing * 6.0, 1.0)
		var walk: float = sin(clock_time * 8.0) * minf(speed, 2.0) * 0.045
		var arm_target: float = lerpf(1.45 if local_view else 0.0, 1.9, swing_amount)
		left_arm.rotation.x = lerpf(left_arm.rotation.x, arm_target + walk, minf(dt * 24.0, 1.0))
		right_arm.rotation.x = lerpf(right_arm.rotation.x, arm_target - walk, minf(dt * 24.0, 1.0))
		left_arm.rotation.z = lerpf(left_arm.rotation.z, -swing_amount * 0.52, minf(dt * 24.0, 1.0))
		right_arm.rotation.z = lerpf(right_arm.rotation.z, swing_amount * 0.52, minf(dt * 24.0, 1.0))
		model.position.y = absf(sin(clock_time * 4.0)) * minf(speed, 2.0) * 0.009
		if is_instance_valid(head):
			head.rotation.x = float(data.get("pitch", 0.0)) * 0.5
	else:
		var state: String = str(data.get("state", "flying"))
		var flying: bool = state == "flying"
		var flutter: float = sin(clock_time * 80.0) * (0.6 if flying else 0.08)
		left_wing.rotation.z = -0.22 + flutter
		right_wing.rotation.z = 0.22 - flutter
		model.rotation.x = -float(data.get("pitch", 0.0)) * 0.35 if flying else 0.0
		model.position.y = sin(clock_time * 9.0) * 0.012 if flying else 0.0

func _build_human(tint_index: int) -> void:
	var skin: StandardMaterial3D = material(Color("e8b186"))
	var dark: StandardMaterial3D = material(Color("27333f"))
	var white: StandardMaterial3D = material(Color("fff6dd"))
	var palette: Array[Color] = [Color("e78b73"), Color("72b9ac"), Color("e6bb63"), Color("9096d0")]
	var shirt: StandardMaterial3D = material(palette[tint_index % palette.size()])
	var trousers: StandardMaterial3D = material(palette[tint_index % palette.size()].darkened(0.2))
	_capsule(model, Vector3(0, 1.09, 0), 0.265, 0.73, shirt, Vector3(1, 1, 0.77))
	_sphere(model, Vector3(0, 0.73, 0.025), Vector3(0.26, 0.17, 0.20), trousers)
	# Pajama buttons, rounded collar, two trouser legs and soft slippers.
	for height: float in [1.29, 1.13, 0.97]:
		_sphere(model, Vector3(0, height, -0.209), Vector3(0.023, 0.023, 0.015), white)
	for side: float in [-1.0, 1.0]:
		_capsule(model, Vector3(side * 0.135, 0.44, 0.01), 0.115, 0.64, trousers)
		_sphere(model, Vector3(side * 0.135, 0.09, -0.09), Vector3(0.145, 0.095, 0.245), dark)
		_sphere(model, Vector3(side * 0.11, 1.37, -0.13), Vector3(0.11, 0.035, 0.085), white)
	left_arm = _human_arm(-1.0, shirt, skin)
	right_arm = _human_arm(1.0, shirt, skin)
	tool_socket = Node3D.new()
	tool_socket.position = Vector3(0, -0.44, -0.025)
	right_arm.add_child(tool_socket)
	head = Node3D.new()
	head.position.y = 1.55
	model.add_child(head)
	_sphere(head, Vector3.ZERO, Vector3(0.235, 0.265, 0.22), skin)
	_sphere(head, Vector3(0, 0.16, 0.035), Vector3(0.242, 0.125, 0.23), dark)
	_sphere(head, Vector3(0.04, 0.25, 0.035), Vector3(0.11, 0.06, 0.08), dark)
	for side: float in [-1.0, 1.0]:
		_sphere(head, Vector3(side * 0.236, -0.01, 0), Vector3(0.045, 0.07, 0.045), skin)
		_sphere(head, Vector3(side * 0.085, 0.035, -0.195), Vector3(0.059, 0.068, 0.036), white)
		_sphere(head, Vector3(side * 0.085, 0.023, -0.225), Vector3(0.028, 0.037, 0.013), dark)
		var brow: MeshInstance3D = _capsule(head, Vector3(side * 0.085, 0.116, -0.199), 0.015, 0.10, dark)
		brow.rotation.z = side * 1.25
	_sphere(head, Vector3(0, -0.025, -0.225), Vector3(0.045, 0.055, 0.056), skin)
	var mouth: MeshInstance3D = _capsule(head, Vector3(0, -0.13, -0.195), 0.012, 0.075, dark)
	mouth.rotation.z = PI / 2.0
	_add_body_capsule(Vector3(0, 1.07, 0), 0.235, 0.73)
	_add_body_sphere(Vector3(0, 1.55, 0), 0.22)
	_add_body_capsule(Vector3(0, 0.50, 0), 0.23, 0.70)

func _human_arm(side: float, shirt: StandardMaterial3D, skin: StandardMaterial3D) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = Vector3(side * 0.31, 1.30, 0)
	model.add_child(pivot)
	_capsule(pivot, Vector3(0, -0.14, 0), 0.105, 0.32, shirt)
	_capsule(pivot, Vector3(0, -0.31, -0.006), 0.079, 0.27, skin)
	_sphere(pivot, Vector3(0, -0.44, -0.025), Vector3(0.088, 0.105, 0.058), skin)
	_sphere(pivot, Vector3(-side * 0.069, -0.415, -0.035), Vector3(0.037, 0.065, 0.032), skin)
	return pivot

func _build_mosquito() -> void:
	var body: StandardMaterial3D = material(Color("294651"))
	var teal: StandardMaterial3D = material(Color("5ca99b"))
	var cream: StandardMaterial3D = material(Color("ffeab4"))
	var eye: StandardMaterial3D = material(Color("f3ac4f"))
	var pupil: StandardMaterial3D = material(Color("192c38"))
	_sphere(model, Vector3(0, 0, 0.045), Vector3(0.069, 0.068, 0.104), body)
	_sphere(model, Vector3(0, -0.012, 0.17), Vector3(0.052, 0.048, 0.115), teal)
	for z: float in [0.12, 0.17, 0.22]:
		var ring := TorusMesh.new()
		ring.inner_radius = 0.044
		ring.outer_radius = 0.053
		ring.rings = 12
		ring.ring_segments = 6
		var band: MeshInstance3D = mesh(model, ring, Vector3(0, -0.012, z), body)
		band.rotation.x = PI / 2.0
	_sphere(model, Vector3(0, 0.005, -0.065), Vector3(0.068, 0.070, 0.065), teal)
	for side: float in [-1.0, 1.0]:
		_sphere(model, Vector3(side * 0.047, 0.018, -0.095), Vector3(0.044, 0.05, 0.035), eye)
		_sphere(model, Vector3(side * 0.047, 0.018, -0.124), Vector3(0.020, 0.026, 0.008), pupil)
		_sphere(model, Vector3(side * 0.040, 0.033, -0.132), Vector3(0.008, 0.010, 0.004), cream)
		_segment(model, Vector3(side * 0.028, 0.053, -0.08), Vector3(side * 0.092, 0.15, -0.092), 0.007, body)
		_sphere(model, Vector3(side * 0.092, 0.15, -0.092), Vector3.ONE * 0.012, cream)
		for z: float in [-0.02, 0.05, 0.12]:
			_segment(model, Vector3(side * 0.045, -0.025, z), Vector3(side * 0.13, -0.09, z + 0.02), 0.006, body)
			_segment(model, Vector3(side * 0.13, -0.09, z + 0.02), Vector3(side * 0.15, -0.15, z - 0.015), 0.005, body)
	_segment(model, Vector3(0, -0.015, -0.11), Vector3(0, -0.034, -0.24), 0.009, body)
	var wing_mat: StandardMaterial3D = material(Color(0.84, 0.96, 0.95, 0.67))
	wing_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	wing_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	wing_mat.roughness = 0.35
	left_wing = _wing(-1.0, wing_mat)
	right_wing = _wing(1.0, wing_mat)
	_add_body_sphere(Vector3.ZERO, 0.10)

func _wing(side: float, mat: StandardMaterial3D) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = Vector3(side * 0.035, 0.055, 0.06)
	model.add_child(pivot)
	var wing: MeshInstance3D = _sphere(pivot, Vector3(side * 0.10, 0, 0.035), Vector3(0.15, 0.006, 0.075), mat)
	wing.rotation.y = side * 0.38
	wing.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return pivot

func _equip_tool(tool: String) -> void:
	current_tool = tool
	if is_instance_valid(held_tool):
		held_tool.queue_free()
	held_tool = make_tool(tool)
	tool_socket.add_child(held_tool)

static func make_tool(tool: String) -> Node3D:
	var root := Node3D.new()
	var coral: StandardMaterial3D = material(Color("e47e65"))
	var dark: StandardMaterial3D = material(Color("274b54"))
	var metal: StandardMaterial3D = material(Color("bcd5cb"))
	var paper: StandardMaterial3D = material(Color("f6e7c9"))
	if tool == "hands":
		return root
	if tool == "newspaper":
		var roll: MeshInstance3D = _capsule(root, Vector3(0, -0.15, 0), 0.045, 0.40, paper)
		roll.rotation.z = 0.04
		for y: float in [-0.26, -0.22, -0.18]:
			_box(root, Vector3(0, y, -0.045), Vector3(0.055, 0.012, 0.003), dark)
		return root
	var length: float = 0.78 if tool == "broom" else 0.40
	_segment(root, Vector3(0, 0.06, 0), Vector3(0, -length, 0), 0.022 if tool == "broom" else 0.017, dark)
	_capsule(root, Vector3(0, -0.025, 0), 0.027, 0.16, coral)
	if tool == "broom":
		_box(root, Vector3(0, -0.79, 0), Vector3(0.34, 0.065, 0.08), coral)
		for i: int in range(10):
			_capsule(root, Vector3(-0.153 + i * 0.034, -0.88, 0), 0.021, 0.19, paper)
	elif tool == "racket":
		var ring := TorusMesh.new()
		ring.inner_radius = 0.115
		ring.outer_radius = 0.142
		ring.rings = 20
		ring.ring_segments = 6
		var frame: MeshInstance3D = mesh(root, ring, Vector3(0, -0.51, 0), coral)
		frame.rotation.x = PI / 2.0
		frame.scale.y = 1.2
		for i: int in range(-3, 4):
			var offset: float = i * 0.03
			var span: float = sqrt(maxf(0.0, 0.12 * 0.12 - offset * offset))
			_segment(root, Vector3(offset, -0.51 - span, 0), Vector3(offset, -0.51 + span, 0), 0.003, metal)
			_segment(root, Vector3(-span, -0.51 + offset, 0), Vector3(span, -0.51 + offset, 0), 0.003, metal)
	else:
		var paddle: MeshInstance3D = _sphere(root, Vector3(0, -0.46, 0), Vector3(0.115, 0.15, 0.012), coral)
		paddle.name = "SwatterPaddle"
		for x: float in [-0.06, -0.02, 0.02, 0.06]:
			for y: float in [-0.52, -0.47, -0.42]:
				_box(root, Vector3(x, y, -0.013), Vector3(0.014, 0.025, 0.002), dark)
	return root

func _add_body_capsule(at: Vector3, radius: float, height: float) -> void:
	var shape := CapsuleShape3D.new()
	shape.radius = radius
	shape.height = height
	_add_collider(at, shape)

func _add_body_sphere(at: Vector3, radius: float) -> void:
	var shape := SphereShape3D.new()
	shape.radius = radius
	_add_collider(at, shape)

func _add_collider(at: Vector3, shape: Shape3D) -> void:
	var body := StaticBody3D.new()
	body.position = at
	body.collision_layer = 2
	body.collision_mask = 0
	add_child(body)
	var collision := CollisionShape3D.new()
	collision.shape = shape
	body.add_child(collision)
	body_shapes.append(body)

static func material(color: Color) -> StandardMaterial3D:
	var result := StandardMaterial3D.new()
	result.albedo_color = color
	result.roughness = 0.82
	return result

static func mesh(parent: Node3D, geometry: Mesh, at: Vector3, mat: Material) -> MeshInstance3D:
	var result := MeshInstance3D.new()
	result.mesh = geometry
	result.material_override = mat
	result.position = at
	parent.add_child(result)
	return result

static func _sphere(parent: Node3D, at: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	var geometry := SphereMesh.new()
	geometry.radius = 1.0
	geometry.height = 2.0
	geometry.radial_segments = 16
	geometry.rings = 8
	var result: MeshInstance3D = mesh(parent, geometry, at, mat)
	result.scale = size
	return result

static func _capsule(parent: Node3D, at: Vector3, radius: float, height: float, mat: Material, scaling: Vector3 = Vector3.ONE) -> MeshInstance3D:
	var geometry := CapsuleMesh.new()
	geometry.radius = radius
	geometry.height = height
	geometry.radial_segments = 12
	geometry.rings = 4
	var result: MeshInstance3D = mesh(parent, geometry, at, mat)
	result.scale = scaling
	return result

static func _box(parent: Node3D, at: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	var geometry := BoxMesh.new()
	geometry.size = size
	return mesh(parent, geometry, at, mat)

static func _segment(parent: Node3D, from: Vector3, to: Vector3, radius: float, mat: Material) -> MeshInstance3D:
	var geometry := CylinderMesh.new()
	geometry.top_radius = radius
	geometry.bottom_radius = radius
	geometry.height = from.distance_to(to)
	geometry.radial_segments = 8
	var result: MeshInstance3D = mesh(parent, geometry, (from + to) * 0.5, mat)
	var direction: Vector3 = (to - from).normalized()
	result.quaternion = Quaternion(Vector3.UP, direction)
	return result
