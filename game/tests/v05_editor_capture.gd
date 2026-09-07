extends SceneTree
## UI-only camera capture. Cosmetic fixture data lives in process memory only;
## never calls any option commit or close handler that saves preferences.
const Prefs = preload("res://scripts/preferences.gd")
const CosmeticsData = preload("res://scripts/cosmetics.gd")

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	root.size = Vector2i(1280,720)
	Prefs.setup_inputs()
	Prefs.cosmetics = CosmeticsData.sanitize({
		"human":{"color":1,"face":2,"hair":2,"outfit":1,"accessory":2,"accent":4},
		"mosquito":{"color":3,"face":1,"hair":2,"outfit":2,"accessory":0,"accent":0}
	})
	var ui: CanvasLayer = load("res://scripts/ui.gd").new()
	root.add_child(ui)
	ui._open_customization()
	for role: String in ["human","mosquito"]:
		ui._select_custom_role(role)
		ui._select_custom_category("hair" if role=="human" else "outfit")
		for frame: int in range(15):
			await process_frame
		await RenderingServer.frame_post_draw
		var path: String = ProjectSettings.globalize_path("res://../outputs/0.5-preview/editor-"+role+".png")
		print("EDITOR05_CAPTURE %s code=%d" % [role,root.get_texture().get_image().save_png(path)])
	ui.queue_free()
	await process_frame
	await process_frame
	quit(0)
