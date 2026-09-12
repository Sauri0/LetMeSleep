extends SceneTree
const Practice = preload("res://scripts/practice_session.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Brain = preload("res://scripts/bot_brain.gd")
var checks := 0
var failures := 0

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("FAIL: " + label)

func _initialize() -> void:
	var map_id: String = Maps.default_map_id()
	var original: Dictionary = Maps.get_map(map_id)
	_test_door_retreat(original,map_id)
	for role: String in ["human","mosquito"]:
		for mode: String in ["blood","survival","sleep"]:
			var session := Practice.new()
			session.start(role,mode,{},"Motion094",{"map_id":map_id,"round_seconds":120})
			var label := role+"/"+mode
			check(session.active and session.sim.phase == "playing", "practice starts " + label)
			if not session.active:
				printerr("PRACTICE_REASON " + str(session.sim.reason))
				session.free()
				continue
			check(session.sim.config.map_id == map_id and session.sim.config.mode == mode, "selected map and mode " + label)
			check(session.sim.actors[1].role == role, "selected role " + label)
			# Insects intentionally give beginners 6–9.6 seconds to orientate.
			for tick: int in range(240):
				session.advance(.05)
			var bot_moved := false
			for id: int in session.brains:
				bot_moved = bot_moved or int(session.brains[id].stats.moves) > 0
			check(bot_moved, "bots decide and move on authored map " + label)
			for round_index: int in range(3):
				session.restart()
				check(session.active and session.sim.elapsed == 0, "restart resets authority " + label)
				check(session.sim.config.map_id == map_id and session.selected_config.map_id == map_id, "restart preserves selected map " + label)
				check(Maps.get_map(map_id) == original, "restart leaves authored geometry identical " + label)
				check(session.sim.actors[1].role == role and session.sim.config.mode == mode, "restart preserves role/mode " + label)
				session.advance(.05)
			session.stop()
			check(not session.active and session.sim == null and session.brains.is_empty(), "stop clears local match " + label)
			session.free()
	var fallback := Practice.new()
	fallback.start("human","blood",{},"Motion094",{"map_id":"house-v3-obsolete"})
	check(fallback.active and fallback.selected_config.map_id == map_id, "obsolete local preference resolves to authored map")
	fallback.restart()
	check(fallback.active and fallback.sim.config.map_id == map_id, "resolved preference survives restart")
	fallback.free()
	print("MOTION094_PRACTICE checks=%d failures=%d" % [checks,failures])
	quit(0 if failures == 0 else 1)

func _test_door_retreat(data: Dictionary, map_id: String) -> void:
	for portal: Dictionary in data.portals:
		var destination := Vector3.ZERO
		if not bool(portal.get("exterior",false)):
			for room: Dictionary in data.rooms:
				if room.id == portal.room:
					destination = room.center
		var into_room := Vector3.ZERO
		into_room[int(portal.axis)] = signf(destination[int(portal.axis)]-Vector3(portal.p)[int(portal.axis)])
		var brain := Brain.new()
		brain.visible_doors = {str(portal.id):{"angle":.4,"target_angle":PI*.5,"blocked":true,"moving":true}}
		var me := {"p":Vector3(portal.p)+into_room*.85,"yaw":0.0,"pitch":0.0}
		var intent := {"move":Vector3.ZERO,"yaw":0.0,"pitch":0.0,"action":""}
		var handled: bool = brain._human_door(me,intent,-into_room,map_id)
		var world_move: Vector3 = Vector3(intent.move).rotated(Vector3.UP,float(intent.yaw))
		check(handled and world_move.dot(into_room)>.7, "bot frees blocked door toward room on authored axis " + str(portal.id))
