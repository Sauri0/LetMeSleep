extends SceneTree
const Practice = preload("res://scripts/practice_session.gd")
func _initialize() -> void:
	var session := Practice.new()
	session.start("human","blood",{},"Quieto")
	for frame: int in range(55*60):
		session.advance(1.0/60)
		if frame % 60 == 0:
			for id: int in session.brains:
				var actor: Dictionary = session.sim.actors[id]
				var own: Dictionary = session.sim.private_for(id)
				print(JSON.stringify({"time":session.sim.elapsed,"id":id,"p":actor.p,"yaw":actor.yaw,"pitch":actor.pitch,"state":actor.state,"focus":own.get("focus",{}),"assignment":own.get("assignment",{}),"move":actor._move,"interact":actor._interact}))
	session.stop()
	session.free()
	quit()
