class_name ActorView
extends Node3D

const CosmeticsData = preload("res://scripts/cosmetics.gd")
const Pose = preload("res://scripts/human_pose.gd")
const MOSQUITO_VISUAL_SCALE: float = 0.35
const MOSQUITO_BODY_RADIUS: float = 0.04
static var cloth_texture: ImageTexture

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
var primary_tint: StandardMaterial3D
var secondary_tint: StandardMaterial3D
var accessory_root: Node3D
var appearance_signature: String = ""
var applied_appearance: Dictionary = {}
var fallback_color: int = 0
var preview_only: bool = false
var body_pose: Dictionary = {}
var torso_node: Node3D
var pelvis_mesh: MeshInstance3D
var limb_meshes: Dictionary = {}
var limb_hands: Dictionary = {}
var limb_feet: Dictionary = {}
var joint_meshes: Dictionary = {}
var pose_colliders: Dictionary = {}
var fps_root: Node3D
var fps_left_arm: Node3D
var fps_right_arm: Node3D
var fps_tool_socket: Node3D
var fps_held_tool: Node3D
var previous_swing: float = 0.0
var swing_duration: float = 0.8
var collar_meshes: Array[MeshInstance3D] = []

func build(role: String, display_name: String, tint_index: int = 0) -> void:
	actor_role = role
	fallback_color = posmod(tint_index, CosmeticsData.PALETTE.size())
	model = Node3D.new()
	add_child(model)
	if role == "human":
		_build_human(tint_index)
	else:
		_build_mosquito()
	name_label = Label3D.new()
	name_label.text = display_name
	name_label.position.y = 1.98 if role == "human" else 0.12
	name_label.font_size = 30 if role == "human" else 24
	name_label.pixel_size = 0.005 if role == "human" else 0.0018
	name_label.modulate = Color("fff6d7")
	name_label.outline_modulate = Color("193c45")
	name_label.outline_size = 7
	name_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	name_label.no_depth_test = false
	add_child(name_label)
	apply_appearance({"color": fallback_color, "accessory": 0})

func set_local(value: bool) -> void:
	local_view = value
	if is_instance_valid(head):
		head.visible = not value
	if actor_role == "human" and is_instance_valid(left_arm):
		left_arm.visible = not value
		right_arm.visible = not value
		fps_root.visible = value
		for collar: MeshInstance3D in collar_meshes:
			collar.visible = not value
	if is_instance_valid(name_label):
		name_label.visible = not value

func update_name_visibility(camera: Camera3D) -> void:
	if not is_instance_valid(name_label) or preview_only:
		return
	var minimum_distance: float = 1.0 if actor_role == "mosquito" else 0.75
	name_label.visible = visible and not local_view and (not is_instance_valid(camera) or camera.global_position.distance_to(global_position) >= minimum_distance)

func body_collision_rids() -> Array[RID]:
	var result: Array[RID] = []
	for body: StaticBody3D in body_shapes:
		result.append(body.get_rid())
	return result

func update_state(data: Dictionary, dt: float) -> void:
	clock_time += dt
	apply_appearance(data.get("appearance", {"color": fallback_color, "accessory": 0}))
	var target: Vector3 = data.get("p", Vector3.ZERO)
	if not initialized or global_position.distance_to(target) > 2.2:
		global_position = target
		initialized = true
	else:
		global_position = global_position.lerp(target, 1.0 - exp(-18.0 * dt))
	rotation.y = lerp_angle(rotation.y, float(data.get("yaw", 0.0)), 1.0 - exp(-20.0 * dt))
	visible = bool(data.get("alive", true))
	for body: StaticBody3D in body_shapes:
		body.collision_layer = 2 if visible and not preview_only else 0
	last_position = global_position
	if actor_role == "human":
		var tool: String = str(data.get("tool", "hands"))
		if tool != current_tool:
			_equip_tool(tool)
		_apply_human_pose(data, dt)
	else:
		var state: String = str(data.get("state", "flying"))
		var flying: bool = state == "flying"
		var flutter: float = sin(clock_time * 80.0) * (0.6 if flying else 0.08)
		left_wing.rotation.z = -0.22 + flutter
		right_wing.rotation.z = 0.22 - flutter
		model.rotation.x = float(data.get("pitch", 0.0)) * 0.6 if flying else 0.0
		# Wing animation carries flight movement; body origin is the authoritative
		# contact centre, without a cosmetic bob that suggests drifting controls.
		model.position = Vector3.ZERO

func _build_human(tint_index: int) -> void:
	var skin: StandardMaterial3D = material(Color("e8b186"))
	var dark: StandardMaterial3D = material(Color("27333f"))
	var white: StandardMaterial3D = material(Color("fff6dd"))
	var tint: Color = CosmeticsData.PALETTE[posmod(tint_index, CosmeticsData.PALETTE.size())]
	var shirt: StandardMaterial3D = material(tint)
	var trousers: StandardMaterial3D = material(tint.darkened(0.2))
	shirt.albedo_texture = _woven_texture()
	trousers.albedo_texture = cloth_texture
	shirt.uv1_scale = Vector3(6, 6, 1)
	trousers.uv1_scale = Vector3(6, 6, 1)
	primary_tint = shirt
	secondary_tint = trousers
	torso_node = Node3D.new()
	model.add_child(torso_node)
	_capsule(torso_node, Vector3.ZERO, 0.24, 0.68, shirt)
	pelvis_mesh = _sphere(model, Vector3(0, 0.73, 0.025), Vector3.ONE * 0.20, trousers)
	# Pajama buttons, rounded collar, two trouser legs and soft slippers.
	for height: float in [1.29, 1.13, 0.97]:
		collar_meshes.append(_sphere(torso_node, Vector3(0, height - 1.09, -0.209), Vector3(0.023, 0.023, 0.015), white))
	for side: float in [-1.0, 1.0]:
		var suffix: String = "l" if side < 0 else "r"
		limb_meshes["thigh_" + suffix] = _capsule(model, Vector3.ZERO, 0.105, 1.0, trousers)
		limb_meshes["shin_" + suffix] = _capsule(model, Vector3.ZERO, 0.105, 1.0, trousers)
		joint_meshes["knee_" + suffix] = _sphere(model, Vector3.ZERO, Vector3.ONE * 0.105, trousers)
		limb_feet[suffix] = _capsule(model, Vector3.ZERO, 0.105, 0.43, dark)
		collar_meshes.append(_sphere(torso_node, Vector3(side * 0.11, 0.28, -0.13), Vector3(0.11, 0.035, 0.085), white))
	left_arm = _build_rig_arm("l", -1.0, shirt, skin)
	right_arm = _build_rig_arm("r", 1.0, shirt, skin)
	tool_socket = Node3D.new()
	(limb_hands["r"] as Node3D).add_child(tool_socket)
	fps_root = Node3D.new()
	fps_root.name = "FirstPersonArms"
	model.add_child(fps_root)
	fps_left_arm = _human_arm(-1.0, shirt, skin, fps_root)
	fps_right_arm = _human_arm(1.0, shirt, skin, fps_root)
	fps_left_arm.position = Vector3(-0.31, -0.30, -0.24)
	fps_right_arm.position = Vector3(0.31, -0.30, -0.24)
	fps_tool_socket = Node3D.new()
	fps_tool_socket.position = Vector3(0, -0.44, -0.025)
	fps_right_arm.add_child(fps_tool_socket)
	fps_root.visible = false
	head = Node3D.new()
	head.position.y = 1.55
	model.add_child(head)
	_sphere(head, Vector3.ZERO, Vector3.ONE * 0.235, skin)
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
	for piece: Dictionary in Pose.collision_segments({}):
		var key: String = str(piece.get("key", "piece%d" % body_shapes.size())).replace("upperarm_", "upper_arm_")
		var length: float = Vector3(piece.from).distance_to(piece.to)
		if length < 0.001:
			_add_body_sphere(piece.from, float(piece.radius))
		else:
			_add_body_capsule((Vector3(piece.from) + Vector3(piece.to)) * 0.5, float(piece.radius), length + float(piece.radius) * 2.0)
		pose_colliders[key] = body_shapes.back()
	_apply_human_pose({}, 1.0)

func _human_arm(side: float, shirt: StandardMaterial3D, skin: StandardMaterial3D, parent: Node3D = null) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = Vector3(side * 0.31, 1.30, 0)
	(model if parent == null else parent).add_child(pivot)
	_capsule(pivot, Vector3(0, -0.14, 0), 0.105, 0.32, shirt)
	_capsule(pivot, Vector3(0, -0.31, -0.006), 0.079, 0.27, skin)
	_sphere(pivot, Vector3(0, -0.44, -0.025), Vector3(0.088, 0.105, 0.058), skin)
	_sphere(pivot, Vector3(-side * 0.069, -0.415, -0.035), Vector3(0.037, 0.065, 0.032), skin)
	return pivot

func _build_rig_arm(suffix: String, side: float, shirt: StandardMaterial3D, skin: StandardMaterial3D) -> Node3D:
	var group := Node3D.new()
	group.name = "Arm_" + suffix
	model.add_child(group)
	limb_meshes["upper_arm_" + suffix] = _capsule(group, Vector3.ZERO, 0.103, 1.0, shirt)
	limb_meshes["forearm_" + suffix] = _capsule(group, Vector3.ZERO, 0.078, 1.0, skin)
	joint_meshes["elbow_" + suffix] = _sphere(group, Vector3.ZERO, Vector3.ONE * 0.079, skin)
	var hand := Node3D.new()
	group.add_child(hand)
	_sphere(hand, Vector3.ZERO, Vector3.ONE * 0.088, skin)
	_sphere(hand, Vector3(-side * 0.069, 0.025, -0.025), Vector3(0.037, 0.062, 0.032), skin)
	# Small knuckle pads retain a hand silhouette when the palms meet.
	for finger: int in range(3):
		_sphere(hand, Vector3(-0.039 + finger * 0.039, -0.041, -0.058), Vector3(0.026, 0.043, 0.022), skin)
	limb_hands[suffix] = hand
	return group

func _apply_human_pose(data: Dictionary, dt: float) -> void:
	body_pose = Pose.sample(data)
	torso_node.position = body_pose.torso
	(torso_node.get_child(0).mesh as CapsuleMesh).height = float(body_pose.torso_height)
	pelvis_mesh.position = body_pose.pelvis
	head.position = body_pose.head
	head.basis = body_pose.head_basis
	model.position = Vector3.ZERO
	if is_instance_valid(name_label):
		name_label.position.y = Vector3(body_pose.head).y + 0.43
	for piece: Dictionary in Pose.collision_segments(data):
		var key: String = str(piece.get("key", "")).replace("upperarm_", "upper_arm_")
		if not pose_colliders.has(key):
			continue
		var collider: StaticBody3D = pose_colliders[key]
		var from: Vector3 = piece.from
		var to: Vector3 = piece.to
		collider.position = (from + to) * 0.5
		if from.distance_to(to) > 0.001:
			collider.quaternion = Quaternion(Vector3.UP, (to - from).normalized())
			var shape: CapsuleShape3D = (collider.get_child(0) as CollisionShape3D).shape
			shape.height = from.distance_to(to) + float(piece.radius) * 2.0
	for suffix: String in ["l", "r"]:
		var hip: Vector3 = body_pose["hip_" + suffix]
		var knee: Vector3 = body_pose["knee_" + suffix]
		var ankle: Vector3 = body_pose["ankle_" + suffix]
		var shoulder: Vector3 = body_pose["shoulder_" + suffix]
		var elbow: Vector3 = body_pose["elbow_" + suffix]
		var hand: Vector3 = body_pose["hand_" + suffix]
		_pose_segment("thigh_" + suffix, hip, knee)
		_pose_segment("shin_" + suffix, knee, ankle)
		_pose_segment("upper_arm_" + suffix, shoulder, elbow)
		_pose_segment("forearm_" + suffix, elbow, hand)
		(joint_meshes["knee_" + suffix] as Node3D).position = knee
		(joint_meshes["elbow_" + suffix] as Node3D).position = elbow
		(limb_hands[suffix] as Node3D).position = hand
		(limb_hands[suffix] as Node3D).quaternion = Quaternion(Vector3.DOWN, (hand - elbow).normalized())
		(limb_feet[suffix] as Node3D).position = ankle + Vector3(0, 0, -0.09)
		(limb_feet[suffix] as Node3D).rotation.x = PI / 2.0
	_update_first_person_arms(data, dt)

func _pose_segment(key: String, from: Vector3, to: Vector3) -> void:
	var direction: Vector3 = to - from
	var distance: float = maxf(direction.length(), 0.001)
	var segment: MeshInstance3D = limb_meshes[key]
	segment.position = (from + to) * 0.5
	segment.quaternion = Quaternion(Vector3.UP, direction / distance)
	(segment.mesh as CapsuleMesh).height = distance + (segment.mesh as CapsuleMesh).radius * 2.0
	if pose_colliders.has(key):
		var collider: StaticBody3D = pose_colliders[key]
		collider.position = segment.position
		collider.quaternion = segment.quaternion
		var shape: CapsuleShape3D = (collider.get_child(0) as CollisionShape3D).shape
		shape.height = distance + shape.radius * 2.0

func _update_first_person_arms(data: Dictionary, dt: float) -> void:
	fps_root.position = body_pose.eye
	fps_root.rotation.x = float(data.get("pitch", 0.0))
	var swing: float = float(data.get("swing", 0.0))
	if swing > previous_swing + 0.04:
		swing_duration = maxf(swing, 0.12)
	previous_swing = swing
	var cooldown: float = float(Pose.SWING_SECONDS.get(current_tool, 0.8))
	var strike: float = sin(clampf((cooldown - swing) / Pose.SWING_GESTURE_SECONDS, 0.0, 1.0) * PI) if swing > 0.0 else 0.0
	var speed: float = float(data.get("motion_speed", 0.0))
	var gait: float = float(data.get("motion_phase", 0.0))
	var bob: float = sin(gait) * minf(speed, 5.0) * 0.018
	var blend: float = minf(dt * 24.0, 1.0)
	# Closing the palms changes their lateral anchor as well as their angle;
	# rotation alone leaves a large gap when the arms already point forwards.
	fps_left_arm.position.x = -0.31 + strike * (0.23 if current_tool == "hands" else 0.0)
	fps_right_arm.position.x = 0.31 - strike * (0.23 if current_tool == "hands" else 0.0)
	fps_left_arm.rotation.x = lerpf(fps_left_arm.rotation.x, 1.45 + bob + strike * (0.55 if current_tool == "hands" else 0.15), blend)
	fps_right_arm.rotation.x = lerpf(fps_right_arm.rotation.x, 1.45 - bob + strike * 0.65, blend)
	fps_left_arm.rotation.z = lerpf(fps_left_arm.rotation.z, strike * (0.18 if current_tool == "hands" else -0.12), blend)
	fps_right_arm.rotation.z = lerpf(fps_right_arm.rotation.z, strike * (-0.18 if current_tool == "hands" else 0.28), blend)

func _build_mosquito() -> void:
	model.scale = Vector3.ONE * MOSQUITO_VISUAL_SCALE
	var body: StandardMaterial3D = material(Color("294651"))
	var teal: StandardMaterial3D = material(Color("5ca99b"))
	primary_tint = teal
	secondary_tint = body
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
	var wing_mat: StandardMaterial3D = material(Color(0.84, 0.96, 0.95, 0.80))
	wing_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	wing_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	wing_mat.roughness = 0.35
	left_wing = _wing(-1.0, wing_mat)
	right_wing = _wing(1.0, wing_mat)
	_add_body_sphere(Vector3.ZERO, MOSQUITO_BODY_RADIUS)

func apply_appearance(raw: Variant) -> void:
	# Catalog values are bounded here as well as on the server. The rendering
	# path never allocates replacement materials or accessories on unchanged frames.
	var safe: Dictionary = CosmeticsData.appearance_for({actor_role: raw}, actor_role)
	var signature: String = "%d:%d" % [int(safe.color), int(safe.accessory)]
	if signature == appearance_signature:
		return
	appearance_signature = signature
	applied_appearance = safe
	var tint: Color = CosmeticsData.PALETTE[int(safe.color)]
	primary_tint.albedo_color = tint
	secondary_tint.albedo_color = tint.darkened(0.20 if actor_role == "human" else 0.52)
	if is_instance_valid(accessory_root):
		accessory_root.get_parent().remove_child(accessory_root)
		accessory_root.queue_free()
	accessory_root = Node3D.new()
	accessory_root.name = "CosmeticAccessory"
	var attachment: Node3D = head if actor_role == "human" else model
	attachment.add_child(accessory_root)
	var accessory: int = int(safe.accessory)
	if accessory == 0:
		return
	var frame: StandardMaterial3D = material(Color("243b4a"))
	var cream: StandardMaterial3D = material(Color("fff0c9"))
	if actor_role == "human":
		if accessory == 1:
			var cap_mat: StandardMaterial3D = material(tint.darkened(0.32))
			_sphere(accessory_root, Vector3(0, 0.22, 0.025), Vector3(0.256, 0.137, 0.244), cap_mat)
			_sphere(accessory_root, Vector3(0, 0.17, -0.235), Vector3(0.255, 0.022, 0.19), cap_mat)
			_sphere(accessory_root, Vector3(0, 0.358, 0.025), Vector3(0.029, 0.021, 0.029), cream)
			_sphere(accessory_root, Vector3(0, 0.235, -0.218), Vector3(0.047, 0.040, 0.013), cream)
		else:
			for side: float in [-1.0, 1.0]:
				_accessory_ring(Vector3(side * 0.087, 0.033, -0.247), 0.058, 0.073, frame)
				_segment(accessory_root, Vector3(side * 0.153, 0.038, -0.245), Vector3(side * 0.226, 0.040, -0.015), 0.011, frame)
			_segment(accessory_root, Vector3(-0.021, 0.043, -0.249), Vector3(0.021, 0.043, -0.249), 0.011, frame)
	else:
		if accessory == 1:
			var ribbon: StandardMaterial3D = material(Color("e88385"))
			for side: float in [-1.0, 1.0]:
				var bow: MeshInstance3D = _sphere(accessory_root, Vector3(side * 0.045, 0.095, -0.058), Vector3(0.052, 0.029, 0.023), ribbon)
				bow.rotation.z = side * 0.32
			_sphere(accessory_root, Vector3(0, 0.095, -0.075), Vector3(0.022, 0.023, 0.018), cream)
		else:
			var brass: StandardMaterial3D = material(Color("d8b763"))
			for side: float in [-1.0, 1.0]:
				_accessory_ring(Vector3(side * 0.047, 0.019, -0.141), 0.044, 0.055, brass)
				_segment(accessory_root, Vector3(side * 0.098, 0.021, -0.133), Vector3(side * 0.078, 0.026, -0.013), 0.010, frame)
			_segment(accessory_root, Vector3(-0.01, 0.018, -0.143), Vector3(0.01, 0.018, -0.143), 0.008, brass)

func _accessory_ring(at: Vector3, inner: float, outer: float, mat: Material) -> void:
	var geometry := TorusMesh.new()
	geometry.inner_radius = inner
	geometry.outer_radius = outer
	geometry.rings = 20
	geometry.ring_segments = 6
	var result: MeshInstance3D = mesh(accessory_root, geometry, at, mat)
	result.rotation.x = PI / 2.0

func _wing(side: float, mat: StandardMaterial3D) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = Vector3(side * 0.035, 0.055, 0.06)
	model.add_child(pivot)
	var wing: MeshInstance3D = _sphere(pivot, Vector3(side * 0.10, 0, 0.035), Vector3(0.15, 0.006, 0.075), mat)
	wing.rotation.y = side * 0.38
	wing.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var vein: StandardMaterial3D = material(Color("a4d5cc"))
	_segment(pivot, Vector3.ZERO, Vector3(side * 0.20, 0.005, 0.058), 0.003, vein).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return pivot

static func _woven_texture() -> ImageTexture:
	if is_instance_valid(cloth_texture):
		return cloth_texture
	var pixels := Image.create(32, 32, false, Image.FORMAT_RGB8)
	for y: int in range(32):
		for x: int in range(32):
			var thread: float = 0.975 if x % 4 == 0 or y % 4 == 0 else 1.0
			if (x / 4 + y / 4) % 2 == 0:
				thread -= 0.008
			pixels.set_pixel(x, y, Color(thread, thread, thread))
	pixels.generate_mipmaps()
	cloth_texture = ImageTexture.create_from_image(pixels)
	return cloth_texture

func _equip_tool(tool: String) -> void:
	current_tool = tool
	if is_instance_valid(held_tool):
		held_tool.queue_free()
	held_tool = make_tool(tool)
	tool_socket.add_child(held_tool)
	if is_instance_valid(fps_held_tool):
		fps_held_tool.queue_free()
	fps_held_tool = make_tool(tool)
	fps_tool_socket.add_child(fps_held_tool)

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
