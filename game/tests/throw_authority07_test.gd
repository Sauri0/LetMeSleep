extends SceneTree
const Sim=preload("res://scripts/simulation.gd")
const Tools=preload("res://scripts/tool_catalog.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Collision=preload("res://scripts/projectile_collision.gd")
const ArenaData=preload("res://scripts/arena.gd")
var checks:=0
var failures:=0

func check(value: bool,label: String) -> void:
	checks+=1
	if not value:
		failures+=1
		printerr("THROW07_FAIL "+label)

func _initialize() -> void:
	_charge_and_ownership()
	_cancellation()
	_continuous_geometry()
	_flight_and_retrieval()
	_projectile_encounters()
	_safe_launch()
	_camera_convergence()
	_closed_leaf_encounter()
	_privacy_and_reset()
	print("THROW07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)

func fixture(tool: String="newspaper",mode: String="blood",humans: int=1) -> RefCounted:
	var sim:=Sim.new()
	var roster: Dictionary={1:{"role":"human"},2:{"role":"mosquito"}}
	if humans>1:roster[3]={"role":"human"}
	sim.start(roster,{"mode":mode,"round_seconds":180.0,"rotation_seconds":40.0})
	if humans>1:sim.actors[3].p=Vector3(-12,0,8)
	sim.actors[1].p=Vector3(-7,0,8)
	sim.actors[1].yaw=0.0
	sim.actors[1].body_yaw=0.0
	for id: int in sim.pickups:sim.pickups[id].p=Vector3(5,4,-8)
	if tool!="hands":
		var selected: int=-1
		for id: int in sim.pickups:
			if str(sim.pickups[id].tool)==tool:selected=id;break
		if selected<0:
			selected=999
			sim.pickups[selected]={"tool":tool,"holder":0,"state":"ground"}
		var rotation:=Vector3(PI*.5,0,0)
		sim.pickups[selected].rotation=rotation
		sim.pickups[selected].p=sim.actors[1].p+Vector3(.40,Collision.resting_offset(tool,rotation),0)
		act(sim,"pickup")
		advance(sim,1.0/60.0)
		check(sim.actors[1].tool==tool,"fixture equips real single pickup "+tool)
	return sim

func act(sim: RefCounted,verb: String,id: int=1,yaw: float=0.0,pitch: float=0.0) -> void:
	sim.action(id,int(sim.actors[id]._action_seq)+1,verb,yaw,pitch)

func advance(sim: RefCounted,seconds: float,fresh: bool=true,dt: float=1.0/60.0) -> void:
	var remaining:=seconds
	while remaining>.000001 and sim.phase=="playing":
		var part:=minf(dt,remaining)
		if fresh:sim.submit_input(1,int(sim.actors[1]._input_seq)+1,Vector3.ZERO,float(sim.actors[1].yaw),float(sim.actors[1].pitch),false)
		sim.step(part)
		remaining-=part

func launch(sim: RefCounted,charge: float=0.0) -> int:
	var id: int=sim._owned_pickup(1)
	act(sim,"throw_start")
	if charge>0:advance(sim,charge)
	act(sim,"throw_release")
	for frame: int in range(20):
		advance(sim,1.0/60.0)
		if str(sim.pickups[id].get("state",""))=="flying":return id
	print("THROW07_LAUNCH_DIAG ",sim.private_for(1).throw," gesture=",sim.actors[1].throw_gesture)
	return -1

func _charge_and_ownership() -> void:
	for tool: String in ["newspaper","slipper"]:
		for full: bool in [false,true]:
			var sim=fixture(tool)
			var count: int=sim.pickups.size()
			var stats:=Tools.throw_stats(tool)
			var id:=launch(sim,float(stats.charge_seconds)+.04 if full else 0.0)
			check(id>=0,"server launches "+tool+" full="+str(full))
			if id<0:continue
			var item: Dictionary=sim.pickups[id]
			check(sim.pickups.size()==count and sim.actors[1].tool=="hands" and int(item.holder)==0,"launch transfers one existing object without duplicate")
			check(absf(Vector3(item.velocity).length()-float(stats.max_speed if full else stats.min_speed))<.001,"speed derives from elapsed server charge only")
			check(not sim.private_for(1).pickup.can_take,"flying object cannot be collected immediately")
			var before: int=sim._projectiles.size()
			act(sim,"throw_release")
			act(sim,"throw_start")
			advance(sim,.05)
			check(sim._projectiles.size()==before and sim.pickups.size()==count,"release/start spam cannot duplicate launched item")
			check(item.owner==1 and item.rotation is Vector3,"public projectile origin and rotation have wire-safe types")
	for tool: String in ["hands","swatter","racket","broom"]:
		var sim=fixture(tool)
		act(sim,"throw_start")
		act(sim,"throw_release")
		advance(sim,.3)
		check(sim._projectiles.is_empty() and sim.actors[1].tool==tool,"nonthrowable item never launches "+tool)
	var mosquito=fixture()
	act(mosquito,"throw_start",2)
	act(mosquito,"throw_release",2)
	advance(mosquito,.3)
	check(mosquito._projectiles.is_empty(),"mosquito role cannot throw")

func _cancellation() -> void:
	for cause: String in ["throw_cancel","drop","pickup","attack","stale","timeout"]:
		var sim=fixture()
		act(sim,"throw_start")
		advance(sim,.15)
		check(sim.private_for(1).throw.state=="charging","charge is visible before cancellation "+cause)
		if cause=="stale":advance(sim,.5,false)
		elif cause=="timeout":advance(sim,Sim.THROW_HOLD_TIMEOUT+.1)
		else:act(sim,cause);advance(sim,.05)
		act(sim,"throw_release")
		advance(sim,.2)
		check(sim._projectiles.is_empty() and Dictionary(sim.actors[1]._throw).is_empty(),"cancellation cannot later launch "+cause)
	var sim=fixture()
	act(sim,"throw_start")
	act(sim,"throw_release")
	act(sim,"throw_cancel")
	advance(sim,.3)
	check(sim._projectiles.is_empty(),"cancel removes queued start/release even before physics frame")
	act(sim,"throw_start")
	advance(sim,.1)
	var seq: int=int(sim.actors[1]._action_seq)
	sim.action(1,seq,"throw_release",0.0,0.0)
	sim.action(1,seq-1,"throw_cancel",0.0,0.0)
	sim.action(1,seq+1,"throw_release",NAN,0.0)
	advance(sim,.05)
	check(sim.private_for(1).throw.state=="charging" and int(sim.actors[1]._action_seq)==seq,"duplicate/reordered/invalid aim cannot release or cancel")
	# Losing ownership independently also cancels; no pickup identity is trusted.
	sim.pickups[sim._owned_pickup(1)].holder=0
	advance(sim,.025)
	check(Dictionary(sim.actors[1]._throw).is_empty(),"loss of actual holder cancels charge")

func _continuous_geometry() -> void:
	var shape: Dictionary={"from":Vector3(-1,1,-.12),"to":Vector3(-1,1,.12),"radius":.05}
	var thin:=AABB(Vector3(-.015,.7,-.3),Vector3(.03,.6,.6))
	var hit:=Collision.sweep_box(shape,Vector3.RIGHT*2,thin)
	check(not hit.is_empty() and absf(float(hit.fraction)-.4675)<.0001,"continuous capsule hits thin wall between endpoints")
	check(Collision.sweep_box(Collision.translated(shape,Vector3.UP*.5),Vector3.RIGHT*2,thin).is_empty(),"sweep above wall stays clear")
	var target: Dictionary={"from":Vector3(0,.9,0),"to":Vector3(0,1.1,0),"radius":.04}
	check(not Collision.sweep_capsules(shape,Collision.translated(shape,Vector3.RIGHT*2),target,target).is_empty(),"continuous long body hits crossing capsule")
	var door_sim=fixture()
	door_sim.doors.kitchen.angle=PI*.25
	door_sim.doors.kitchen.target_angle=PI*.25
	var door_shape:=Collision.capsule("newspaper",Vector3(-.8,1.2,-8.2),Vector3.ZERO)
	check(not Collision.map_hit(door_shape,Vector3.LEFT*3.4,"house",door_sim.doors).is_empty(),"oriented door leaf blocks projectile sweep")

func _flight_and_retrieval() -> void:
	for tool: String in ["newspaper","slipper"]:
		var sim=fixture(tool)
		var id:=launch(sim,0.0)
		if id<0:check(false,"launch needed for landing "+tool);continue
		advance(sim,5.0)
		var item: Dictionary=sim.pickups[id]
		check(item.state=="ground" and sim._projectiles.is_empty(),"projectile lands and becomes retrievable "+tool)
		check(Vector3(item.velocity).is_zero_approx() and float(item.ttl)==0.0,"resting object stops without disappearing")
		var floor: float=ArenaData.floor_below(Vector3(item.p)+Vector3.UP*.1,"house",sim.doors)
		var expected: float=floor+Collision.resting_offset(tool,item.rotation)
		check(absf(float(item.p.y)-expected)<.015,"visible bounds meet actual support without floating "+tool)
		sim.actors[1].p=Vector3(item.p)+Vector3(.7,-float(item.p.y),0)
		act(sim,"pickup")
		advance(sim,.025)
		check(sim.actors[1].tool==tool and int(item.holder)==1,"same landed pickup can be taken again")

func _privacy_and_reset() -> void:
	var sim=fixture()
	act(sim,"throw_start")
	advance(sim,.2)
	var snapshot: Dictionary=sim.public_snapshot()
	check(not snapshot.actors[1].has("_throw") and snapshot.actors[1].throw_gesture.state=="charging","only visible gesture is public")
	check(not sim.private_for(2).has("throw"),"mosquito never receives human throw controls")
	for pickup: Dictionary in snapshot.pickups.values():
		for key: String in pickup:check(not key.begins_with("_"),"projectile internals never serialize")
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"mode":"blood"})
	check(sim._projectiles.is_empty() and sim.actors[1].tool=="hands" and sim.actors[1].throw_gesture.state=="idle","next round resets charge/items to original ownership")

func _projectile_encounters() -> void:
	for tool: String in ["newspaper","slipper"]:
		for mode: String in ["blood","sleep","survival"]:
			for dt: float in [.05,1.0/60.0]:
				var sim=fixture(tool,mode)
				var id:=launch(sim,0.0)
				if id<0:check(false,"encounter launch");continue
				var item: Dictionary=sim.pickups[id]
				var direction:=Vector3(item.velocity).normalized()
				var travel:=.65
				var seconds:=travel/Vector3(item.velocity).length()
				sim.actors[2].p=Vector3(item.p)+direction*travel+Vector3.DOWN*9.8*seconds*seconds*.5
				var initial_blood: float=sim.blood
				advance(sim,.30,true,dt)
				var expected: String="dead" if mode=="survival" else "stunned"
				check(sim.actors[2].state==expected,"real released object hits insect %s %s %.0fHz" % [tool,mode,1.0/dt])
				check(sim.actors[2].impact.tool==tool and sim.actors[2].impact.kind=="projectile" and sim.actors[2].impact.material==Tools.DATA[tool].material,"confirmed event retains launched object's tool/material after owner has hands")
				check(sim.blood==initial_blood,"projectile does not create or reset shared blood")
				if mode!="survival":check(sim.actors[2].alive and float(sim.actors[2]._stun_remaining)>34.6,"projectile preserves 35s stun rather than lives/death")
	var sim=fixture("newspaper","blood",2)
	var id:=launch(sim,0.0)
	if id<0:check(false,"human obstruction launch");return
	var start: Vector3=sim.pickups[id].p
	sim.actors[3].p=Vector3(start.x,0,start.z-.75)
	sim.actors[2].p=start+Vector3(0,-.25,-1.2)
	advance(sim,.4)
	check(sim.actors[3].alive and not sim.actors[3].has("health"),"human projectile obstacle has no damage/health mechanic")
	check(sim.actors[2].state=="flying" and int(sim.actors[2].impact.id)==0,"projectile cannot damage insect behind human torso")
	check(sim.pickups[id].impact_kind=="human","human blocks physical projectile before hidden target")

func _safe_launch() -> void:
	var sim=fixture()
	sim.actors[2]._assignment={"human":1,"zone":5,"revision":10}
	sim.actors[2].state="biting"
	sim._update_attached()
	var id:=launch(sim,0.0)
	check(id>=0,"occupied throwing arm does not prevent real release")
	advance(sim,.3)
	check(sim.actors[2].state=="biting" and int(sim.actors[2].impact.id)==0,"initial overlap exclusion avoids automatic strike on insect carried by throwing arm")
	sim=fixture()
	var human: Dictionary=sim.actors[1]
	human.p=Vector3(-1.45,0,-8.2)
	human.yaw=PI*.5
	human.body_yaw=PI*.5
	sim.doors.kitchen.angle=0.0
	sim.doors.kitchen.target_angle=0.0
	act(sim,"throw_start",1,PI*.5,0)
	advance(sim,.1)
	act(sim,"throw_release",1,PI*.5,0)
	advance(sim,.2)
	check(sim._projectiles.is_empty() and human.tool=="newspaper","blocked release keeps original held item")
	check(sim.private_for(1).throw.reason=="blocked","blocked launch gives explicit private feedback")

func _camera_convergence() -> void:
	for tool: String in ["newspaper","slipper"]:
		var sim=fixture(tool)
		act(sim,"throw_start")
		advance(sim,.15)
		var eye: Vector3=Pose.view_origin(sim.actors[1])
		var ray: Vector3=Pose.view_direction(sim.actors[1])
		sim.actors[2].p=eye+ray*.55
		var exact: Vector3=sim._throw_aim_point(1,{"yaw":0.0,"pitch":0.0})
		check((exact-eye).cross(ray).length()<.00001 and eye.distance_to(exact)<.56,"near reticle contact remains exactly on camera ray "+tool)
		act(sim,"throw_release")
		var item_id: int=sim._owned_pickup(1)
		for frame: int in range(12):
			advance(sim,1.0/60.0)
			if sim.pickups[item_id].state=="flying":break
		check(sim.pickups[item_id].state=="flying","near reticle target permits actual hand launch "+tool)
		if sim.pickups[item_id].state!="flying":continue
		var item: Dictionary=sim.pickups[item_id]
		var direction:=Vector3(item.velocity).normalized()
		check((exact-Vector3(item.p)).cross(direction).length()<.0001,"released hand trajectory converges on exact captured ray point "+tool)
		check(Vector3(item.p).distance_to(Pose.throw_origin(sim.actors[1],direction,float(sim.actors[1].throw_gesture.power)))<.0001,"projectile begins at real shared grip rather than camera "+tool)
		advance(sim,.20)
		check(sim.actors[2].state=="stunned","close reticle encounter hits with physical projectile and normal gravity "+tool)
	var sim=fixture()
	act(sim,"throw_start")
	advance(sim,.1)
	var eye: Vector3=Pose.view_origin(sim.actors[1])
	var baseline: Vector3=sim._throw_aim_point(1,{"yaw":0.0,"pitch":0.0})
	sim.actors[2].p=eye+Vector3(.16,0,-.55)
	check(sim._throw_aim_point(1,{"yaw":0.0,"pitch":0.0}).distance_to(baseline)<.00001,"nearby insect outside zero-width ray never snaps aim")
	act(sim,"throw_release")
	advance(sim,1.0/60.0)
	var locked: Vector3=sim.actors[1]._throw.aim_point
	sim.actors[2].p=eye+Vector3(0,0,-.8)
	advance(sim,.05)
	check(Vector3(sim.actors[1]._throw.aim_point).distance_to(locked)<.00001,"release keeps world aim point instead of tracking moved insect")
	check(not sim.public_snapshot().actors[1].throw_gesture.has("aim_point"),"captured world point stays private")

func _closed_leaf_encounter() -> void:
	for tool: String in ["newspaper","slipper"]:
		var sim=fixture(tool)
		var human: Dictionary=sim.actors[1]
		human.p=Vector3(-.7,0,-8.2)
		human.yaw=PI*.5
		human.body_yaw=PI*.5
		sim.doors.kitchen.angle=0.0
		sim.doors.kitchen.target_angle=0.0
		sim.actors[2].p=Vector3(-2.65,1.5,-8.2)
		var held: int=sim._owned_pickup(1)
		act(sim,"throw_start",1,PI*.5,0)
		advance(sim,.25)
		var point: Vector3=sim._throw_aim_point(1,{"yaw":PI*.5,"pitch":0.0})
		check(point.x>-2.2,"camera point stops at leaf before hidden insect "+tool)
		act(sim,"throw_release",1,PI*.5,0)
		advance(sim,.45)
		check(human.tool=="hands" and sim.pickups[held].impact_kind=="door","released projectile actually contacts closed door "+tool)
		check(sim.actors[2].state=="flying" and sim.actors[2].impact.id==0,"closed door prevents projectile damage to hidden insect "+tool)
