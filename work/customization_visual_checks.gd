extends SceneTree

var failures: int = 0
var world: Node3D

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	print("CUSTOM_VISUAL %s %s" % ["PASS" if condition else "FAIL", label])
	if not condition:
		failures += 1

func _run() -> void:
	world = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	var roster: Dictionary = {
		1:{"role":"human","name":"Local","alive":true,"p":Vector3.ZERO,"appearance":{"color":3,"accessory":1}},
		2:{"role":"human","name":"Friend","alive":true,"p":Vector3(1,0,0),"appearance":{"color":1,"accessory":2}},
	}
	world.set_local_role(1,"lobby")
	world.sync_actors(roster,1,1.0)
	var local: ActorView = world.actors[1]
	check(local.head.visible and not local.local_view, "lobby own head retained in third person")
	check(local.applied_appearance == roster[1].appearance, "lobby human appearance applied")
	check(local.accessory_root.get_child_count() > 0, "cap geometry present")
	var accessory_id: int = local.accessory_root.get_instance_id()
	var material_id: int = local.primary_tint.get_instance_id()
	var collision_ids: Array[RID] = local.body_collision_rids()
	for i: int in range(30):
		world.sync_actors(roster,1,0.016)
	check(local.accessory_root.get_instance_id() == accessory_id, "unchanged appearance reuses accessory nodes")
	check(local.primary_tint.get_instance_id() == material_id, "unchanged appearance reuses materials")
	world.set_local_role(1,"human")
	check(not local.head.visible and local.local_view, "round first-person head masked")
	check(not local.accessory_root.is_visible_in_tree(), "own head accessory cannot occlude first-person view")
	world.set_local_role(1,"lobby")
	check(local.head.visible and local.accessory_root.is_visible_in_tree(), "return to lobby restores head and accessory")
	roster[1].appearance = {"color":5,"accessory":2}
	world.sync_actors(roster,1,0.016)
	check(local.primary_tint.get_instance_id() == material_id, "color change reuses existing clothing material")
	check(local.primary_tint.albedo_color == load("res://scripts/cosmetics.gd").PALETTE[5], "new color reaches visible clothing")
	check(local.body_collision_rids() == collision_ids, "cosmetics preserve body colliders")
	check((world.actors[2] as ActorView).applied_appearance.color == 1, "other actor cosmetic remains independent")
	local.apply_appearance({"color":999,"accessory":"invalid"})
	check(local.applied_appearance == {"color":0,"accessory":0}, "invalid appearance safely defaults")
	roster[1].role = "mosquito"
	roster[1].state = "flying"
	roster[1].appearance = {"color":4,"accessory":1}
	world.set_local_role(1,"mosquito")
	world.sync_actors(roster,1,1.0)
	check((world.actors[1] as ActorView).actor_role == "mosquito", "same peer changes visual role between lobby and round")
	check((world.actors[1] as ActorView).accessory_root.get_child_count() == 3, "mosquito bow geometry present")
	var original_camera: Transform3D = world.menu_camera.transform
	world.show_customization("human",{"color":3,"accessory":1})
	check(world.customization_actor.actor_role == "human" and world.customization_actor.head.visible, "human preview has full visible character")
	check(not (world.actors[2] as Node3D).visible, "live lobby actor hidden behind cosmetic preview")
	var preview_id: int = world.customization_actor.get_instance_id()
	world.show_customization("human",{"color":2,"accessory":2})
	check(world.customization_actor.get_instance_id() == preview_id, "preview selection reuses same role avatar")
	var all_disabled: bool = true
	for body: StaticBody3D in world.customization_actor.body_shapes:
		all_disabled = all_disabled and body.collision_layer == 0
	check(all_disabled, "cosmetic preview has no physical colliders")
	world.show_customization("mosquito",{"color":1,"accessory":2})
	check(world.customization_actor.actor_role == "mosquito" and world.customization_actor.scale.x == 3.5, "mosquito preview enlarged independently of playable actor")
	check(world.customization_actor.accessory_root.get_child_count() == 5, "mosquito goggles geometry present")
	world.end_customization()
	await process_frame
	await process_frame
	check(not is_instance_valid(world.customization_root) and not is_instance_valid(world.customization_actor), "preview root and actor cleared")
	check(world.menu_camera.transform.is_equal_approx(original_camera), "menu camera restored after preview")
	check((world.actors[2] as Node3D).visible, "existing lobby actors restored")
	world.clear_actors()
	if "--screens" in OS.get_cmdline_user_args():
		for fixture: Dictionary in [
			{"role":"human","color":3,"accessory":1,"name":"human-cap"},
			{"role":"human","color":2,"accessory":2,"name":"human-glasses"},
			{"role":"mosquito","color":1,"accessory":1,"name":"mosquito-bow"},
			{"role":"mosquito","color":4,"accessory":2,"name":"mosquito-goggles"},
		]:
			world.show_customization(fixture.role,{"color":fixture.color,"accessory":fixture.accessory})
			await create_timer(0.15).timeout
			await RenderingServer.frame_post_draw
			var path: String = "C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/custom-%s.png" % fixture.name
			var result: Error = root.get_texture().get_image().save_png(path)
			check(result == OK, "rendered preview %s" % fixture.name)
	world.end_customization()
	world.clear_actors()
	await process_frame
	await process_frame
	print("CUSTOM_VISUAL_RESULT failures=%d" % failures)
	quit(failures)
