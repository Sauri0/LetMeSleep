extends "res://tests/surface09_client_checks.gd"
## Native Main/Client/Practice. The fixture delays publication after real F so
## mouse/input can arrive between authority departure and the private snapshot.
const InsectPose = preload("res://scripts/mosquito_pose.gd")
var transitions: Array[Dictionary] = []
class DeliveredMouse extends Node:
	var deltas: Array[Vector2]=[]
	func _unhandled_input(event: InputEvent) -> void:
		if event is InputEventMouseMotion: deltas.append(event.relative)
var mouse_trace: DeliveredMouse

func _run() -> void:
	_backup()
	output=ProjectSettings.globalize_path("res://../outputs/0.9-surface-view-client")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): output=arg.trim_prefix("--output=")
	DirAccess.make_dir_recursive_absolute(output)
	Prefs.load_settings()
	Prefs.video_resolution=0
	Prefs.video_fullscreen=false
	Prefs.mosquito_sensitivity=.003
	Prefs.invert_y=false
	root.size=Vector2i(1280,720)
	root.content_scale_size=Vector2i(1280,720)
	_bind("perch",KEY_F)
	_bind("move_forward",KEY_W)
	app=load("res://scripts/main.gd").new()
	root.add_child(app)
	client=app.get_node("Client")
	ui=client.ui
	mouse_trace=DeliveredMouse.new()
	root.add_child(mouse_trace)
	client.practice_active=true
	client.local_id=1
	ui.set_practice(true)
	client.practice.start("mosquito","blood",Prefs.cosmetics,"View transition",{"map_id":"house","round_seconds":180,"blood_goal":1000})
	client.practice.brains.clear()
	await _settle_frames(8)
	_check(client.playing and client.role=="mosquito" and client.world.current_map=="house" and client.practice.sim.config.map_id=="house","Main Client and practice share authored physical house")
	var cases: Array[Dictionary]=[
		{"name":"floor","p":Vector3(0,.16,6.5),"n":Vector3.UP},
		{"name":"ceiling","p":Vector3(0,2.84,6.5),"n":Vector3.DOWN},
		{"name":"west","p":Vector3(-1.74,1.6,2.4),"n":Vector3.RIGHT},
		{"name":"east","p":Vector3(1.74,1.6,2.4),"n":Vector3.LEFT},
		{"name":"north","p":Vector3(-7,1.8,5.54),"n":Vector3.FORWARD},
		{"name":"south","p":Vector3(-7,1.8,6.06),"n":Vector3.BACK},
		{"name":"vertical-up","p":Vector3(-1.74,1.6,2.4),"n":Vector3.RIGHT,"f":Vector3.UP},
		{"name":"vertical-down","p":Vector3(-1.74,1.6,2.4),"n":Vector3.RIGHT,"f":Vector3.DOWN},
	]
	for test: Dictionary in cases: await _transition_case(test)
	_check(is_equal_approx(InsectPose.VIEW_PITCH_LIMIT,PI*.5) and is_equal_approx(InsectPose.SURFACE_PITCH_LIMIT,1.35),"flight and supported pitch limits match public contract")
	report={"transitions":transitions,"map_id":"house","real_f_input":true,"publication_delayed_by_fixture":true,"microphone_used":false}
	await _finish()

func _transition_case(test: Dictionary) -> void:
	client.set_physics_process(true)
	client.practice.set_physics_process(true)
	await _reset_at(test.p)
	var sim: RefCounted=client.practice.sim
	var actor: Dictionary=sim.actors[1]
	_check(Vector3(actor._surface_normal).is_equal_approx(test.n),"actual support normal "+test.name)
	# A prepared tangent heading tests exactly vertical departure on a real
	# wall. It does not replace adhesion, a camera basis, F or the ACK handler.
	if test.has("f"):
		actor._surface_forward=test.f
		actor._surface_view_yaw=actor.yaw
		actor.pitch=0.0
		client.pitch=0.0
		client.yaw=float(actor.yaw)
		client._mosquito_input_view=Vector2(client.yaw,0.0)
		client.practice._publish()
	await _settle_frames(12)
	client.set_physics_process(false)
	client.practice.set_physics_process(false)
	if not test.has("f"):
		_motion(Vector2(0,-100000))
		await process_frame
		_check(is_equal_approx(client.pitch,1.35),"real mouse clamps supported view upward "+test.name)
		_motion(Vector2(0,100000))
		await process_frame
		_check(is_equal_approx(client.pitch,-1.35),"real mouse clamps supported view downward "+test.name)
		# Return to a moderate local pitch by another real mouse event.
		_motion(Vector2(0,-(1.35+.21)/Prefs.mosquito_sensitivity))
		await process_frame
	client._physics_process(.04)
	sim.step(1.0/60.0)
	client.practice._publish()
	await _settle_frames(2)
	var aimed_actor: Dictionary=actor.duplicate(true)
	aimed_actor.yaw=client.yaw; aimed_actor.pitch=client.pitch
	# Idle orbit is independent; F departs toward the current camera aim.
	var departure_direction: Vector3=sim._insect_view_direction(aimed_actor)
	await _tap("perch")
	sim.step(1.0/60.0)
	var pending: Dictionary=sim.private_for(1)
	var tr: Dictionary=pending.surface.view_transition.duplicate(true)
	_check(actor.state=="flying" and not tr.is_empty() and not bool(tr.get("acknowledged",true)),"real F creates pending private rebase "+test.name)
	if tr.is_empty(): return
	_check(ArenaData.flight_direction(Vector3.FORWARD,actor.yaw,actor.pitch).distance_to(departure_direction)<.00001,"departure preserves surface aim before delivery "+test.name)
	var extra_yaw:=0.0
	var extra_pitch:=0.0
	if not test.has("f"):
		mouse_trace.deltas.clear()
		_motion(Vector2(17,-11))
		await process_frame
		# This input still uses the old coordinate frame and reaches authority
		# before its ACK; the authority must hold just its orientation.
		client._physics_process(.04)
		_motion(Vector2(-7,4))
		await process_frame
		extra_yaw=wrapf(float(client.yaw)-float(tr.source_yaw),-PI,PI)
		extra_pitch=float(client.pitch)-float(tr.source_pitch)
		# Window stretch transforms injected device-space relative coordinates.
		# Compare against the two events actually delivered to this viewport,
		# observed independently from Client's accumulated orientation.
		var delivered:=Vector2.ZERO
		for delta: Vector2 in mouse_trace.deltas: delivered+=delta
		_check(mouse_trace.deltas.size()==2 and absf(extra_yaw+delivered.x*Prefs.mosquito_sensitivity)<.00001 and absf(extra_pitch+delivered.y*Prefs.mosquito_sensitivity)<.00001,"both delivered mouse deltas remain in local view "+test.name)
		_check(ArenaData.flight_direction(Vector3.FORWARD,actor.yaw,actor.pitch).distance_to(departure_direction)<.00001,"old-frame input cannot overwrite departure aim "+test.name)
	var expected_yaw: float=wrapf(float(tr.yaw)+extra_yaw,-PI,PI)
	var expected_pitch: float=clampf(float(tr.pitch)+extra_pitch,-PI*.5,PI*.5)
	var expected_sequence: int=maxi(client.sequence,int(tr.input_seq))+1
	var previous_action: int=client.action_sequence
	client.practice._publish()
	_check(client._view_revision==int(tr.revision) and client.action_sequence==previous_action+1,"Client applies revision once and sends exactly one ACK "+test.name)
	_check(bool(actor._view_transition.acknowledged) and int(actor._view_input_floor)==expected_sequence,"Practice routes ACK to authority with exact next-input barrier "+test.name)
	_check(absf(wrapf(client.yaw-expected_yaw,-PI,PI))<.00001 and absf(client.pitch-expected_pitch)<.00001,"Client preserves pending mouse delta during rebase "+test.name)
	_check(absf(wrapf(actor.yaw-client.yaw,-PI,PI))<.00001 and absf(actor.pitch-client.pitch)<.00001,"ACK installs same orientation atomically on authority "+test.name)
	var after_action: int=client.action_sequence
	client._private(pending)
	client._private(sim.private_for(1))
	_check(client.action_sequence==after_action and absf(wrapf(client.yaw-expected_yaw,-PI,PI))<.00001 and absf(client.pitch-expected_pitch)<.00001,"duplicate pending/acknowledged snapshots never recenter or resend ACK "+test.name)
	client.practice.send_input(int(tr.input_seq),Vector3.RIGHT,float(tr.source_yaw),float(tr.source_pitch),false)
	_check(absf(wrapf(actor.yaw-expected_yaw,-PI,PI))<.00001 and absf(actor.pitch-expected_pitch)<.00001,"old input after ACK cannot restore old coordinates "+test.name)
	_action("move_forward",true)
	await process_frame
	client._physics_process(.04)
	_action("move_forward",false)
	_check(int(actor._input_seq)==expected_sequence and actor._move==Vector3.FORWARD,"first real Client input uses the advertised sequence and W "+test.name)
	var expected_direction: Vector3=ArenaData.flight_direction(Vector3.FORWARD,expected_yaw,expected_pitch)
	_check(ArenaData.flight_direction(Vector3.FORWARD,actor.yaw,actor.pitch).distance_to(expected_direction)<.00001,"first rebased W input uses corrected flight direction "+test.name)
	if test.has("f"):
		_check(absf(client.pitch-float(Vector3(test.f).y)*PI*.5)<.00001,"exact vertical departure is not clipped "+test.name)
		client.practice._publish()
		await _settle_frames(20)
		_check((-client.camera.global_basis.z).dot(test.f)>.99,"native camera settles on exact vertical view "+test.name)
		await _capture(test.name)
	else:
		_motion(Vector2(0,-100000)); await process_frame
		_check(is_equal_approx(client.pitch,PI*.5),"real mouse reaches upward vertical in flight "+test.name)
		_motion(Vector2(0,100000)); await process_frame
		_check(is_equal_approx(client.pitch,-PI*.5),"real mouse reaches downward vertical in flight "+test.name)
	transitions.append({"face":test.name,"normal":str(test.n),"revision":tr.revision,"source_yaw":tr.source_yaw,"source_pitch":tr.source_pitch,"pending_mouse_yaw":extra_yaw,"pending_mouse_pitch":extra_pitch,"rebased_yaw":expected_yaw,"rebased_pitch":expected_pitch,"first_input_seq":expected_sequence,"authority_input_seq":actor._input_seq})

func _finish() -> void:
	finishing=true
	for action: String in held:
		if held[action]: _action(action,false)
	if is_instance_valid(app): app.queue_free()
	if is_instance_valid(mouse_trace): mouse_trace.queue_free()
	await _settle()
	_restore()
	var restored: bool=FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH)
	_check(restored,"original preferences restored byte for byte")
	report.merge({"checks":checks,"failures":failures,"preferences_modified":not restored,"real_main_client_practice":true},true)
	var file:=FileAccess.open(output.path_join("checks.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t")); file.close()
	print("SURFACE_VIEW09_CLIENT_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
