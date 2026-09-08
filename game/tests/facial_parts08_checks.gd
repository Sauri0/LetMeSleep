extends SceneTree
## Imported geometry selection, shared expressions, colour invariance and FPS.
const Actor = preload("res://scripts/actor_view.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
const Pose = preload("res://scripts/human_pose.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures += 1
		if failures<30: print("FACIAL_PARTS08_FAIL ",message)

func visible_signature(skin: Node3D, category_filter: Array[String] = []) -> Array:
	var result: Array = []
	for mesh: MeshInstance3D in skin.meshes:
		if not category_filter.is_empty() and str(mesh.name).split("_")[1] not in category_filter: continue
		if mesh.visible: result.append([str(mesh.name),mesh.mesh.get_rid(),mesh.transform,mesh.skin])
	return result

func skeleton_signature(skin: Node3D) -> Array:
	var result: Array = []
	for index: int in range(skin.skeleton.get_bone_count()): result.append(skin.skeleton.get_bone_pose(index))
	return result

func _run() -> void:
	for role: String in ["human","mosquito"]:
		var actor := Actor.new()
		root.add_child(actor)
		actor.build(role,"",0)
		var skin: Node3D = actor.imported_skin
		check(skin.skeleton.get_bone_count()==(36 if role=="human" else 21),role+" skeleton preserved")
		check(skin.face_channels.size()==9,role+" three independent sets of three facial parts")
		for mesh: MeshInstance3D in skin.face_channels:
			var category: String = str(mesh.name).split("_")[1]
			for surface: int in range(mesh.mesh.get_surface_count()):
				var material: String = mesh.mesh.surface_get_material(surface).resource_name
				check(category=="eyes" or material not in ["eye_white","pupil"],"eye components never split into mouth or brows "+str(mesh.name))
			for channel: String in Facial.CHANNELS:
				check(skin.face_channels[mesh].has(channel),str(mesh.name)+" channel "+channel)
			if role=="mosquito" and category=="brows":
				for control: String in ["BrowUp","BrowDown"]:
					for blink: String in ["BlinkL","BlinkR"]:
						check(skin.face_channels[mesh].has(control+blink),"brow retains coupled eyelid attachment "+str(mesh.name))
		for eyes: int in range(3):
			for brows: int in range(3):
				for mouth: int in range(3):
					var appearance := {"eyes":eyes,"brows":brows,"mouth":mouth,"hair":0,"outfit":0,"accessory":0,"footwear":0}
					actor.apply_appearance(appearance)
					var visible := 0
					for mesh: MeshInstance3D in skin.face_channels:
						if not mesh.visible: continue
						visible += 1
						var parts := str(mesh.name).split("_")
						check(int(parts[2])==int(appearance[parts[1]]),role+" independent selection "+str(mesh.name))
					check(visible==3,role+" exactly three facial parts visible")
					for expression: String in ["neutral","sleepy","alert","effort","impact"]:
						var data := {"state":"human" if role=="human" else "flying","preview_only":true,"facial_preview":expression,"facial_no_blink":true}
						for frame: int in range(12): skin._animate_face(data,.10)
						for mesh: MeshInstance3D in skin.face_channels:
							if not mesh.visible: continue
							for channel: String in ["BlinkL","BlinkR","BrowUp","BrowDown","MouthOpen","MouthSmile","MouthPress"]:
								var value := mesh.get_blend_shape_value(int(skin.face_channels[mesh][channel]))
								check(is_finite(value) and absf(value-float(skin.facial_values[channel]))<.00011,"active part receives shared expression "+str(mesh.name)+" "+expression+channel)
							if role=="mosquito" and str(mesh.name).split("_")[1]=="brows":
								for control: String in ["BrowUp","BrowDown"]:
									for blink: String in ["BlinkL","BlinkR"]:
										var corrector: String=control+blink
										check(absf(mesh.get_blend_shape_value(skin.face_channels[mesh][corrector])-float(skin.facial_values[control])*float(skin.facial_values[blink]))<.00011,"actual corrective equals visible control product")
		var fixed := {"eyes":2,"brows":1,"mouth":0,"hair":2,"outfit":1,"accessory":2,"footwear":1,"mustache":2,"beard":2}
		actor.apply_appearance(fixed)
		var geometry := visible_signature(skin)
		var head_categories: Array[String] = ["head","eyes","brows","mouth","hair","accessory","mustache","beard"]
		var head_geometry := visible_signature(skin,head_categories)
		var bone_poses := skeleton_signature(skin)
		for outfit: int in range(3):
			for footwear: int in range(3):
				var changed_clothes := fixed.duplicate()
				changed_clothes.outfit = outfit; changed_clothes.footwear = footwear
				actor.apply_appearance(changed_clothes)
				check(visible_signature(skin,head_categories)==head_geometry,role+" all 9 garment/footwear choices preserve every selected head mesh/transform/skin")
				check(skeleton_signature(skin)==bone_poses,role+" clothes never alter a bone pose")
		actor.apply_appearance(fixed)
		var body_geometry := visible_signature(skin,["core","outfit","footwear","wing"])
		for eyes: int in range(3):
			for brows: int in range(3):
				for mouth: int in range(3):
					var changed_head := fixed.duplicate()
					changed_head.eyes=eyes;changed_head.brows=brows;changed_head.mouth=mouth
					actor.apply_appearance(changed_head)
					check(visible_signature(skin,["core","outfit","footwear","wing"])==body_geometry,role+" every independent face combination preserves the actual body meshes")
		actor.apply_appearance(fixed)
		for color: int in range(6):
			for accent: int in range(6):
				for hair_color: int in range(6 if role=="human" else 1):
					var tinted := fixed.duplicate()
					tinted.color = color; tinted.accent = accent; tinted.hair_color = hair_color
					actor.apply_appearance(tinted)
					check(visible_signature(skin)==geometry,role+" tint changes no selected mesh, transform, skin or geometry resource")
					check(skeleton_signature(skin)==bone_poses,role+" tint never alters a bone pose")
					if role=="human":
						check(Color(skin.material_cache.hair.albedo_color).is_equal_approx(Cosmetics.HAIR_PALETTE[hair_color]),"hair brows mustache beard share independent tint")
						check(Color(skin.material_cache.hair_matte.albedo_color).is_equal_approx(Cosmetics.HAIR_PALETTE[hair_color]),"matte H0 preserves the same independent hair palette")
						check(is_zero_approx(float(skin.material_cache.hair_matte.metallic_specular)) and is_equal_approx(float(skin.material_cache.hair_matte.roughness),1.0),"H0 native material retains measured matte finish")
		if role=="human":
			for mustache: int in range(3):
				for beard: int in range(3):
					fixed.mustache = mustache; fixed.beard = beard
					actor.apply_appearance(fixed)
					var visible_hair := {"mustache":0,"beard":0}
					for mesh: MeshInstance3D in skin.meshes:
						var pieces := str(mesh.name).split("_")
						if mesh.visible and pieces[1] in visible_hair:
							visible_hair[pieces[1]] += 1
							check(int(pieces[2])==int(fixed[pieces[1]]),"facial hair independent selection")
					check(visible_hair.mustache==(0 if mustache==0 else 1) and visible_hair.beard==(0 if beard==0 else 1),"none option removes only corresponding facial hair")
			actor.set_local(true)
			for mesh: MeshInstance3D in skin.meshes:
				if str(mesh.name).split("_")[1] in ["head","eyes","brows","mouth","mustache","beard","hair","accessory"]:
					check(not mesh.visible,"all local head details hidden "+str(mesh.name))
			check(skin.meshes.any(func(mesh: MeshInstance3D) -> bool: return mesh.visible and str(mesh.name)=="human_core"),"physical first-person arms remain visible")
		actor.queue_free()
		await process_frame
	var shader_source: String = FileAccess.get_file_as_string("res://assets/art/characters/shared/cloth.gdshader")
	check("void fragment()" in shader_source and not "void vertex(" in shader_source and not "VERTEX" in shader_source,"cloth colours/pattern are fragment shading only, with no vertex displacement")
	print("FACIAL_PARTS08_RESULT checks=%d failures=%d"%[checks,failures])
	quit(failures)
