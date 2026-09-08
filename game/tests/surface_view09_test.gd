extends SceneTree
const Sim=preload("res://scripts/simulation.gd")
const ArenaData=preload("res://scripts/arena.gd")
const Motion=preload("res://scripts/surface_locomotion.gd")
const Pose=preload("res://scripts/mosquito_pose.gd")
const Audit=preload("res://tests/network_privacy_audit.gd")
const DT:=1.0/60.0
var checks:=0
var failures:=0

func check(ok: bool,label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		if failures<=24: printerr("SURFACE_VIEW09_FAIL "+label)

func _initialize() -> void:
	_six_faces()
	_corner_departure()
	_channel_orders()
	_focus_and_help()
	_privacy_and_reset()
	print("SURFACE_VIEW09_RESULT checks=%d failures=%d"%[checks,failures])
	quit(0 if failures==0 else 1)

func fixture(insects: int=1) -> RefCounted:
	var sim=Sim.new()
	var roster: Dictionary={1:{"role":"human"},2:{"role":"mosquito"}}
	if insects==2: roster[3]={"role":"mosquito"}
	sim.start(roster,{"mode":"blood","blood_goal":1000,"round_seconds":180})
	# Finite test arena, immutable identity. Movement and support authority are
	# production code; no mesh, camera or client is mocked as proof of rendering.
	var id: String="surface-view09-empty"
	ArenaData._map_cache[id]={"half_x":5.0,"half_z":5.0,"ceiling":4.0,"obstacles":[]}
	sim.config.map_id=id
	sim.doors.clear()
	sim.surface_motion.configure(id)
	sim.actors[1].p=Vector3.ZERO
	return sim

func land(sim: RefCounted, point: Vector3, normal: Vector3, forward: Vector3, yaw: float=.73,pitch: float=.21) -> void:
	var actor: Dictionary=sim.actors[2]
	actor.p=point
	actor.velocity=Vector3.ZERO
	sim.submit_input(2,int(actor._input_seq)+1,Vector3.ZERO,yaw,pitch,false)
	sim.action(2,int(actor._action_seq)+1,"perch",yaw,pitch)
	for tick: int in range(30):
		sim.step(DT)
		if actor.state=="perched": break
	check(actor.state=="perched" and Vector3(actor._surface_normal).is_equal_approx(normal),"actual F approach attaches on requested finite fixture face")
	# Select a tangent heading to exercise every public frame independently of
	# initial projection; the floor/wall/ceiling chain below obtains it by walking.
	actor._surface_forward=forward.normalized()
	actor._surface_view_yaw=actor.yaw

func direction(actor: Dictionary) -> Vector3:
	return ArenaData.flight_direction(Vector3.FORWARD,float(actor.yaw),float(actor.pitch))

func release(sim: RefCounted) -> Dictionary:
	var actor: Dictionary=sim.actors[2]
	var expected: Vector3=sim._insect_view_direction(actor)
	sim.action(2,int(actor._action_seq)+1,"perch",actor.yaw,actor.pitch)
	sim.step(DT)
	var transition: Dictionary=sim.private_for(2).surface.view_transition
	check(actor.state=="flying" and not transition.is_empty() and not transition.acknowledged,"real F departure creates a pending private revision")
	check(direction(actor).distance_to(expected)<.00001,"flight forward exactly preserves departing camera direction")
	return transition

func _six_faces() -> void:
	var faces: Array[Dictionary]=[
		{"p":Vector3(3,.16,3),"n":Vector3.UP,"f":Vector3.FORWARD},
		{"p":Vector3(3,3.84,3),"n":Vector3.DOWN,"f":Vector3.RIGHT},
		{"p":Vector3(-4.84,2,3),"n":Vector3.RIGHT,"f":Vector3.UP},
		{"p":Vector3(4.84,2,3),"n":Vector3.LEFT,"f":Vector3.DOWN},
		{"p":Vector3(3,2,-4.84),"n":Vector3.BACK,"f":Vector3.UP},
		{"p":Vector3(3,2,4.84),"n":Vector3.FORWARD,"f":Vector3.RIGHT},
	]
	for face: Dictionary in faces:
		for pitch: float in [-1.2,0.0,.8]:
			var sim=fixture()
			land(sim,face.p,face.n,face.f,.73,pitch)
			var actor: Dictionary=sim.actors[2]
			# New mouse input can precede the walking tick. The same unconsumed
			# local yaw delta must be represented by departure and visible camera.
			sim.submit_input(2,int(actor._input_seq)+1,Vector3.ZERO,.83,pitch,false)
			var expected: Vector3=Pose.surface_view_direction(face.n,Vector3(face.f).rotated(face.n,.10),pitch)
			check(sim._insect_view_direction(actor).distance_to(expected)<.00001,"unconsumed yaw delta uses surface normal, including ceiling")
			var transition: Dictionary=release(sim)
			check(direction(actor).distance_to(expected)<.00001 and is_equal_approx(float(transition.source_yaw),.83),"surface view converts once without world-yaw fallback")
			var seq: int=actor._input_seq+1
			sim.submit_input(2,seq,Vector3.FORWARD,-2.0,-.9,false)
			check(direction(actor).distance_to(expected)<.00001 and actor._move==Vector3.FORWARD and actor._last_input==sim.elapsed,"pre-ACK input updates movement/freshness while its old orientation is blocked")
			check(sim.public_snapshot().actors[2].surface_normal==Vector3.ZERO and sim.public_snapshot().actors[2].surface_forward==Vector3.ZERO,"departed public frame contains no stale adhesion normal")
	# Exact vertical heading must not be clipped to the former 1.48-radian aim.
	var vertical=fixture();land(vertical,Vector3(-4.84,2,3),Vector3.RIGHT,Vector3.UP,0,0)
	release(vertical)
	check(direction(vertical.actors[2]).distance_to(Vector3.UP)<.000001 and absf(vertical.actors[2].pitch-PI*.5)<.000001,"vertical wall walk becomes exactly vertical W flight")

func _corner_departure() -> void:
	for dt: float in [DT,.05]:
		var sim=fixture()
		land(sim,Vector3(-4.6,.16,3),Vector3.UP,Vector3.LEFT,PI*.5,0)
		var actor: Dictionary=sim.actors[2]
		var saw_wall:=false
		for tick: int in range(int(ceil(8.0/dt))):
			sim.submit_input(2,int(actor._input_seq)+1,Vector3.FORWARD,PI*.5,0,false)
			sim.step(dt)
			saw_wall=saw_wall or Vector3(actor._surface_normal)==Vector3.RIGHT
		check(saw_wall and actor.state=="perched" and actor._surface_normal==Vector3.DOWN and Vector3(actor._surface_forward).x>.99,"real held W transports floor-wall-ceiling heading")
		release(sim)
		var start: Vector3=actor.p
		for tick: int in range(int(ceil(.35/dt))):
			sim.submit_input(2,int(actor._input_seq)+1,Vector3.FORWARD,PI*.5,0,false)
			sim.step(dt)
			check(ArenaData.can_fit_mosquito(actor.p,sim.config.map_id),"post-departure travel still collides with real bounds")
		check(Vector3(actor.p).x>start.x+.65 and direction(actor).x>.99,"W advances in transported ceiling direction despite old-angle packets")
		check(not sim.private_for(2).surface.view_transition.acknowledged,"transition persists beyond several public/input ticks until explicit ACK")
	var jump=fixture();land(jump,Vector3(-4.84,2,3),Vector3.RIGHT,Vector3.UP,0,0)
	jump.submit_input(2,10,Vector3.UP,0,0,false);jump.step(DT)
	check(jump.actors[2].state=="flying" and direction(jump.actors[2]).y>.999 and not jump.private_for(2).surface.view_transition.is_empty(),"Space edge uses the same authoritative rebase as F")

func _channel_orders() -> void:
	for input_first: bool in [true,false]:
		var sim=fixture();land(sim,Vector3(3,3.84,3),Vector3.DOWN,Vector3.RIGHT,.7,.2)
		var tr: Dictionary=release(sim)
		var actor: Dictionary=sim.actors[2]
		var first_seq: int=int(tr.input_seq)+5
		var pending_yaw: float=wrapf(float(tr.source_yaw)+.18,-PI,PI)
		var pending_pitch: float=float(tr.source_pitch)-.11
		var rebased_yaw: float=wrapf(float(tr.yaw)+wrapf(pending_yaw-float(tr.source_yaw),-PI,PI),-PI,PI)
		var rebased_pitch: float=clampf(float(tr.pitch)+pending_pitch-float(tr.source_pitch),-Pose.VIEW_PITCH_LIMIT,Pose.VIEW_PITCH_LIMIT)
		check(not sim.submit_view_ack(1,1,tr.revision,first_seq,0,0) and not sim.submit_view_ack(2,actor._action_seq,tr.revision,first_seq,0,0),"human and stale-action ACK rejected")
		check(not sim.submit_view_ack(2,actor._action_seq+1,int(tr.revision)+1,first_seq,0,0) and not sim.submit_view_ack(2,actor._action_seq+1,tr.revision,tr.input_seq,0,0),"wrong revision and pre-transition barrier rejected")
		check(not sim.submit_view_ack(2,actor._action_seq+1,tr.revision,first_seq,NAN,0),"nonfinite ACK cannot unlock orientation")
		sim.submit_input(2,first_seq-1,Vector3.RIGHT,-2.8,.7,false)
		if input_first: sim.submit_input(2,first_seq+1,Vector3.FORWARD,rebased_yaw+.07,rebased_pitch+.03,false)
		check(sim.submit_view_ack(2,actor._action_seq+1,tr.revision,first_seq,rebased_yaw,rebased_pitch),"valid current ACK accepted in either channel order")
		if not input_first:
			sim.submit_input(2,first_seq,Vector3.FORWARD,rebased_yaw,rebased_pitch,false)
			sim.submit_input(2,first_seq+1,Vector3.FORWARD,rebased_yaw+.07,rebased_pitch+.03,false)
		check(absf(wrapf(actor.yaw-(rebased_yaw+.07),-PI,PI))<.000001 and absf(actor.pitch-(rebased_pitch+.03))<.000001,"pending mouse delta and latest rebased input survive both channel orders")
		var accepted_yaw: float=actor.yaw
		sim.submit_input(2,first_seq-2,Vector3.ZERO,0,0,false)
		check(actor.yaw==accepted_yaw and actor._move==Vector3.FORWARD,"reordered input cannot revert movement or orientation after ACK")
		check(not sim.submit_view_ack(2,actor._action_seq+1,tr.revision,first_seq,0,0) and actor.yaw==accepted_yaw,"duplicate ACK cannot recenter the view")
		check(sim.private_for(2).surface.view_transition.acknowledged,"acknowledged revision remains available for idempotent snapshots")
	# ACK can overtake old packets which the input sequencer has never seen.
	var overtaken=fixture();land(overtaken,Vector3(3,3.84,3),Vector3.DOWN,Vector3.RIGHT)
	var tr: Dictionary=release(overtaken)
	var actor: Dictionary=overtaken.actors[2]
	var floor_seq: int=int(tr.input_seq)+10
	check(overtaken.submit_view_ack(2,actor._action_seq+1,tr.revision,floor_seq,tr.yaw,tr.pitch),"ACK installs a future first-input barrier")
	overtaken.submit_input(2,floor_seq-1,Vector3.RIGHT,-2.0,.8,false)
	check(actor.yaw==tr.yaw and actor.pitch==tr.pitch and actor._move==Vector3.RIGHT,"unseen old packet below barrier keeps movement but cannot overwrite rebased view")
	var seq: int=actor._action_seq
	overtaken.action(2,seq,"perch",0,0)
	check(overtaken._pending_actions.is_empty(),"ACK shares the action sequence with F, without replaying an old perch")

func _focus_and_help() -> void:
	var sim=fixture()
	sim.actors[1].p=Vector3(2,0,-4)
	sim.actors[2]._assignment={"human":1,"zone":0,"revision":1}
	var mark: Dictionary=sim._zone_pose(sim.actors[2]._assignment)
	land(sim,Vector3(mark.p.x,float(mark.p.y)-.55,-4.84),Vector3.BACK,Vector3.UP,0,0)
	var actor: Dictionary=sim.actors[2]
	var delta: Vector3=Vector3(sim.private_for(2).assignment.p)-Vector3(actor.p)
	actor._surface_forward=delta.slide(Vector3.BACK).normalized()
	var local_pitch:=atan2(delta.dot(Vector3.BACK),delta.slide(Vector3.BACK).length())
	sim.submit_input(2,int(actor._input_seq)+1,Vector3.ZERO,0,local_pitch,true)
	check(direction(actor).dot(delta.normalized())<.4 and sim.private_for(2).focus.can_focus,"wall focus uses visible surface aim, not the obsolete world-yaw ray")
	var schedule: float=actor._next_rotation
	var started: float=sim.elapsed
	for tick: int in range(210):
		sim.step(DT)
		var tr: Dictionary=sim.private_for(2).surface.view_transition
		if not tr.is_empty() and not tr.acknowledged: sim.submit_view_ack(2,actor._action_seq+1,tr.revision,actor._input_seq+1,tr.yaw,tr.pitch)
		var point: Vector3=sim.private_for(2).assignment.p
		var target: Vector3=(point-Vector3(actor.p)).normalized()
		sim.submit_input(2,int(actor._input_seq)+1,Vector3.ZERO,atan2(-target.x,-target.z),asin(clampf(target.y,-1,1)),true)
		if actor.state=="biting": break
	check(actor.state=="biting" and sim.elapsed-started>=Sim.FOCUS_SECONDS-.0001,"wall E departs and completes actual timed focus after rebasing")
	check(actor._next_rotation==schedule,"view transition never resets private bite calendar")
	var help=fixture(2);land(help,Vector3(3,1,-4.84),Vector3.BACK,Vector3.UP,0,PI*.25)
	help.actors[3].p=Vector3(help.actors[2].p)+Vector3(0,.3,.3)
	help._kill(3)
	help.submit_input(2,10,Vector3.ZERO,0,PI*.25,true)
	check(help.private_for(2).help.target==3,"wall helper shares the camera-facing direction")
	help.step(.025)
	check(help.actors[2].state=="perched" and help.actors[2].help_target==3 and absf(help.actors[3]._stun_remaining-34.9)<.00001,"wall rescue remains supported and preserves total 4x rate")

func _privacy_and_reset() -> void:
	var sim=fixture();land(sim,Vector3(3,3.84,3),Vector3.DOWN,Vector3.RIGHT)
	release(sim)
	var own: Dictionary=sim.private_for(2);own.tick=sim._frame
	var snapshot: Dictionary=sim.public_snapshot();snapshot.tick=sim._frame
	var audit=Audit.new();audit.record_private(own);audit.record_public(snapshot,2)
	check(audit.ok() and not snapshot.actors[2].has("view_transition") and not snapshot.actors[2].has("_view_transition") and not sim.private_for(1).has("surface"),"view revision and channel barrier stay only in the insect's private data")
	var saved: Dictionary=own.surface.view_transition.duplicate(true)
	sim.step(.1)
	check(sim.private_for(2).surface.view_transition==saved,"private transition is persistent rather than a lossy one-frame event")
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{})
	check(sim.private_for(2).surface.view_transition.is_empty() and sim.actors[2]._view_revision==0 and sim.actors[2]._view_input_floor==-1,"new round discards old revisions, buffers and barriers")
