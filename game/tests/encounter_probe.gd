extends SceneTree
const Practice = preload("res://scripts/practice_session.gd")
const Brain = preload("res://scripts/bot_brain.gd")
func _initialize() -> void:
	run.call_deferred()
func run() -> void:
	var reports := []
	for scenario: String in ["human_idle","human_defending","mosquito_pilot","mosquito_retreat","mosquito_timed_retreat"]:
		var practice := Practice.new()
		root.add_child(practice)
		practice.set_physics_process(false)
		practice.start("mosquito" if scenario.begins_with("mosquito") else "human","blood",{},"Probe")
		var pilot := Brain.new()
		pilot.setup(1)
		var report := {"scenario":scenario,"first_bite":-1.0,"first_focus":-1.0,"first_blood":-1.0,"first_hit":-1.0,"blood_at_10":0.0,"blood_at_30":0.0,"result_time":0.0,"winner":"","bites":0,"stuns":0,"recoveries":0,"player_death":-1.0,"events":[]}
		var previous := {}
		var action_seq := 0
		for frame: int in range(120*60+1):
			var snapshot: Dictionary = practice.sim.public_snapshot()
			if scenario != "human_idle":
				var intent: Dictionary = pilot.decide(snapshot,practice.sim.private_for(1),1.0/60)
				if scenario in ["mosquito_retreat","mosquito_timed_retreat"] and snapshot.actors[1].state == "biting" and pilot.attached_age > (3.25 if scenario == "mosquito_timed_retreat" else 2.75):
					intent.action = "bite"
					pilot.stats.detaches += 1
					pilot.retreat_left = 3.2
					pilot.retreat_burst_left = 0.9
					pilot.retreat_point = Brain.escape_point(snapshot.actors[1].p,Vector3(practice.sim.private_for(1).assignment.normal),"house")
				practice.send_input(frame,intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
				if not str(intent.action).is_empty():
					action_seq += 1
					practice.send_action(action_seq,intent.action,intent.yaw,intent.pitch)
			practice.advance(1.0/60)
			var now: Dictionary = practice.sim.public_snapshot()
			if now.blood > 0 and report.first_blood < 0: report.first_blood = practice.sim.elapsed
			for id: int in now.actors:
				var actor: Dictionary = now.actors[id]
				if actor.role == "mosquito" and practice.sim.private_for(id).get("focus",{}).get("state","") == "charging" and report.first_focus < 0:
					report.first_focus = practice.sim.elapsed
				if actor.get("state","") == "biting" and previous.get(id,"") != "biting":
					report.bites += 1
					report.events.append({"time":practice.sim.elapsed,"event":"bite","id":id,"blood":now.blood})
					if report.first_bite < 0: report.first_bite = practice.sim.elapsed
				if not actor.alive and previous.get(id,"") != "dead":
					if report.first_hit < 0: report.first_hit = practice.sim.elapsed
					if id == 1 and report.player_death < 0: report.player_death = practice.sim.elapsed
				if actor.state == "stunned" and previous.get(id,"") != "stunned":
					report.stuns += 1
					if report.first_hit < 0: report.first_hit = practice.sim.elapsed
					report.events.append({"time":practice.sim.elapsed,"event":"stun","id":id})
				if actor.state == "flying" and previous.get(id,"") == "stunned":
					report.recoveries += 1
					report.events.append({"time":practice.sim.elapsed,"event":"recover","id":id})
				previous[id] = actor.state
			if frame == 599: report.blood_at_10 = now.blood
			if frame == 1799: report.blood_at_30 = now.blood
			if now.phase != "playing":
				report.result_time = practice.sim.elapsed
				report.winner = now.winner
				break
			report["pilot_stats"] = pilot.stats
		reports.append(report)
		practice.stop()
		practice.queue_free()
		await process_frame
	print("ENCOUNTERS " + JSON.stringify(reports))
	var destination := "user://encounters05.json"
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="): destination = argument.substr(9)
	var file := FileAccess.open(destination,FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(reports,"\t"))
	var checks := [
		reports[0].first_bite >= 10.0,
		reports[0].winner == "mosquito" and reports[0].result_time > 30.0,
		reports[1].winner == "human",
		reports[2].first_hit > reports[2].first_bite + 2.0 and reports[2].stuns >= 1 and reports[2].recoveries >= 1 and reports[2].player_death < 0.0,
		reports[4].winner == "mosquito" and reports[4].bites >= 3 and reports[4].player_death < 0.0,
	]
	var failures := checks.count(false)
	print("ENCOUNTER_RESULT checks=%d failures=%d" % [checks.size(),failures])
	quit(failures)
