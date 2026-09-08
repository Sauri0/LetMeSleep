extends SceneTree
const Sim = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Surface = preload("res://scripts/surface_locomotion.gd")
const Insect = preload("res://scripts/mosquito_pose.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const Privacy = preload("res://tests/network_privacy_audit.gd")
var checks := 0
var failures := 0
var fixture_serial := 0

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		if failures<=30: printerr("SURFACE09_FAIL "+label)

func _initialize() -> void:
	_six_faces()
	_corners()
	_seam_and_isolation()
	_authority()
	_help_and_focus()
	_doors()
	print("SURFACE09_TEST_RESULT checks=%d failures=%d"%[checks,failures])
	quit(0 if failures==0 else 1)

func fixture(boxes: Array[AABB] = []) -> RefCounted:
	fixture_serial += 1
	var id := "surface09-fixture-"+str(fixture_serial)
	# Test-only immutable map fixtures have distinct IDs, matching seed identity.
	ArenaData._map_cache[id] = {"half_x":5.0,"half_z":5.0,"ceiling":4.0,"obstacles":boxes}
	var helper = Surface.new()
	helper.configure(id)
	return helper

func actor_at(position: Vector3, yaw: float=0.0, pitch: float=0.0) -> Dictionary:
	return {"p":position,"yaw":yaw,"pitch":pitch,"velocity":Vector3.ZERO,"state":"flying","motion_phase":0.0,"motion_speed":0.0,"grounded":false}

func land(helper: RefCounted, actor: Dictionary, dt: float=1.0/60.0) -> void:
	var start: Vector3 = actor.p
	check(helper.begin(actor,{}),"reachable support acquired")
	check(actor.p==start and actor.state=="flying","F acquisition never teleports the centre")
	for tick: int in range(int(ceil(.5/dt))):
		if actor.state=="perched": break
		var previous: Vector3 = actor.p
		helper.approach(actor,dt,{})
		check(previous.distance_to(actor.p)<=Surface.APPROACH_SPEED*dt+.00001,"approach has bounded real travel")
	check(actor.state=="perched" and helper.supported(actor,{}),"physical approach establishes support")

func audit_tick(helper: RefCounted, actor: Dictionary, previous: Vector3, dt: float) -> void:
	check(actor.state=="perched" and helper.supported(actor,{}),"support survives every walking tick")
	check(ArenaData.can_fit_mosquito(actor.p,helper.map_id),"walking body stays outside all architecture")
	check(previous.distance_to(actor.p)<=Surface.SPEED*dt+.00001,"walking cannot teleport or exceed speed")
	var n: Vector3 = actor._surface_normal
	var f: Vector3 = actor._surface_forward
	check(n.is_finite() and f.is_finite() and absf(n.length()-1.0)<.00001 and absf(f.length()-1.0)<.00001 and absf(n.dot(f))<.00001,"public frame is finite orthonormal")
	var public := {"state":"perched","surface_normal":n,"surface_forward":f,"p":actor.p}
	check(Insect.orientation(public).y.is_equal_approx(n) and (-Insect.orientation(public).z).is_equal_approx(f),"impact and renderer consume the same transported orientation")

func _six_faces() -> void:
	var helper = fixture()
	var faces := [
		{"p":Vector3(3,.16,3),"n":Vector3.UP}, {"p":Vector3(3,3.84,3),"n":Vector3.DOWN},
		{"p":Vector3(-4.84,2,3),"n":Vector3.RIGHT}, {"p":Vector3(4.84,2,3),"n":Vector3.LEFT},
		{"p":Vector3(3,2,-4.84),"n":Vector3.BACK}, {"p":Vector3(3,2,4.84),"n":Vector3.FORWARD},
	]
	for face: Dictionary in faces:
		var distances: Array[float] = []
		for dt: float in [1.0/60.0,.05]:
			var actor := actor_at(face.p)
			land(helper,actor,dt)
			check(Vector3(actor._surface_normal).is_equal_approx(face.n),"correct exterior normal on each of six faces")
			var start: Vector3 = actor.p
			for tick: int in range(int(round(1.0/dt))):
				var previous: Vector3 = actor.p
				helper.step_surface(actor,Vector3.FORWARD,dt,{})
				audit_tick(helper,actor,previous,dt)
			check(absf((Vector3(actor.p)-start).dot(face.n))<.00001,"plane walking has no normal drift")
			check(start.distance_to(actor.p)>.58,"W travels a useful distance while staying attached")
			distances.append(start.distance_to(actor.p))
			for tick: int in range(int(ceil(.2/dt))): helper.step_surface(actor,Vector3.ZERO,dt,{})
			var stopped: Vector3 = actor.p
			helper.step_surface(actor,Vector3.ZERO,.05,{})
			check(actor.p==stopped and actor.velocity==Vector3.ZERO and actor.state=="perched","release movement stops and retains adhesion")
			check(helper.release(actor,{}),"F has an unobstructed departure on each face")
			check(actor.state=="flying" and actor._surface_normal==Vector3.ZERO and actor._surface_forward==Vector3.ZERO and Dictionary(actor._surface).is_empty(),"release clears support and visible contact frame")
		check(absf(distances[0]-distances[1])<.025,"20 and 60 Hz travel agree within integration bound")
	var air := actor_at(Vector3(0,2,0))
	check(not helper.begin(air,{}) and air.state=="flying","F cannot freeze free air")

func _corners() -> void:
	var helper = fixture()
	for dt: float in [1.0/60.0,.05]:
		var actor := actor_at(Vector3(-4.6,.16,3),PI*.5)
		land(helper,actor,dt)
		var seen: Dictionary = {}
		for tick: int in range(int(ceil(8.0/dt))):
			var previous: Vector3 = actor.p
			helper.step_surface(actor,Vector3.FORWARD,dt,{})
			audit_tick(helper,actor,previous,dt)
			seen[actor._surface_normal] = true
		check(seen.has(Vector3.UP) and seen.has(Vector3.RIGHT) and seen.has(Vector3.DOWN),"held W traverses floor to wall to ceiling")
		check(actor._surface_normal==Vector3.DOWN and Vector3(actor._surface_forward).x>.99,"ceiling transition transports heading without reversing W")
	var box_helper = fixture([AABB(Vector3(-1,1,-1),Vector3(2,.5,2))])
	var climber := actor_at(Vector3(.8,1.68,0),-PI*.5)
	land(box_helper,climber)
	var left_top := false
	for tick: int in range(55):
		var previous: Vector3 = climber.p
		box_helper.step_surface(climber,Vector3.FORWARD,1.0/60.0,{})
		audit_tick(box_helper,climber,previous,1.0/60.0)
		left_top = left_top or climber._surface_normal==Vector3.RIGHT
	check(left_top and float(climber.p.y)<1.5,"outside table edge turns down its real side without entering the box")

func _seam_and_isolation() -> void:
	var tiled = fixture([AABB(Vector3(-2,1,-1),Vector3(2,.2,2)),AABB(Vector3(0,1,-1),Vector3(2,.2,2))])
	var actor := actor_at(Vector3(-.3,1.4,0),-PI*.5)
	land(tiled,actor)
	for tick: int in range(90):
		var previous: Vector3 = actor.p
		tiled.step_surface(actor,Vector3.FORWARD,1.0/60.0,{})
		audit_tick(tiled,actor,previous,1.0/60.0)
		check(actor._surface_normal==Vector3.UP,"coplanar module seam does not rotate into an internal face")
	check(float(actor.p.x)>.5,"walk crosses the coplanar seam")
	var empty = fixture()
	check(not empty.supported(actor,{}),"support identity from a different seed is rejected")
	var original: Vector3 = actor.p
	empty.step_surface(actor,Vector3.FORWARD,.05,{})
	check(actor.state=="flying" and actor.p==original,"stale support is released in place without teleporting to another map")

func make_sim(mode: String="blood", insects: int=1) -> RefCounted:
	var sim = Sim.new()
	var roster := {1:{"role":"human"}}
	for id: int in range(2,2+insects): roster[id] = {"role":"mosquito"}
	sim.start(roster,{"mode":mode,"blood_goal":1000,"round_seconds":180,"rotation_seconds":40})
	sim.actors[1].p = Vector3(-10,0,7)
	return sim

func sim_land(sim: RefCounted, id: int, position: Vector3) -> void:
	sim.actors[id].p = position
	sim.actors[id].velocity = Vector3.ZERO
	sim.action(id,int(sim.actors[id]._action_seq)+1,"perch")
	for tick: int in range(24): sim.step(1.0/60.0)
	check(sim.actors[id].state=="perched","real action and simulation complete surface approach")

func _authority() -> void:
	var sim = make_sim()
	sim.submit_input(2,1,Vector3.UP,0,0,false)
	sim_land(sim,2,Vector3(-7,.16,8))
	check(sim.actors[2].state=="perched","Space held before landing does not auto-launch")
	sim.submit_input(2,2,Vector3.UP,0,0,false)
	sim.step(.025)
	check(sim.actors[2].state=="perched","held Space requires a new edge")
	sim.submit_input(2,3,Vector3.ZERO,0,0,false)
	sim.submit_input(2,4,Vector3.UP,0,0,false)
	sim.step(.025)
	check(sim.actors[2].state=="flying","new Space edge actually departs")
	sim.submit_input(2,5,Vector3.ZERO,0,0,false)
	sim_land(sim,2,Vector3(-7,.16,8))
	var schedule: float = sim.actors[2]._next_rotation
	sim.submit_input(2,10,Vector3.FORWARD,0,0,false)
	sim.submit_input(2,9,Vector3.UP,1,0,false)
	sim.submit_input(2,10,Vector3.UP,1,0,false)
	sim.submit_input(2,11,Vector3(NAN,0,0),0,0,false)
	sim.step(.25)
	check(sim.actors[2].state=="perched" and sim.actors[2]._input_seq==10 and sim.actors[2].yaw==0.0,"duplicates/reorder/nonfinite input cannot detach or redirect")
	sim.step(.5)
	var stopped: Vector3 = sim.actors[2].p
	sim.step(.15)
	check(sim.actors[2].p==stopped and sim.actors[2].state=="perched","stale controls stop walking while support remains")
	check(sim.actors[2]._next_rotation==schedule,"walking and departure do not reset the private calendar")
	var snapshot: Dictionary = sim.public_snapshot()
	var own: Dictionary = sim.private_for(2)
	snapshot.tick=sim._frame; own.tick=sim._frame
	var audit = Privacy.new(); audit.record_private(own); audit.record_public(snapshot,2)
	check(audit.ok() and own.surface.attached and not snapshot.actors[2].has("_surface"),"network audit accepts only visible orientation, never support IDs")
	check(Insect.orientation(snapshot.actors[2]).is_equal_approx(Insect.orientation(sim._impact_actor(sim.actors[2]))),"public and authority impact orientation are equivalent")
	for mode: String in ["blood","sleep","survival"]:
		var hit_sim = make_sim(mode)
		sim_land(hit_sim,2,Vector3(-7,.16,8))
		hit_sim._kill(2)
		check(hit_sim.actors[2].state==("dead" if mode=="survival" else "stunned"),"surface impact preserves mode result "+mode)
		check(hit_sim.public_snapshot().actors[2].surface_normal==Vector3.ZERO and hit_sim.public_snapshot().actors[2].surface_forward==Vector3.ZERO,"impact clears previous visible support "+mode)
		if mode!="survival": check(hit_sim.private_for(2).stun.remaining==35.0,"surface hit preserves 35 second stun")

func _doors() -> void:
	var sim = make_sim()
	var definition: Dictionary = Doors.DEFINITIONS.kitchen
	sim.doors.kitchen.angle=0.0; sim.doors.kitchen.target_angle=0.0; sim.doors.kitchen.moving=false
	var transform := Doors.leaf_transform(definition,0.0)
	var position: Vector3 = transform*Vector3(1.1,1.1,.18)
	sim.actors[2].p = position
	sim._try_perch(2)
	check(Dictionary(sim.actors[2]._surface_pending).is_empty(),"closed leaf cannot become a support")
	var floor_start: Vector3 = transform*Vector3(1.1,.16,.24)
	sim_land(sim,2,floor_start)
	var normal: Vector3 = -transform.basis.z
	var yaw := atan2(-normal.x,-normal.z)
	# Set the intended heading through the visible gap, preserving input semantics.
	sim.actors[2]._surface_forward = normal
	sim.actors[2]._surface_view_yaw = yaw
	for tick: int in range(70):
		sim.submit_input(2,tick+1,Vector3.FORWARD,yaw,0,false)
		sim.step(1.0/60.0)
		check(ArenaData.can_fit_mosquito(sim.actors[2].p,"house",sim.doors),"walking under door preserves its exact OBB clearance")
	check((transform.affine_inverse()*Vector3(sim.actors[2].p)).z<-.15 and sim.actors[2].state=="perched","unchanged .04 radius walks through visible .14 door gap")

func _help_and_focus() -> void:
	var helper_sim = make_sim("sleep",2)
	sim_land(helper_sim,2,Vector3(-7,.16,8))
	helper_sim.actors[3].p = Vector3(-7,.04,7.5)
	helper_sim._kill(3)
	var helper_start: Vector3 = helper_sim.actors[2].p
	for tick: int in range(30):
		helper_sim.submit_input(2,tick+1,Vector3.ZERO,0,0,true)
		helper_sim.step(1.0/60.0)
	check(helper_sim.actors[2].state=="perched" and helper_sim.actors[2].p==helper_start,"held E helps without forcing a supported helper into flight")
	check(helper_sim.actors[2].help_target==3 and absf(float(helper_sim.actors[3]._stun_remaining)-33.0)<.0001,"supported assistance preserves exactly 4x total recovery rate")
	check(helper_sim.actors[2]._focus_progress==0.0,"help retains priority over mark concentration")
	var focus_sim = make_sim()
	focus_sim.actors[1].p = Vector3(-7,0,8)
	focus_sim.actors[2]._assignment = {"human":1,"zone":6,"revision":1}
	sim_land(focus_sim,2,Vector3(-7,.16,7.35))
	var began: float = focus_sim.elapsed
	var deadline: float = focus_sim.actors[2]._next_rotation
	for tick: int in range(240):
		var point: Vector3 = focus_sim.private_for(2).assignment.p
		var direction: Vector3 = (point-Vector3(focus_sim.actors[2].p)).normalized()
		focus_sim.submit_input(2,tick+1,Vector3.ZERO,atan2(-direction.x,-direction.z),asin(clampf(direction.y,-1,1)),true)
		focus_sim.step(1.0/60.0)
		if focus_sim.actors[2].state=="biting": break
	check(focus_sim.actors[2].state=="biting" and focus_sim.elapsed-began>=Sim.FOCUS_SECONDS-.0001,"held E departs from floor and completes actual timed focus")
	check(focus_sim.actors[2]._next_rotation==deadline and Dictionary(focus_sim.actors[2]._surface).is_empty(),"focus preserves calendar and discards the environmental support")
