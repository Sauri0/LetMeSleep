extends SceneTree
## Synthetic level injection only. No microphone, audio playback, codec or
## network capture: this validates the local visual consumer, not full lipsync.
const Actor = preload("res://scripts/actor_view.gd")
const Envelope = preload("res://scripts/voice_mouth_envelope.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
var checks := 0
var failures := 0
var report_path := ""

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		print("VOICE09VISUAL FAIL "+label)

func weights(actor: ActorView) -> Dictionary:
	var result: Dictionary = {}
	for mesh: MeshInstance3D in actor.imported_skin.face_channels:
		if not mesh.visible: continue
		var values: Dictionary = {}
		for index: int in range(mesh.mesh.get_blend_shape_count()):
			values[str(mesh.mesh.get_blend_shape_name(index))]=mesh.get_blend_shape_value(index)
		result[str(mesh.name)]=values
	return result

func mouth_vertices(actor: ActorView) -> PackedVector3Array:
	var result := PackedVector3Array()
	for piece: MeshInstance3D in actor.imported_skin.meshes:
		if not piece.visible or not "_mouth_" in str(piece.name): continue
		var baked := piece.bake_mesh_from_current_blend_shape_mix()
		for surface: int in range(baked.get_surface_count()):
			for point: Vector3 in baked.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]: result.append(piece.global_transform*point)
	return result

func settle() -> void:
	for frame: int in range(2):
		await process_frame
		await RenderingServer.frame_post_draw

func _run() -> void:
	report_path=ProjectSettings.globalize_path("res://../work/voice09-visual.json")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): report_path=arg.trim_prefix("--output=")
	var envelope := Envelope.new()
	for invalid: float in [NAN,INF,-INF,-1.0]:
		envelope.set_level(invalid)
		check(envelope.advance(.02)==0.0,"invalid or negative input cannot open mouth")
	envelope.set_level(2.0)
	check(envelope.target==1.0,"input clamps to unity")
	var last := 0.0
	for frame: int in range(6):
		envelope.set_level(1.0)
		var level := envelope.advance(1.0/60.0)
		check(level>=last and level<=1.0,"attack is monotone and bounded")
		last=level
	check(last>.98,"real level opens within brief attack")
	envelope.set_level(0.0)
	for frame: int in range(12):
		var level := envelope.advance(1.0/60.0)
		check(level<=last,"silence closes without invented pulses")
		last=level
	check(last==0.0,"silence reaches exact zero")
	envelope.set_level(1.0)
	for frame: int in range(30): envelope.advance(1.0/60.0)
	check(envelope.value==0.0 and not envelope.active(),"stale provider cannot leave mouth open")
	var a := Envelope.new()
	var b := Envelope.new()
	for frame: int in range(6):
		a.set_level(.8); a.advance(1.0/60.0)
	for frame: int in range(12):
		b.set_level(.8); b.advance(1.0/120.0)
	check(absf(a.value-b.value)<.000001,"attack independent of 60/120Hz frame rate")
	for role: String in ["human","mosquito"]:
		var actor := Actor.new()
		root.add_child(actor)
		actor.build(role,"",0)
		actor.preview_only=true
		actor.set_process(false)
		for mouth: int in range(3):
			var data := {"p":Vector3.ZERO,"state":"human" if role=="human" else "flying","appearance":{"eyes":mouth,"mouth":mouth,"brows":mouth},"preview_only":true,"facial_preview":"neutral"}
			actor.clear_voice_level()
			actor.update_state(data,.1)
			await settle()
			var neutral_vertices := mouth_vertices(actor)
			var original := weights(actor)
			var original_base: Dictionary = actor.imported_skin.facial_values.duplicate(true)
			actor.set_voice_level(0.0)
			actor._process(.1)
			check(weights(actor)==original,"silent facial preview remains identical "+role+str(mouth))
			for frame: int in range(6):
				actor.set_voice_level(1.0)
				actor._process(1.0/60.0)
			var speaking := weights(actor)
			actor.set_process(false)
			await settle()
			var voiced_vertices := mouth_vertices(actor)
			var displacement := 0.0
			check(neutral_vertices.size()==voiced_vertices.size() and voiced_vertices.size()>0,"same real mouth topology under voice")
			for index: int in range(mini(neutral_vertices.size(),voiced_vertices.size())):
				displacement=maxf(displacement,neutral_vertices[index].distance_to(voiced_vertices[index]))
			check(displacement>.00015,"imported selected mouth vertices actually move "+role+str(mouth))
			check(actor.get_voice_level()>.98,"actor consumes local level "+role)
			check(actor.imported_skin.facial_values==original_base,"voice never contaminates base expression")
			for name: String in original:
				for channel: String in original[name]:
					if channel=="MouthOpen":
						check(float(speaking[name][channel])>.70 and float(speaking[name][channel])<=.72,"actual selectable mouth receives level")
					else:
						check(speaking[name][channel]==original[name][channel],"blink/brow/gaze/correctives unchanged")
			actor.clear_voice_level()
			check(actor.get_voice_level()==0.0 and weights(actor)==original,"mute restores exact original mouth immediately")
			var expressive := original_base.duplicate()
			expressive.MouthOpen=.85
			actor.imported_skin.facial_values=expressive
			actor.imported_skin.apply_facial_values(expressive)
			actor.set_voice_level(1.0)
			actor._process(.06)
			for mesh: MeshInstance3D in actor.imported_skin.face_channels:
				if not mesh.visible: continue
				var channel: int = actor.imported_skin.face_channels[mesh].get("MouthOpen",-1)
				if channel>=0: check(absf(mesh.get_blend_shape_value(channel)-.85)<.00001,"max composition preserves wider yawn/gesture")
			actor.clear_voice_level()
			actor.set_voice_level(.9)
			actor._process(.05)
			for frame: int in range(30): actor._process(1.0/60.0)
			check(actor.get_voice_level()==0.0,"watchdog closes without another actor snapshot")
			check(not actor.human_snapshot_values.has("voice_level") and not data.has("voice_level"),"local level is absent from snapshots")
		actor.set_voice_level(1.0)
		actor._process(.05)
		actor.update_state({"alive":false,"state":"dead"},.016)
		check(actor.get_voice_level()==0.0 and actor.imported_skin.voice_mouth_level==0.0,"dead actor clears local signal")
		actor.update_state({"alive":true},.016)
		actor.set_voice_level(1.0)
		actor._process(.05)
		root.remove_child(actor)
		check(actor.get_voice_level()==0.0 and actor.imported_skin.voice_mouth_level==0.0,"removal clears local signal")
		actor.set_voice_level(1.0)
		check(not actor.voice_envelope.active(),"late provider callback cannot reopen a removed actor")
		actor.free()
	var file := FileAccess.open(report_path,FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"input":"synthetic levels; no capture or playback","full_lipsync_validated":false},"\t"))
	print("VOICE09VISUAL %d/%d PASS"%[checks-failures,checks])
	quit(1 if failures else 0)
