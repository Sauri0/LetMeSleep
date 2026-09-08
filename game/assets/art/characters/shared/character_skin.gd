extends Node3D
## Imported editable deformation rig. Its skeleton follows the authoritative pose;
## body capsules, motion rules and private surface assignment remain in gameplay.
const Clothing = preload("res://assets/art/characters/shared/cloth.gdshader")
const CosmeticsData = preload("res://scripts/cosmetics.gd")
const FacialExpression = preload("res://assets/art/characters/shared/facial_expression.gd")
const Tools = preload("res://scripts/tool_catalog.gd")
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
var facial_detail_near := false
var facial_detail_initialized := false
const FACIAL_NEAR_LOD_BIAS := 1000.0

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
		var category: String = str(node.name).split("_")[1]
		if category in ["face","eyes","brows","mouth"]:
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
	facial_applied.clear()
	material_cache.clear()
	var primary: Color = CosmeticsData.PALETTE[int(safe.get("color",0))]
	var accent: Color = CosmeticsData.PALETTE[int(safe.get("accent",0))]
	var hair_tint: Color = CosmeticsData.human_hair_color(safe) if species=="human" else Color.WHITE
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
					elif species=="human" and key.begins_with("hair"):
						standard.albedo_color = hair_tint
					if key.begins_with("wing"):
						standard.cull_mode = BaseMaterial3D.CULL_DISABLED
				material_cache[key] = replacement
			mesh.set_surface_override_material(surface,material_cache[key])

func _update_visibility() -> void:
	for mesh: MeshInstance3D in meshes:
		var pieces: PackedStringArray = String(mesh.name).split("_")
		var category: String = pieces[1] if pieces.size()>1 else "core"
		var selected := true
		if pieces.size()>2 and category in ["face","eyes","brows","mouth","mustache","beard","hair","outfit","accessory","footwear"]:
			selected = int(pieces[2])==int(appearance.get("eyes",appearance.get("face",0)) if category=="face" else appearance.get(category,0))
		if category=="hair" and species=="human":
			var capped: bool = int(appearance.get("accessory",0)) in [1,3]
			selected = selected and (String(mesh.name).ends_with("_capped")==capped)
		if first_person and species=="human" and category in ["head","face","eyes","brows","mouth","mustache","beard","hair","accessory"]:
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
			if pose.has("hand_width_"+side):
				var forearm_axis := (Vector3(pose["hand_"+side])-Vector3(pose["elbow_"+side])).normalized()
				var original_forearm := (_point(contract["forearm_"+side].to)-_point(contract["forearm_"+side].from)).normalized()
				var base_width := Basis(Quaternion(original_forearm,forearm_axis))*Vector3.RIGHT
				base_width=(base_width-forearm_axis*base_width.dot(forearm_axis)).normalized()
				var desired_width: Vector3 = pose["hand_width_"+side]
				desired_width=(desired_width-forearm_axis*desired_width.dot(forearm_axis)).normalized()
				if desired_width.length_squared()>.5:
					var twist := atan2(forearm_axis.dot(base_width.cross(desired_width)),base_width.dot(desired_width))
					_set_bone("forearm_"+side,pose["elbow_"+side],pose["hand_"+side],Basis(forearm_axis,twist*.80))
			var hand: Vector3 = pose["hand_"+side]
			var direction: Vector3 = pose.get("hand_direction_"+side,(hand-Vector3(pose["elbow_"+side])).normalized())
			_set_bone("hand_"+side,hand,hand+direction*0.10)
			if pose.has("hand_width_"+side):
				var id: int = bone_ids["hand_"+side]
				var original := (_point(contract["hand_"+side].to)-_point(contract["hand_"+side].from)).normalized()
				var original_width := (Vector3.RIGHT-original*Vector3.RIGHT.dot(original)).normalized()
				var width: Vector3 = pose["hand_width_"+side]
				var change := Basis(width,direction,width.cross(direction))*Basis(original_width,original,original_width.cross(original)).inverse()
				skeleton.set_bone_global_pose(id,Transform3D(change*skeleton.get_bone_global_rest(id).basis,hand))
		var target_grip: float = 1.0 if side=="r" and Tools.GRASPS.has(str(data.get("tool","hands"))) else 0.0
		var throwing: Dictionary = data.get("throw_gesture",{})
		if side=="r" and str(throwing.get("state","idle"))=="release":
			target_grip *= 1.0-smoothstep(.80,1.0,float(throwing.get("progress",0.0)))
		if bool(strike.get("active",false)) and str(strike.get("hand","right"))==("left" if side=="l" else "right") and str(strike.get("tool","hands"))=="hands":
			target_grip = 0.0
		var previous_grip: float = hand_grip[side]
		hand_grip[side] = lerpf(previous_grip,target_grip,1.0-exp(-16.0*dt))
		var grip: float = hand_grip[side]
		if not changed and absf(grip-previous_grip)<.0001: continue
		_pose_fingers(side,grip,pose,str(data.get("tool","hands")))

func _pose_fingers(side: String, grip: float, pose: Dictionary, tool: String) -> void:
	var hand_id: int = bone_ids["hand_"+side]
	var hand_change := skeleton.get_bone_global_pose(hand_id)*skeleton.get_bone_global_rest(hand_id).affine_inverse()
	var original_axis := (_point(contract["hand_"+side].to)-_point(contract["hand_"+side].from)).normalized()
	var width := (hand_change.basis*(Vector3.RIGHT-original_axis*Vector3.RIGHT.dot(original_axis))).normalized()
	var longitudinal := (hand_change.basis*original_axis).normalized()
	var normal := width.cross(longitudinal).normalized()
	var radius := float(Tools.GRASPS.get(tool,{"radius":.015}).radius)+.014
	var depth := float(Tools.GRASPS.get(tool,{}).get("depth",radius-.014))+.014
	var centre: Vector3 = pose.get("tool_grip",Vector3.ZERO)
	var bend := Basis(width,.10*(1.0-grip))
	for finger: int in range(4):
		var a := "finger%d_a_%s"%[finger,side]
		var b := "finger%d_b_%s"%[finger,side]
		if not contract.has(a) or not contract.has(b): continue
		var base: Vector3 = hand_change*_point(contract[a].from)
		var joint := base+bend*(hand_change.basis*(_point(contract[a].to)-_point(contract[a].from)))
		var tip := joint+bend*(hand_change.basis*(_point(contract[b].to)-_point(contract[b].from)))
		var along := (_point(contract[a].from)-_point(contract["hand_"+side].from)).dot(Vector3.RIGHT)
		joint=joint.lerp(centre+width*along+longitudinal*depth*.82-normal*radius*.55,grip)
		tip=tip.lerp(centre+width*along+longitudinal*depth*.50+normal*radius*.84,grip)
		_set_digit_bone(a,base,joint,hand_change)
		_set_digit_bone(b,joint,tip,hand_change)
	var ta := "thumb_a_"+side
	var tb := "thumb_b_"+side
	if contract.has(ta) and contract.has(tb):
		var base: Vector3 = hand_change*_point(contract[ta].from)
		var joint: Vector3 = hand_change*_point(contract[ta].to)
		var tip: Vector3 = hand_change*_point(contract[tb].to)
		var target_tip := centre-width*.038-normal*radius*.25-longitudinal*depth*.90
		var target_joint := base.lerp(target_tip,.52)-width*.012
		joint=joint.lerp(target_joint,grip)
		tip=tip.lerp(target_tip,grip)
		_set_digit_bone(ta,base,joint,hand_change)
		_set_digit_bone(tb,joint,tip,hand_change)

func _set_digit_bone(name: String, from: Vector3, to: Vector3, palm_change: Transform3D) -> void:
	var id: int = bone_ids[name]
	var original := _point(contract[name].to)-_point(contract[name].from)
	var direction := to-from
	# Preserve palm roll when solving a phalanx in world space. A shortest arc
	# from the unposed rest alone twists the finger relative to its own knuckle.
	var change := Basis(Quaternion((palm_change.basis*original).normalized(),direction.normalized()))*palm_change.basis
	var target := Transform3D(change*skeleton.get_bone_global_rest(id).basis,from)
	target.basis.y *= direction.length()/maxf(original.length(),.001)
	skeleton.set_bone_global_pose(id,target)

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
	facial_values = facial.advance(data,species,dt,int(appearance.get("eyes",appearance.get("face",0))))
	facial_elapsed += dt
	if first_person and species=="human": return
	_update_facial_detail()
	var camera := get_viewport().get_camera_3d()
	var distance := camera.global_position.distance_to(skeleton.to_global(skeleton.get_bone_global_pose(int(bone_ids.head)).origin)) if camera!=null else 0.0
	var near := 7.0 if species=="human" else 1.8
	var interval := 1.0/30.0 if distance<near else .10 if distance<near*3.0 else .30
	var approaching := facial_distance>=near and distance<near
	facial_distance = distance
	if facial_elapsed<interval and not approaching: return
	facial_elapsed = 0.0
	apply_facial_values(facial_values)

func apply_facial_values(values: Dictionary) -> void:
	## Shared by gameplay and exact-state gallery fixtures. Correctives preserve
	## the brow/eye attachment when either eyelid closes during an expression.
	_update_facial_detail()
	var effective: Dictionary = values.duplicate()
	for control: String in ["BrowUp","BrowDown"]:
		for blink: String in ["BlinkL","BlinkR"]:
			effective[control+blink]=float(values.get(control,0.0))*float(values.get(blink,0.0))
	effective.BrowUpBrowDown=float(values.get("BrowUp",0.0))*float(values.get("BrowDown",0.0))
	for blink: String in ["BlinkL","BlinkR"]:
		effective["BrowUpBrowDown"+blink]=float(effective.BrowUpBrowDown)*float(values.get(blink,0.0))
		var closure := clampf(float(values.get(blink,0.0)),0.0,1.0)
		for knot: int in range(1,8):
			effective[blink+"Arc"+str(knot)]=maxf(0.0,1.0-absf(closure*8.0-float(knot)))
	for mesh: MeshInstance3D in face_channels:
		if not mesh.visible: continue
		var channels: Dictionary = face_channels[mesh]
		var applied: Dictionary = facial_applied.get(mesh,{})
		for channel: String in channels:
			var value := float(effective.get(channel,0.0))
			if channels.has(channel) and (not applied.has(channel) or absf(float(applied[channel])-value)>.0001):
				mesh.set_blend_shape_value(int(channels[channel]),value)
				applied[channel] = value
		facial_applied[mesh] = applied
		# Allows the source script to run while the previous GLB is still imported.
		if channels.has("Blink") and not channels.has("BlinkL"):
			mesh.set_blend_shape_value(int(channels.Blink),maxf(float(values.get("BlinkL",0.0)),float(values.get("BlinkR",0.0))))

func _update_facial_detail() -> void:
	## Automatic LOD was simplified in the open pose and pierced closed lids.
	## Keep the authored facial index set at reading distance; body/hair retain
	## their existing LOD. Scale-aware distance also covers the isolated editor.
	if skeleton==null: return
	var camera := get_viewport().get_camera_3d()
	var scale_factor := global_transform.basis.get_scale().abs()
	var model_scale := maxf(scale_factor.x,maxf(scale_factor.y,scale_factor.z))
	var near_distance := (7.0 if species=="human" else 1.8)*model_scale/(1.0 if species=="human" else .35)
	var distance := camera.global_position.distance_to(skeleton.to_global(skeleton.get_bone_global_pose(int(bone_ids.head)).origin)) if camera!=null else 0.0
	var near_now := distance<=near_distance
	if facial_detail_initialized and near_now==facial_detail_near: return
	facial_detail_initialized = true
	facial_detail_near = near_now
	for mesh: MeshInstance3D in face_channels:
		mesh.lod_bias = FACIAL_NEAR_LOD_BIAS if near_now else 1.0
