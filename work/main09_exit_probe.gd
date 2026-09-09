extends SceneTree
const Main = preload("res://scripts/main.gd")
func _initialize() -> void: _run.call_deferred()
func _run() -> void:
	var app := Main.new()
	root.add_child(app)
	await create_timer(.2).timeout
	print("MAIN09_EXIT_PROBE requested=7")
	app.request_exit(7)
