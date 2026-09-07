extends SceneTree
const Practice = preload("res://scripts/practice_session.gd")
const Brain = preload("res://scripts/bot_brain.gd")
func _initialize() -> void:
	var session := Practice.new()
	session.start("mosquito","blood",{},"Piloto")
	var pilot := Brain.new()
	pilot.setup(1)
	var action_seq := 0
	for frame: int in range(30*60):
		var snapshot: Dictionary = session.sim.public_snapshot()
		var intent: Dictionary = pilot.decide(snapshot,session.sim.private_for(1),1.0/60)
		session.send_input(frame,intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
		if not str(intent.action).is_empty():
			action_seq += 1
			session.send_action(action_seq,intent.action)
		session.advance(1.0/60)
		if frame % 12 == 0 and session.sim.elapsed > 16.0:
			var actor: Dictionary = session.sim.actors[1]
			var own: Dictionary = session.sim.private_for(1)
			print(JSON.stringify({"time":session.sim.elapsed,"p":actor.p,"yaw":actor.yaw,"pitch":actor.pitch,"focus":own.get("focus",{}),"assignment":own.get("assignment",{}),"interact":actor._interact,"human":snapshot.actors[101]}))
		if session.sim.phase != "playing": break
	session.stop()
	session.free()
	quit()
