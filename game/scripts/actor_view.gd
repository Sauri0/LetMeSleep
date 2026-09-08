class_name ActorView
extends Node3D

const CosmeticsData = preload("res://scripts/cosmetics.gd")
const Pose = preload("res://scripts/human_pose.gd")
const MosquitoPoseData = preload("res://scripts/mosquito_pose.gd")
const Rounded = preload("res://assets/procedural_shapes.gd")
const ImportedSkin = preload("res://assets/art/characters/shared/character_skin.gd")
const SwatterArt = preload("res://assets/art/house/swatter.glb")
const RacketArt = preload("res://assets/art/characters/tools/racket.glb")
const BroomArt = preload("res://assets/art/characters/tools/broom.glb")
const NewspaperArt = preload("res://assets/art/house/newspaper.glb")
const MOSQUITO_VISUAL_SCALE: float = 0.35
const MOSQUITO_BODY_RADIUS: float = 0.04
static var cloth_texture: ImageTexture

# Editable imported characters follow the authoritative pose; capsules own play.
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
var face_root: Node3D
var hair_root: Node3D
var outfit_root: Node3D
var detail_material: StandardMaterial3D
var face_eyes: Array[MeshInstance3D] = []
var sleeve_cuffs: Array[MeshInstance3D] = []
var movement_state: String = ""
var stun_blend: float = 0.0
var mosquito_legs: Node3D
var stun_sparkles: Node3D
var help_icon: Label3D
var imported_skin: Node3D
var legacy_geometry_dirty := false
var mosquito_orientation := Quaternion.IDENTITY
var human_snapshot_hash := 0
var human_snapshot_values: Dictionary = {}

func build(role: String, display_name: String, tint_index: int = 0) -> void:
	actor_role = role
	fallback_color = posmod(tint_index, CosmeticsData.PALETTE.size())
	model = Node3D.new()
	add_child(model)
	if role == "human":
		_build_human(tint_index)
	else:
		_build_mosquito()
	imported_skin = ImportedSkin.new()
	imported_skin.name = "ImportedCharacter"
	model.add_child(imported_skin)
	imported_skin.setup(role)
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
	_hide_legacy_geometry()

func set_local(value: bool) -> void:
	local_view = value
	if is_instance_valid(head):
		head.visible = not value
	if actor_role == "human" and is_instance_valid(left_arm):
		left_arm.visible = true
		right_arm.visible = true
		fps_root.visible = false
		for collar: MeshInstance3D in collar_meshes:
			collar.visible = not is_instance_valid(imported_skin)
	if is_instance_valid(imported_skin):
		imported_skin.set_first_person(value and actor_role=="human")
	if is_instance_valid(name_label):
		name_label.visible = not value

func update_name_visibility(camera: Camera3D) -> void:
	if not is_instance_valid(name_label) or preview_only:
		return
	var minimum_distance: float = 1.0 if actor_role == "mosquito" else 0.75
	name_label.visible = visible and not local_view and not (actor_role=="mosquito" and movement_state=="biting") and (not is_instance_valid(camera) or camera.global_position.distance_to(global_position) >= minimum_distance)
	if actor_role=="mosquito" and movement_state=="stunned":
		name_label.visible = false
	if is_instance_valid(camera):
		var distance: float = camera.global_position.distance_to(name_label.global_position)
		var focal: float = get_viewport().get_visible_rect().size.y*0.5/tan(deg_to_rad(camera.fov*0.5))
		name_label.pixel_size = minf(0.005 if actor_role=="human" else 0.0018,(22.0 if actor_role=="human" else 16.0)*distance/(float(name_label.font_size)*focal))

func body_collision_rids() -> Array[RID]:
	var result: Array[RID] = []
	for body: StaticBody3D in body_shapes:
		result.append(body.get_rid())
	return result

func update_state(data: Dictionary, dt: float) -> void:
	clock_time += dt
	apply_appearance(data.get("appearance", {"color": fallback_color, "accessory": 0}))
	if legacy_geometry_dirty:
		_hide_legacy_geometry()
	var target: Vector3 = data.get("p", Vector3.ZERO)
	if not initialized or global_position.distance_to(target) > 2.2:
		global_position = target
		initialized = true
	else:
		global_position = global_position.lerp(target, 1.0 - exp(-18.0 * dt))
	rotation.y = float(data.get("body_yaw",data.get("yaw",0.0))) if actor_role=="human" else lerp_angle(rotation.y,float(data.get("yaw",0.0)),1.0-exp(-20.0*dt))
	visible = bool(data.get("alive", true))
	movement_state = str(data.get("state",movement_state))
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
		stun_blend = move_toward(stun_blend,1.0 if state=="stunned" else 0.0,dt*4.5)
		var flutter: float = sin(clock_time * 80.0) * (0.6 if flying else 0.08)*(1.0-stun_blend)
		left_wing.rotation.z = lerpf(-0.22+flutter,-0.05,stun_blend)
		right_wing.rotation.z = lerpf(0.22-flutter,0.05,stun_blend)
		left_wing.rotation.y = stun_blend*1.25
		right_wing.rotation.y = -stun_blend*1.25
		# Authoritative public-state anatomy owns banking and surface alignment.
		# Interpolate snapshots in world space, then put both mesh and ray shapes
		# in that same displayed frame; no independent acceleration-only tilt.
		var target_orientation: Basis = MosquitoPoseData.orientation(data)
		mosquito_orientation = mosquito_orientation.slerp(target_orientation.get_rotation_quaternion(),1.0-exp(-14.0*dt))
		var local_orientation: Basis = global_basis.orthonormalized().inverse()*Basis(mosquito_orientation)
		model.basis = local_orientation.scaled(Vector3.ONE*MOSQUITO_VISUAL_SCALE)
		mosquito_legs.scale = Vector3(lerpf(1.0,0.62,stun_blend),lerpf(1.0,0.28,stun_blend),1.0)
		stun_sparkles.visible = state=="stunned"
		stun_sparkles.rotation.y = clock_time*1.5
		# Wing animation carries flight movement; body origin is the authoritative
		# contact centre, without a cosmetic bob that suggests drifting controls.
		# The folded pose reserves 16mm for the eye rim above the physical floor.
		model.position = Vector3(0,stun_blend*0.016,0)
		for index: int in range(MosquitoPoseData.LOCAL_SEGMENTS.size()):
			var piece: Dictionary = MosquitoPoseData.LOCAL_SEGMENTS[index]
			var from: Vector3 = local_orientation*Vector3(piece.from)+model.position
			var to: Vector3 = local_orientation*Vector3(piece.to)+model.position
			var collider: StaticBody3D = body_shapes[index]
			collider.position = (from+to)*.5
			if from.distance_to(to)>.001:collider.quaternion=Quaternion(Vector3.UP,(to-from).normalized())
		if state!="stunned":
			help_icon.visible = false
		if is_instance_valid(imported_skin):
			imported_skin.apply_mosquito(data,clock_time,stun_blend)

func _hide_legacy_geometry() -> void:
	# Retain the collider/socket scaffold while only the exported deformation
	# meshes render. Tools remain attached to the authoritative hand socket.
	for child: Node in model.get_children():
		if child==imported_skin:
			continue
		_hide_scaffold(child)
	legacy_geometry_dirty = false

func _hide_scaffold(node: Node) -> void:
	if node==held_tool:
		return
	if node is GeometryInstance3D:
		(node as GeometryInstance3D).visible = false
	for child: Node in node.get_children():
		_hide_scaffold(child)

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
	detail_material = material(Color("e8b889"))
	torso_node = Node3D.new()
	model.add_child(torso_node)
	_capsule(torso_node, Vector3.ZERO, 0.24, 0.68, shirt)
	pelvis_mesh = _sphere(model, Vector3(0, 0.73, 0.025), Vector3.ONE * 0.20, trousers)
	# Pajama buttons, rounded collar, two trouser legs and soft slippers.
	for height: float in [1.29, 1.13, 0.97]:
		collar_meshes.append(_sphere(torso_node, Vector3(0, height - 1.09, -0.209), Vector3(0.016, 0.016, 0.010), white))
	for side: float in [-1.0, 1.0]:
		var suffix: String = "l" if side < 0 else "r"
		limb_meshes["thigh_" + suffix] = _capsule(model, Vector3.ZERO, 0.105, 1.0, trousers)
		limb_meshes["shin_" + suffix] = _capsule(model, Vector3.ZERO, 0.105, 1.0, trousers)
		joint_meshes["knee_" + suffix] = _sphere(model, Vector3.ZERO, Vector3.ONE * 0.105, trousers)
		limb_feet[suffix] = _capsule(model, Vector3.ZERO, 0.105, 0.43, dark)
		var collar: MeshInstance3D = Rounded.rounded(torso_node,Vector3(side*0.077,0.265,-0.125),Vector3(0.105,0.032,0.10),0.014,white)
		collar.rotation.y = side*0.35
		collar_meshes.append(collar)
	left_arm = _build_rig_arm("l", -1.0, shirt, skin)
	right_arm = _build_rig_arm("r", 1.0, shirt, skin)
	tool_socket = Node3D.new()
	(limb_hands["r"] as Node3D).add_child(tool_socket)
	fps_root = Node3D.new()
	fps_root.name = "UnusedLegacyViewmodel"
	model.add_child(fps_root)
	fps_root.visible = false
	head = Node3D.new()
	head.position.y = 1.55
	model.add_child(head)
	_sphere(head, Vector3.ZERO, Vector3.ONE * 0.235, skin)
	for side: float in [-1.0, 1.0]:
		_sphere(head, Vector3(side * 0.236, -0.01, 0), Vector3(0.045, 0.07, 0.045), skin)
	_sphere(head, Vector3(0, -0.025, -0.225), Vector3(0.045, 0.055, 0.056), skin)
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
	Rounded.rounded(hand,Vector3.ZERO,Vector3(0.135,0.15,0.12),0.052,skin)
	_sphere(hand, Vector3(-side * 0.060, 0.018, -0.01), Vector3(0.029, 0.045, 0.035), skin)
	# Small knuckle pads retain a hand silhouette when the palms meet.
	for finger: int in range(4):
		_sphere(hand, Vector3(-0.045 + finger * 0.03, -0.040, -0.034), Vector3(0.017, 0.041, 0.030), skin)
	var cuff := TorusMesh.new()
	cuff.inner_radius = 0.073
	cuff.outer_radius = 0.082
	cuff.rings = 16
	cuff.ring_segments = 6
	sleeve_cuffs.append(mesh(hand,cuff,Vector3(0,0.085,0),detail_material))
	limb_hands[suffix] = hand
	return group

func _apply_human_pose(data: Dictionary, dt: float) -> void:
	var snapshot_hash := hash(data)
	if snapshot_hash==human_snapshot_hash and human_snapshot_values==data and not body_pose.is_empty():
		if is_instance_valid(imported_skin): imported_skin.apply_human(body_pose,data,dt)
		return
	human_snapshot_hash = snapshot_hash
	human_snapshot_values = data.duplicate(true)
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
		var shape: Shape3D = (collider.get_child(0) as CollisionShape3D).shape
		if shape is CapsuleShape3D:
			collider.quaternion = Quaternion(Vector3.UP, (to-from).normalized()) if from.distance_to(to)>.001 else Quaternion.IDENTITY
			shape.radius = float(piece.radius)
			shape.height = from.distance_to(to)+float(piece.radius)*2.0
		elif shape is SphereShape3D:
			shape.radius = float(piece.radius)
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
	if body_pose.has("tool_direction"):
		var direction: Vector3 = body_pose.tool_direction if str(Dictionary(data.get("strike",{})).get("hand","right"))=="right" else Vector3.DOWN
		if direction.length_squared()>0.001:
			var normal: Vector3 = body_pose.get("tool_normal",Vector3.BACK)
			var up := -direction.normalized()
			var back := (normal-up*normal.dot(up)).normalized()
			tool_socket.basis = (limb_hands["r"] as Node3D).basis.inverse()*Basis(up.cross(back).normalized(),up,back)
	if is_instance_valid(imported_skin):
		imported_skin.apply_human(body_pose,data,dt)

func _pose_segment(key: String, from: Vector3, to: Vector3) -> void:
	var direction: Vector3 = to - from
	var distance: float = maxf(direction.length(), 0.001)
	var segment: MeshInstance3D = limb_meshes[key]
	segment.position = (from + to) * 0.5
	segment.quaternion = Quaternion(Vector3.UP, direction / distance)
	(segment.mesh as CapsuleMesh).height = distance + (segment.mesh as CapsuleMesh).radius * 2.0
	# The shared capsule loop above owns ray shapes. The hidden scaffold's full
	# limb must not stretch a tapered distal collider back over the entire arm.

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
	_sphere(model, Vector3(0, 0.005, -0.065), Vector3(0.068, 0.070, 0.065), teal)
	mosquito_legs = Node3D.new()
	mosquito_legs.name = "Legs"
	model.add_child(mosquito_legs)
	for side: float in [-1.0, 1.0]:
		for z: float in [-0.02, 0.05, 0.12]:
			_segment(mosquito_legs, Vector3(side * 0.045, -0.025, z), Vector3(side * 0.13, -0.09, z + 0.02), 0.006, body)
			_segment(mosquito_legs, Vector3(side * 0.13, -0.09, z + 0.02), Vector3(side * 0.15, -0.15, z - 0.015), 0.005, body)
	_segment(model, Vector3(0, -0.015, -0.11), Vector3(0, -0.034, -0.24), 0.009, body)
	var wing_mat: StandardMaterial3D = material(Color(0.84, 0.96, 0.95, 0.80))
	wing_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	wing_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	wing_mat.roughness = 0.35
	left_wing = _wing(-1.0, wing_mat)
	right_wing = _wing(1.0, wing_mat)
	stun_sparkles = Node3D.new()
	stun_sparkles.name = "StunSparkles"
	stun_sparkles.position.y = 0.082
	add_child(stun_sparkles)
	var sparkle: StandardMaterial3D = material(Color("ffe5a0"))
	sparkle.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	for index: int in range(3):
		var angle: float = TAU*float(index)/3.0
		var glint: MeshInstance3D = _sphere(stun_sparkles,Vector3(cos(angle)*0.047,0,sin(angle)*0.047),Vector3(0.007,0.011,0.007),sparkle)
		glint.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	stun_sparkles.visible = false
	help_icon = Label3D.new()
	help_icon.name = "HelpIcon"
	help_icon.text = "+"
	help_icon.position.y = 0.16
	help_icon.font_size = 32
	help_icon.pixel_size = 0.0015
	help_icon.modulate = Color("ffe6a3")
	help_icon.outline_modulate = Color("244a50")
	help_icon.outline_size = 9
	help_icon.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	help_icon.no_depth_test = false
	help_icon.visible = false
	add_child(help_icon)
	for piece: Dictionary in MosquitoPoseData.local_segments():
		var distance: float = Vector3(piece.from).distance_to(piece.to)
		if distance<.001:_add_body_sphere(piece.from,float(piece.radius))
		else:_add_body_capsule((Vector3(piece.from)+Vector3(piece.to))*.5,float(piece.radius),distance+float(piece.radius)*2.0)

func apply_appearance(raw: Variant) -> void:
	# Catalog values are bounded here as well as on the server. The rendering
	# path never allocates replacement materials or accessories on unchanged frames.
	var safe: Dictionary = CosmeticsData.appearance_for({actor_role: raw}, actor_role)
	var signature: String = JSON.stringify(safe)
	if signature == appearance_signature:
		return
	appearance_signature = signature
	applied_appearance = safe
	legacy_geometry_dirty = true
	if is_instance_valid(imported_skin):
		imported_skin.set_appearance(safe)
	var tint: Color = CosmeticsData.PALETTE[int(safe.color)]
	primary_tint.albedo_color = tint
	secondary_tint.albedo_color = tint.darkened(0.20 if actor_role == "human" else 0.52)
	_refresh_style(safe)
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

func _refresh_style(safe: Dictionary) -> void:
	for branch: Node3D in [face_root,hair_root,outfit_root]:
		if is_instance_valid(branch):
			branch.get_parent().remove_child(branch)
			branch.queue_free()
	face_eyes.clear()
	face_root = Node3D.new()
	hair_root = Node3D.new()
	outfit_root = Node3D.new()
	face_root.name = "FaceStyle"
	hair_root.name = "HairStyle"
	outfit_root.name = "OutfitStyle"
	var attachment: Node3D = head if actor_role=="human" else model
	attachment.add_child(face_root)
	attachment.add_child(hair_root)
	(torso_node if actor_role=="human" else model).add_child(outfit_root)
	var face: int = int(safe.get("face",0))
	var hair: int = int(safe.get("hair",0))
	var outfit: int = int(safe.get("outfit",0))
	var accent: Color = CosmeticsData.PALETTE[int(safe.get("accent",0))]
	var trim: StandardMaterial3D = material(accent)
	var dark: StandardMaterial3D = material(Color("243342"))
	var white: StandardMaterial3D = material(Color("f6e6c5"))
	if actor_role=="human":
		detail_material.albedo_color = accent
		primary_tint.albedo_texture = _outfit_texture(CosmeticsData.PALETTE[int(safe.color)],accent,outfit)
		primary_tint.albedo_color = Color.WHITE
		primary_tint.uv1_scale = Vector3.ONE
		for side: float in [-1.0,1.0]:
			var eye_height: float = 0.038 if face==1 else 0.059
			_sphere(face_root,Vector3(side*0.082,0.037,-0.20),Vector3(0.054,eye_height,0.031),white)
			_sphere(face_root,Vector3(side*0.082,0.028,-0.226),Vector3(0.023,eye_height*0.60,0.012),dark)
			_sphere(face_root,Vector3(side*0.074,0.047,-0.237),Vector3(0.008,0.009,0.003),white)
			var brow: MeshInstance3D = _capsule(face_root,Vector3(side*0.082,0.116 if face!=1 else 0.086,-0.204),0.014,0.085,dark)
			brow.rotation.z = side*(0.95 if face==2 else 1.7 if face==1 else 1.35)
			_sphere(face_root,Vector3(side*0.13,-0.069,-0.185),Vector3(0.036,0.023,0.009),material(Color("ce927c")))
		var mouth: MeshInstance3D = _capsule(face_root,Vector3(0,-0.13,-0.197),0.011,0.070 if face!=1 else 0.036,dark)
		mouth.rotation.z = PI/2.0 if face!=1 else 0.0
		_sphere(hair_root,Vector3(0,0.17,0.035),Vector3(0.239,0.105,0.218),dark)
		if hair==0:
			for i: int in range(3):
				var lock: MeshInstance3D = _sphere(hair_root,Vector3(-0.12+i*0.11,0.185,-0.10),Vector3(0.085,0.064,0.14),dark)
				lock.rotation.z = -0.25
		elif hair==1:
			for i: int in range(4):
				var lock: MeshInstance3D = _sphere(hair_root,Vector3(-0.055+i*0.039,0.24+i*0.008,-0.01),Vector3(0.05,0.13,0.11),dark)
				lock.rotation.z = -0.45-i*0.10
		else:
			for i: int in range(9):
				var angle: float = i*TAU/9.0
				_sphere(hair_root,Vector3(sin(angle)*0.17,0.195+float(i%2)*0.035,cos(angle)*0.13),Vector3(0.092,0.095,0.095),dark)
			_sphere(hair_root,Vector3(0,0.25,0),Vector3(0.10,0.085,0.10),dark)
		if outfit==2:
			for side: float in [-1.0,1.0]:
				var lapel: MeshInstance3D = _sphere(outfit_root,Vector3(side*0.09,0.19,-0.17),Vector3(0.055,0.105,0.015),trim)
				lapel.rotation.z = -side*0.4
	else:
		for side: float in [-1.0,1.0]:
			var height: float = 0.033 if face==2 else 0.050
			_sphere(face_root,Vector3(side*0.047,0.018,-0.095),Vector3(0.044,height,0.035),trim)
			_sphere(face_root,Vector3(side*0.047,0.014,-0.124),Vector3(0.018,height*0.53,0.008),dark)
			_sphere(face_root,Vector3(side*0.040,0.031,-0.132),Vector3(0.007,0.008,0.003),white)
			if face==1:
				_segment(face_root,Vector3(side*0.022,0.065,-0.117),Vector3(side*0.083,0.049,-0.113),0.009,dark)
			var base := Vector3(side*0.028,0.053,-0.08)
			if hair==0:
				_segment(hair_root,base,Vector3(side*0.092,0.15,-0.092),0.007,dark)
			elif hair==1:
				var previous: Vector3 = base
				for i: int in range(5):
					var tip := Vector3(side*(0.032+sin(i*0.55)*0.074),0.07+i*0.016,-0.08-i*0.006)
					_segment(hair_root,previous,tip,0.007,dark)
					previous = tip
			else:
				_segment(hair_root,base,Vector3(side*0.087,0.15,-0.092),0.006,dark)
				for i: int in range(4):
					var stem := Vector3(side*(0.033+i*0.014),0.072+i*0.019,-0.085)
					_segment(hair_root,stem,stem+Vector3(side*0.025,0.013,0.005),0.005,trim)
			_sphere(hair_root,Vector3(side*0.092,0.15,-0.092),Vector3.ONE*0.011,white)
		if outfit==1:
			for i: int in range(7):
				var angle: float = i*2.399
				_sphere(outfit_root,Vector3(sin(angle)*0.046,-0.012+cos(angle)*0.043,0.13+i*0.017),Vector3(0.011,0.011,0.013),trim)
		else:
			for z: float in ([0.12,0.17,0.22] if outfit==0 else [0.14,0.205]):
				var ring := TorusMesh.new()
				ring.inner_radius = 0.039 if outfit==2 else 0.044
				ring.outer_radius = 0.055
				ring.rings = 16
				ring.ring_segments = 6
				mesh(outfit_root,ring,Vector3(0,-0.012,z),trim).rotation.x = PI/2.0

static func _outfit_texture(base: Color, accent: Color, style: int) -> ImageTexture:
	var pixels := Image.create(64,64,false,Image.FORMAT_RGB8)
	for y: int in range(64):
		for x: int in range(64):
			var color: Color = base
			if style==0 and x%16<2:
				color = base.lerp(accent,0.43)
			elif style==1 and y%14<5:
				color = accent
			elif style==2:
				color = base.darkened(0.28) if x<21 or x>43 else accent
			var weave: float = 0.98 if x%3==0 or y%3==0 else 1.0
			pixels.set_pixel(x,y,color*weave)
	pixels.generate_mipmaps()
	return ImageTexture.create_from_image(pixels)

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

static func make_tool(tool: String) -> Node3D:
	var root := Node3D.new()
	var coral: StandardMaterial3D = material(Color("e47e65"))
	var dark: StandardMaterial3D = material(Color("274b54"))
	var metal: StandardMaterial3D = material(Color("bcd5cb"))
	var paper: StandardMaterial3D = material(Color("f6e7c9"))
	if tool == "hands":
		return root
	if tool in ["racket","broom"]:
		var imported: Node3D = (RacketArt if tool=="racket" else BroomArt).instantiate()
		imported.name = "AuthoredTool"
		root.add_child(imported)
		return root
	if tool in ["swatter","newspaper"]:
		var imported: Node3D = (SwatterArt if tool=="swatter" else NewspaperArt).instantiate()
		imported.name = "AuthoredTool"
		imported.rotation.z = PI
		# Authoring origin is the grip base and +Y is the long axis. Match the
		# shared active-face centre exactly, without changing contact statistics.
		imported.scale = Vector3.ONE*(0.46/0.55 if tool=="swatter" else 0.30/0.32)
		root.add_child(imported)
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
