extends SceneTree
## Native comparison on the same world/camera. Does not save any preferences.
const World = preload("res://scripts/world.gd")
const UI = preload("res://scripts/ui.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
const BASELINE_AREAS := {"human-bite":46788.0,"human-task":41808.0,"mosquito-ready":46788.0,"mosquito-charge":46788.0,"mosquito-stunned":46788.0,"mosquito-rescue":46788.0}
var checks := 0
var failures := 0
var records: Array[Dictionary] = []
var studio: Node3D
var interface: CanvasLayer
var output: String

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, description: String) -> void:
	checks += 1
	if not value: failures += 1
	print("HUD06 %s %s" % ["PASS" if value else "FAIL",description])

func _run() -> void:
	output = ProjectSettings.globalize_path("res://../outputs/0.6-hud/")
	DirAccess.make_dir_recursive_absolute(output)
	# The working copy permits exact before/after images. Source-only checkouts
	# can still validate the current layout against the measured baseline areas.
	var before: GDScript
	var baseline_path := ProjectSettings.globalize_path("res://../work/hud06-before-ui.gd")
	if FileAccess.file_exists(baseline_path):
		before = GDScript.new()
		before.source_code = FileAccess.get_file_as_string(baseline_path)
		if before.reload() != OK:
			printerr("HUD06 baseline script cannot load")
			quit(1)
			return
	studio = World.new()
	root.add_child(studio)
	studio.build()
	studio.load_map("house")
	if is_instance_valid(studio.audio_fx): studio.audio_fx.set_process(false)
	var sizes := [Vector2i(1280,720),Vector2i(1920,1080)]
	for viewport_size: Vector2i in sizes:
		root.size = viewport_size
		root.content_scale_size = Vector2i(1280,720)
		root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
		for scene: String in ["human-bite","human-task","mosquito-ready","mosquito-charge","mosquito-stunned","mosquito-rescue"]:
			var human := scene.begins_with("human")
			var actors := {
				1:{"role":"human","name":"","p":Vector3(0,0,1),"yaw":0.0,"body_yaw":0.0,"pitch":-0.7,"state":"human","alive":true,"tool":"hands","appearance":Cosmetics.appearance_for(Cosmetics.default_profile(),"human")},
				2:{"role":"mosquito","name":"","p":Vector3(0.35,1.1,0),"yaw":PI,"state":"flying","alive":true,"appearance":Cosmetics.appearance_for(Cosmetics.default_profile(),"mosquito")},
			}
			var local_id: int = 1 if human else 2
			studio.set_local_role(local_id,"human" if human else "mosquito")
			studio.sync_actors(actors,local_id,1.0)
			var camera: Camera3D = studio.menu_camera
			camera.position = Vector3(0,1.6,1.0) if human else Vector3(0.35,1.45,2.0)
			camera.look_at(Vector3(0,0.6,0.15) if human else Vector3(0.1,1.15,0.1))
			camera.make_current()
			var data := {"actors":actors,"config":{"mode":"sleep" if scene=="human-task" else "blood","blood_goal":12},"blood":3.2,"elapsed":32.0,"time_left":82.0,"task_goal":3,"tasks_done":1}
			var personal: Dictionary = {}
			match scene:
				"human-bite": personal = {"bite_feedback":{"active":true,"count":1,"side":"left"}}
				"human-task": personal = {"task":{"name":"Revisar la heladera","remaining":18.9,"work":3.0,"progress":1.4}}
				"mosquito-ready": personal = {"assignment":{"label":"Antebrazo"},"focus":{"state":"ready"}}
				"mosquito-charge": personal = {"assignment":{"label":"Antebrazo"},"focus":{"state":"charging","progress":0.45}}
				"mosquito-stunned":
					actors[2].state = "stunned"
					personal = {"stun":{"active":true,"remaining":18.0,"helped":false}}
				"mosquito-rescue": personal = {"help":{"state":"helping","target":3,"progress":0.5,"remaining":17.5}}
			studio.sync_actors(actors,local_id,1.0)
			var baseline_area: float = BASELINE_AREAS[scene]
			var versions: Array[String] = ["before","after"] if before != null else ["after"]
			for version: String in versions:
				interface = before.new() if version=="before" else UI.new()
				root.add_child(interface)
				interface.show_game(data,personal,local_id)
				await process_frame
				await process_frame
				await RenderingServer.frame_post_draw
				var pixels := _panel_area(interface._hud)
				var filename := "%s-%s-%d.png" % [version,scene,viewport_size.y]
				root.get_texture().get_image().save_png(output.path_join(filename))
				records.append({"file":filename,"panel_area":pixels,"canvas_area":1280*720,"role":"human" if human else "mosquito","state":scene,"version":version})
				if version=="before": baseline_area=pixels
				else:
					check(pixels < baseline_area*0.75,"Panels occupy at least 25% less area: " + scene + " " + str(viewport_size.y))
					check(_fits(interface._hud),"Visible HUD panels stay inside canvas: " + scene + " " + str(viewport_size.y))
					check(not interface._help_open and interface._hud_help.visible,"Help stays folded with a discoverable shortcut: " + scene)
				root.remove_child(interface)
				interface.free()
				await process_frame
	var result := {"checks":checks,"failures":failures,"comparisons":records,"baseline_sha256":"45C0DE15C22A0FE3E44FAB60A167E7680B0AB38F17F5DBFA6D6BE5E3A96F9687","note":"HUD fixture using the real World and fixed cameras; panel rectangle area is not a gameplay/performance metric. No preference writes."}
	var report := FileAccess.open(output.path_join("comparison.json"),FileAccess.WRITE)
	report.store_string(JSON.stringify(result,"  "))
	report.close()
	print("HUD06_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)

func _panel_area(node: Node) -> float:
	var total := 0.0
	if node is PanelContainer and node.is_visible_in_tree(): total += node.get_global_rect().get_area()
	for child: Node in node.get_children(): total += _panel_area(child)
	return total

func _fits(node: Node) -> bool:
	if node is PanelContainer and node.is_visible_in_tree():
		var rect: Rect2 = node.get_global_rect()
		if rect.position.x<0 or rect.position.y<0 or rect.end.x>1280.1 or rect.end.y>720.1: return false
	for child: Node in node.get_children():
		if not _fits(child): return false
	return true
