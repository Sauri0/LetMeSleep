extends SceneTree
var preview: Control
var failures := 0
var checks := 0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("PREVIEW05 %s %s" % ["PASS" if ok else "FAIL",message])

func settle() -> void:
	await process_frame
	await process_frame
	await process_frame

func _run() -> void:
	root.size = Vector2i(960,720)
	preview = load("res://scripts/avatar_preview.gd").new()
	preview.size = Vector2(960,720)
	root.add_child(preview)
	preview.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	await settle()
	check(preview.viewport.own_world_3d,"preview owns isolated world")
	for role: String in ["human","mosquito"]:
		for style: int in range(3):
			var appearance: Dictionary = {"color":style+1,"accessory":style,"face":style,"hair":style,"outfit":style,"accent":5-style}
			preview.set_avatar(role,appearance)
			await settle()
			check(preview.avatar.applied_appearance==appearance,"all six fields applied for %s%d" % [role,style])
			check(preview.avatar.face_root.get_child_count()>0 and preview.avatar.hair_root.get_child_count()>0,"real face/hair meshes %s%d" % [role,style])
			check(preview.avatar.body_shapes[0].collision_layer==0,"preview body excluded from collision %s%d" % [role,style])
			var signature: String = preview.avatar.appearance_signature
			var face_id: int = preview.avatar.face_root.get_instance_id()
			for frame: int in range(20):
				preview.set_avatar(role,appearance)
			check(preview.avatar.appearance_signature==signature and preview.avatar.face_root.get_instance_id()==face_id,"unchanged appearance allocates no replacement meshes")
			if DisplayServer.get_name()!="headless":
				await RenderingServer.frame_post_draw
				var folder: String = ProjectSettings.globalize_path("res://../outputs/0.5-preview")
				DirAccess.make_dir_recursive_absolute(folder)
				check(root.get_texture().get_image().save_png(folder.path_join("modelo-%s-%d.png" % [role,style]))==OK,"native model capture")
	preview.set_avatar("human",{})
	preview.avatar.set_local(true)
	check(preview.avatar.left_arm.visible and preview.avatar.right_arm.visible and not preview.avatar.fps_root.visible,"local human uses real arms and no duplicate viewmodel")
	check(not preview.avatar.head.visible and preview.avatar.torso_node.visible and preview.avatar.pelvis_mesh.visible,"only local head is hidden, body remains visible")
	var wheel := InputEventMouseButton.new()
	wheel.button_index = MOUSE_BUTTON_WHEEL_UP
	wheel.pressed = true
	for frame: int in range(30):
		preview._gui_input(wheel)
	check(is_equal_approx(preview.zoom,0.72),"zoom-in bounded")
	wheel.button_index = MOUSE_BUTTON_WHEEL_DOWN
	for frame: int in range(30):
		preview._gui_input(wheel)
	check(is_equal_approx(preview.zoom,1.35),"zoom-out bounded")
	preview.reset_view()
	check(is_equal_approx(preview.zoom,1.0) and is_equal_approx(preview.orbit_yaw,-0.25),"reset restores orbit and zoom")
	preview.queue_free()
	await settle()
	print("PREVIEW05_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
