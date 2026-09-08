extends RefCounted
const Prefs = preload("res://scripts/preferences.gd")
const SIZES: Array[Vector2i] = [Vector2i(1280,720),Vector2i(1920,1080),Vector2i(2560,1440),Vector2i(3840,2160)]
const CAPS: Array[int] = [0,60,120,144,165,240]

static func apply_display(window: Window) -> void:
	if DisplayServer.get_name()=="headless": return
	var requested := SIZES[clampi(Prefs.video_resolution,0,SIZES.size()-1)]
	window.content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT
	window.content_scale_aspect=Window.CONTENT_SCALE_ASPECT_KEEP
	window.content_scale_size=requested
	window.mode=Window.MODE_FULLSCREEN if Prefs.video_fullscreen else Window.MODE_WINDOWED
	if not Prefs.video_fullscreen:
		var usable := DisplayServer.screen_get_usable_rect(window.current_screen)
		var limit := usable.size-Vector2i(24,64)
		var factor := minf(1.0,minf(float(limit.x)/requested.x,float(limit.y)/requested.y))
		window.size=Vector2i(Vector2(requested)*factor)
		window.position=usable.position+(usable.size-window.size)/2
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED if Prefs.video_vsync else DisplayServer.VSYNC_DISABLED)
	Engine.max_fps=Prefs.video_fps
	window.get_tree().call_group("video_settings","apply_video_settings")

static func apply_world(world: Node) -> void:
	var viewport := world.get_viewport()
	viewport.positional_shadow_atlas_size=[0,1024,2048][clampi(Prefs.video_shadows,0,2)]
	_visit(world)

static func _visit(node: Node) -> void:
	if node is Light3D:
		if not node.has_meta("authored_shadows"): node.set_meta("authored_shadows",node.shadow_enabled)
		node.shadow_enabled=bool(node.get_meta("authored_shadows")) and Prefs.video_shadows>0
	elif node is ReflectionProbe:
		node.visible=Prefs.video_reflections
	for child: Node in node.get_children(): _visit(child)
