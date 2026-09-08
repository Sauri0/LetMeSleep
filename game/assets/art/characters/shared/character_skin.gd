extends Node3D
## Imported editable deformation rig. Its skeleton follows the authoritative pose;
## body capsules, motion rules and private surface assignment remain in gameplay.
const Clothing = preload("res://assets/art/characters/shared/cloth.gdshader")
const CosmeticsData = preload("res://scripts/cosmetics.gd")
const FacialExpression = preload("res://assets/art/characters/shared/facial_expression.gd")
var species := "human"
var asset: Node3D
var skeleton: Skeleton3D
var contract: Dictionary = {}
var meshes: Array[MeshInstance3D] = []
var material_cache: Dictionary = {}
var bone_ids: Dictionary = {}
var appearance: Dictionary = {}
var first_person := false
var time := 0.0
var signature := ""
var hand_grip := {"l":0.0,"r":0.0}
var flight_blend := 1.0
var bite_blend := 0.0
var insect_previous_time := 0.0
var human_pose_hash := 0
var human_pose_values: Dictionary = {}
var facial: RefCounted
var facial_values: Dictionary = {}
var facial_elapsed := 0.0
var facial_distance := 0.0
var face_channels: Dictionary = {}
var facial_applied: Dictionary = {}

func setup(role: String, source_path: String = "", contract_path: String = "") -> void:
	species = role
	facial = FacialExpression.new(float(posmod(get_instance_id()*197,997))/997.0)
	var packed: PackedScene = load(source_path if not source_path.is_empty() else "res://assets/art/characters/%s/%s_lms06.glb" % [role,role])
	asset = packed.instantiate()
	add_child(asset)
	skeleton = asset.find_child("*Skeleton*",true,false) as Skeleton3D
	if skeleton==null:
		for child: Node in asset.find_children("*","Skeleton3D",true,false):
			skeleton = child as Skeleton3D
			break
	assert(skeleton!=null,"Character GLB requires a deformation skeleton")
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(contract_path if not contract_path.is_empty() else "res://assets/art/characters/%s/rig_contract.json" % role))
	contract = Dictionary(parsed).get("bones",{})
	for i: int in range(skeleton.get_bone_count()):
		bone_ids[skeleton.get_bone_name(i)] = i
	for node: Node in asset.find_children("*","MeshInstance3D",true,false):
		meshes.append(node as MeshInstance3D)
		if str(node.name).begins_with(species+"_face_"):
			var channels: Dictionary = {}
			for index: int in range(node.mesh.get_blend_shape_count()):
				channels[str(node.mesh.get_blend_shape_name(index))] = index
			face_channels[node] = channels
	for node: Node in asset.find_children("*","AnimationPlayer",true,false):
		(node as AnimationPlayer).stop()
		(node as AnimationPlayer).active = false
	set_appearance({})

func set_first_person(value: bool) -> void:
	first_person = value
	_update_visibility()

func set_appearance(value: Dictionary) -> void:
	var safe: Dictionary = CosmeticsData.appearance_for({species:value},species)
	var next_signature: String = JSON.stringify(safe)
	if next_signature==signature:
		return
	appearance = safe
	signature = next_signature
	_update_visibility()
	material_cache.clear()
	var primary: Color = CosmeticsData.PALETTE[int(safe.get("color",0))]
	var accent: Color = CosmeticsData.PALETTE[int(safe.get("accent",0))]
	for mesh: MeshInstance3D in meshes:
		for surface: int in range(mesh.mesh.get_surface_count()):
			var original: Material = mesh.mesh.surface_get_material(surface)
			var key: String = original.resource_name
			if not material_cache.has(key):
				var replacement: Material = original.duplicate()
				if species=="human" and key.begins_with("primary"):
					var cloth := ShaderMaterial.new()
					cloth.shader = Clothing
					cloth.set_shader_parameter("primary_color",primary)
					cloth.set_shader_parameter("accent_color",accent)
					cloth.set_shader_parameter("pattern",int(safe.get("outfit",0)))
					replacement = cloth
				elif replacement is StandardMaterial3D:
					var standard: StandardMaterial3D = replacement
					if key.begins_with("insect_primary") or key in ["primary","cap_cloth"]:
						standard.albedo_color = primary
					elif key.begins_with("accent"):
						standard.albedo_color = accent
					elif key.begins_with("insect_dark"):
						standard.albedo_color = primary.darkened(0.62)
					if key.begins_with("wing"):
						standard.cull_mode = BaseMaterial3D.CULL_DISABLED
				material_cache[key] = replacement
			mesh.set_surface_override_material(surface,material_cache[key])

func _update_visibility() -> void:
	for mesh: MeshInstance3D in meshes:
		var pieces: PackedStringArray = String(mesh.name).split("_")
		var category: String = pieces[1] if pieces.size()>1 else "core"
		var selected := true
		if pieces.size()>2 and category in ["face","hair","outfit","accessory","footwear"]:
			selected = int(pieces[2])==int(appearance.get(category,0))
		if category=="hair" and species=="human":
			var capped: bool = int(appearance.get("accessory",0)) in [1,3]
			selected = selected and (String(mesh.name).ends_with("_capped")==capped)
		if first_person and species=="human" and category in ["head","face","hair","accessory"]:
			selected = false
		mesh.visible = selected

func _point(value: Array) -> Vector3:
	return Vector3(float(value[0]),float(value[1]),float(value[2]))

func _set_bone(name: String, from: Vector3, to: Vector3, orientation: Basis=Basis.IDENTITY, stretch: bool=true) -> void:
	if not bone_ids.has(name) or not contract.has(name):
		return
	var id: int = bone_ids[name]
	var rest: Transform3D = skeleton.get_bone_global_rest(id)
	var definition: Dictionary = contract[name]
	var original_from: Vector3 = _point(definition.from)
	var original_direction: Vector3 = _point(definition.to)-original_from
	var direction: Vector3 = to-from
	var change := Basis(Quaternion(original_direction.normalized(),direction.normalized()))
	var target := Transform3D(orientation*change*rest.basis,from)
	if stretch:
		# Bone Y is its own longitudinal direction after glTF import.
		target.basis.y *= direction.length()/maxf(original_direction.length(),0.001)
	skeleton.set_bone_global_pose(id,target)

func apply_human(pose: Dictionary, data: Dictionary, dt: float) -> void:
	time += dt
	var strike: Dictionary = data.get("strike",{})
	_animate_face(data,dt)
	var new_hash := hash(pose)
	var changed := new_hash!=human_pose_hash or human_pose_values!=pose
	human_pose_hash = new_hash
	if changed:
		human_pose_values = pose.duplicate(true)
		_set_bone("pelvis",pose.pelvis,Vector3(pose.pelvis)+Vector3.UP*0.18)
		_set_bone("torso",pose.torso,Vector3(pose.torso)+Vector3.UP*(0.16*float(pose.get("torso_height",0.68))/0.68),pose.get("torso_basis",Basis.IDENTITY))
		_set_bone("head",pose.head,Vector3(pose.head)+Vector3.UP*0.19,pose.head_basis,false)
	for side: String in ["l","r"]:
		if changed:
			_set_bone("thigh_"+side,pose["hip_"+side],pose["knee_"+side])
			_set_bone("shin_"+side,pose["knee_"+side],pose["ankle_"+side])
			_set_bone("foot_"+side,pose["ankle_"+side],Vector3(pose["ankle_"+side])+Vector3(pose.get("foot_direction_"+side,Vector3.FORWARD))*.22)
			_set_bone("upperarm_"+side,pose["shoulder_"+side],pose["elbow_"+side])
			_set_bone("forearm_"+side,pose["elbow_"+side],pose["hand_"+side])
			var hand: Vector3 = pose["hand_"+side]
			var direction: Vector3 = (hand-Vector3(pose["elbow_"+side])).normalized()
			_set_bone("hand_"+side,hand,hand+direction*0.10)
		var target_grip: float = .55 if side=="r" and str(data.get("tool","hands"))!="hands" else .10
		if bool(strike.get("active",false)) and str(strike.get("hand","right"))==("left" if side=="l" else "right") and str(strike.get("tool","hands"))=="hands":
			target_grip = 0.0
		var previous_grip: float = hand_grip[side]
		hand_grip[side] = lerpf(previous_grip,target_grip,1.0-exp(-16.0*dt))
		var grip: float = hand_grip[side]
		if not changed and absf(grip-previous_grip)<.0001: continue
		for finger: int in range(4):
			for section: String in ["a","b"]:
				var name: String = "finger%d_%s_%s" % [finger,section,side]
				if bone_ids.has(name):
					var id: int = bone_ids[name]
					skeleton.set_bone_pose_rotation(id,skeleton.get_bone_rest(id).basis.get_rotation_quaternion()*Quaternion(Vector3.RIGHT,grip))

func apply_mosquito(data: Dictionary, clock_time: float, stun: float) -> void:
	time = clock_time
	var dt := clampf(clock_time-insect_previous_time,0.0,.10)
	insect_previous_time = clock_time
	var state: String = str(data.get("state","flying"))
	var flying: bool = state=="flying"
	flight_blend = move_toward(flight_blend,1.0 if flying else 0.0,dt*6.0)
	bite_blend = move_toward(bite_blend,1.0 if state=="biting" else 0.0,dt*8.0)
	var speed := clampf(Vector3(data.get("velocity",Vector3.ZERO)).length()/3.8,0.0,1.0)
	_animate_face(data,dt)
	var proboscis_id: int = int(bone_ids.get("proboscis",-1))
	if proboscis_id>=0:
		var rest: Transform3D = skeleton.get_bone_global_rest(proboscis_id)
		var bend := Basis(Vector3.RIGHT,-.82*bite_blend)
		skeleton.set_bone_global_pose(proboscis_id,Transform3D(bend*rest.basis,rest.origin))
	for side: String in ["l","r"]:
		var sign: float = -1.0 if side=="l" else 1.0
		var wing: String = "wing_"+side
		var rest: Transform3D = skeleton.get_bone_global_rest(bone_ids[wing])
		var flap: float = sin(time*(80.0+speed*9.0)+sign*.09)*lerpf(.035,.64,flight_blend)*(1.0-stun)
		var folded := Basis(Vector3.UP,sign*(stun*1.25+(1.0-flight_blend)*.35))*Basis(Vector3.BACK,sign*(0.20+flap))
		skeleton.set_bone_global_pose(bone_ids[wing],Transform3D(folded*rest.basis,rest.origin))
		for leg: int in range(3):
			for section: String in ["a","b"]:
				var name: String = "leg%d_%s_%s" % [leg,section,side]
				var amount: float = stun*.65+flight_blend*(.12+speed*.13+sin(time*4.1+leg*1.7+sign)*.055)
				if section=="b": amount *= -0.7
				var id: int = bone_ids[name]
				skeleton.set_bone_pose_rotation(id,skeleton.get_bone_rest(id).basis.get_rotation_quaternion()*Quaternion(Vector3.FORWARD,sign*amount))
				if section=="b" and state in ["perched","biting"]:
					# Local +Y is the real surface normal after ActorView's alignment.
					# End the tarsus on that plane instead of burying it in the wall.
					var posed: Transform3D = skeleton.get_bone_global_pose(id)
					var authored: Vector3 = Vector3(contract[name].to[0],contract[name].to[1],contract[name].to[2])-Vector3(contract[name].from[0],contract[name].from[1],contract[name].from[2])
					var end: Vector3 = posed.origin+posed.basis*skeleton.get_bone_global_rest(id).basis.inverse()*authored
					end.y = lerpf(end.y,-.1235 if state=="biting" else -.1095,1.0-flight_blend)
					_set_bone(name,posed.origin,end)
		var antenna: String = "antenna_"+side
		var antenna_id: int = bone_ids[antenna]
		skeleton.set_bone_pose_rotation(antenna_id,skeleton.get_bone_rest(antenna_id).basis.get_rotation_quaternion()*Quaternion(Vector3.FORWARD,sign*(sin(time*2.2)*.07+speed*.08)*(1.0-stun)))

func _animate_face(data: Dictionary, dt: float) -> void:
	if facial==null: return
	facial_values = facial.advance(data,species,dt,int(appearance.get("face",0)))
	facial_elapsed += dt
	if first_person and species=="human": return
	var camera := get_viewport().get_camera_3d()
	var distance := camera.global_position.distance_to(skeleton.to_global(skeleton.get_bone_global_pose(int(bone_ids.head)).origin)) if camera!=null else 0.0
	var near := 7.0 if species=="human" else 1.8
	var interval := 1.0/30.0 if distance<near else .10 if distance<near*3.0 else .30
	var approaching := facial_distance>=near and distance<near
	facial_distance = distance
	if facial_elapsed<interval and not approaching: return
	facial_elapsed = 0.0
	for mesh: MeshInstance3D in face_channels:
		if not mesh.visible: continue
		var channels: Dictionary = face_channels[mesh]
		var applied: Dictionary = facial_applied.get(mesh,{})
		for channel: String in FacialExpression.CHANNELS:
			var value := float(facial_values[channel])
			if channels.has(channel) and (not applied.has(channel) or absf(float(applied[channel])-value)>.0001):
				mesh.set_blend_shape_value(int(channels[channel]),value)
				applied[channel] = value
		facial_applied[mesh] = applied
		# Allows the source script to run while the previous GLB is still imported.
		if channels.has("Blink") and not channels.has("BlinkL"):
			mesh.set_blend_shape_value(int(channels.Blink),maxf(float(facial_values.BlinkL),float(facial_values.BlinkR)))
