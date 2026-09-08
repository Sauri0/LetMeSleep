extends SceneTree
const Prefs = preload("res://scripts/preferences.gd")
const Video = preload("res://scripts/video_settings.gd")
var failures := 0
var checks := 0
func _initialize() -> void: _run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures+=1
	print("VIDEO07 %s %s" % ["PASS" if ok else "FAIL",label])
func _run() -> void:
	if DisplayServer.get_name()=="headless": quit(1); return
	Prefs.load_settings()
	var existed:=FileAccess.file_exists(Prefs.FILE_PATH)
	var original:=FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if existed else PackedByteArray()
	var scene:=Node3D.new()
	root.add_child(scene)
	var light:=SpotLight3D.new()
	light.shadow_enabled=true
	scene.add_child(light)
	var soft:=OmniLight3D.new()
	soft.shadow_enabled=false
	scene.add_child(soft)
	var probe:=ReflectionProbe.new()
	scene.add_child(probe)
	for quality: int in [2,0,1,2]:
		Prefs.video_shadows=quality
		Video.apply_world(scene)
		check(light.shadow_enabled==(quality>0) and not soft.shadow_enabled,"quality changes preserve authored shadow choices")
		check(root.positional_shadow_atlas_size==[0,1024,2048][quality],"shadow atlas follows requested quality")
	Prefs.video_reflections=false
	Video.apply_world(scene)
	check(not probe.visible,"reflections can be disabled independently")
	Prefs.video_reflections=true
	Video.apply_world(scene)
	check(probe.visible,"reflection setting restores probe")
	for index: int in [0,1,2,3]:
		Prefs.video_resolution=index
		Prefs.video_vsync=false
		Prefs.video_fps=144
		Video.apply_display(root)
		for frame: int in range(3): await process_frame
		await RenderingServer.frame_post_draw
		check(root.content_scale_size==Video.SIZES[index],"selected image resolution is retained")
		check(Vector2i(root.get_texture().get_size())==Video.SIZES[index],"actual render texture matches requested pixels")
	check(Engine.max_fps==144 and DisplayServer.window_get_vsync_mode()==DisplayServer.VSYNC_DISABLED,"high-refresh cap works with sync disabled")
	Prefs.video_fps=0
	Prefs.video_vsync=true
	Video.apply_display(root)
	check(Engine.max_fps==0 and DisplayServer.window_get_vsync_mode()==DisplayServer.VSYNC_ENABLED,"unlimited mode does not impose 60 FPS and sync remains selectable")
	check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if existed else not FileAccess.file_exists(Prefs.FILE_PATH),"graphics checks do not write saved user preferences")
	scene.queue_free()
	await process_frame
	print("VIDEO07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)
