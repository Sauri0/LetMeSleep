extends SceneTree
## The generated catalog already owns the roof; rendering a second box at the
## same height produced the moving brown/cream stripes in the user's recording.
var failures := 0
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var world: Node3D = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	for map_id: String in ["house", "house-v1-1", "house-v1-2"]:
		world.load_map(map_id)
		var area := 0.0
		var paint_only := true
		for node: Node in world.map_root.find_children("*", "MeshInstance3D", true, false):
			var mesh := node as MeshInstance3D
			if not mesh.mesh is BoxMesh: continue
			var box: AABB = mesh.global_transform * mesh.mesh.get_aabb()
			if absf(box.position.y - float(world.map_data.ceiling)) > .0001: continue
			area += box.size.x * box.size.z
			paint_only = paint_only and mesh.get_active_material(0) == world.ceiling_paint
		var expected: float = 4.0 * float(world.map_data.half_x) * float(world.map_data.half_z)
		if absf(area - expected) > .01 or not paint_only:
			failures += 1
			printerr("CEILING_FAIL %s area=%.3f expected=%.3f paint_only=%s" % [map_id, area, expected, paint_only])
		print("CEILING_CHECK %s area=%.3f expected=%.3f" % [map_id, area, expected])
	world.queue_free()
	await process_frame
	await process_frame
	print("CEILING_RESULT maps=3 failures=", failures)
	quit(0 if failures == 0 else 1)
