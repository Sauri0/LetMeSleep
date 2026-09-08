extends SceneTree
## CPU-only diagnostic. Per-tick work, not a framerate benchmark.
var destination := ""
var measurements := {"snapshot":[],"private":[],"bots":[],"input":[],"simulation":[]}
func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="): destination=arg.trim_prefix("--report=")
	_run.call_deferred()
func _run() -> void:
	var sim: RefCounted=load("res://scripts/simulation.gd").new()
	var roster := {}
	var brains := {}
	for id: int in range(1,17):
		roster[id]={"name":"Profile%d"%id,"role":"human" if id<=4 else "mosquito"}
		var brain: RefCounted=load("res://scripts/bot_brain.gd").new()
		brain.setup(id)
		brains[id]=brain
	sim.start(roster,{"human_count":4,"mode":"blood","round_seconds":180,"blood_goal":1000})
	for tick: int in range(840):
		var start:=Time.get_ticks_usec()
		var snapshot: Dictionary=sim.public_snapshot()
		var observed:=Time.get_ticks_usec()
		var priv_us:=0
		var brain_us:=0
		var input_us:=0
		for id: int in brains:
			var before:=Time.get_ticks_usec()
			var own: Dictionary=sim.private_for(id)
			var middle:=Time.get_ticks_usec()
			var intent: Dictionary=brains[id].decide(snapshot,own,1.0/60.0)
			var after:=Time.get_ticks_usec()
			sim.submit_input(id,tick+1,intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
			if not str(intent.action).is_empty(): sim.action(id,tick+1,str(intent.action),intent.yaw,intent.pitch)
			priv_us+=middle-before
			brain_us+=after-middle
			input_us+=Time.get_ticks_usec()-after
		var sim_start:=Time.get_ticks_usec()
		sim.step(1.0/60.0)
		if tick>=120:
			measurements.snapshot.append(float(observed-start)/1000)
			measurements.private.append(float(priv_us)/1000)
			measurements.bots.append(float(brain_us)/1000)
			measurements.input.append(float(input_us)/1000)
			measurements.simulation.append(float(Time.get_ticks_usec()-sim_start)/1000)
	var report: Dictionary={"version":ProjectSettings.get_setting("application/config/version"),"cpu":OS.get_processor_name(),"scenario":"16 authoritative actors+16 brains; 60Hz simulated CPU diagnostic; excludes render and networking","warmup_ticks":120,"sample_ticks":720,"milliseconds":{}}
	for label: String in measurements:
		var samples: Array=measurements[label]
		samples.sort()
		var total:=0.0
		for value: float in samples: total+=value
		report.milliseconds[label]={"mean":total/samples.size(),"p50":samples[int(samples.size()*.5)],"p90":samples[int(samples.size()*.9)],"p99":samples[int(samples.size()*.99)],"max":samples[-1]}
	print("PROFILE07 "+JSON.stringify(report))
	if not destination.is_empty():
		var file:=FileAccess.open(destination,FileAccess.WRITE)
		file.store_string(JSON.stringify(report,"\t"))
	quit()
