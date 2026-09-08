extends SceneTree
const Sim = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const ArenaData = preload("res://scripts/arena.gd")
var checks := 0
var failures := 0

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("ATTACK07_FAIL "+label)

func make_sim(tool: String = "hands") -> RefCounted:
	var sim = Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"mode":"blood","rotation_seconds":40})
	sim.actors[1].p=Vector3(-7,0,8)
	sim.actors[1].tool=tool
	return sim

func _initialize() -> void:
	_static_footprint()
	_moving_flight()
	_attached_opportunity()
	_blocked_and_recovery()
	_pickup_contract()
	_same_arm_carry()
	print("ATTACK07_TEST_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)

func _static_footprint() -> void:
	for tool: String in Sim.TOOL_STATS:
		var radius: float=float(Sim.TOOL_STATS[tool].radius)+ArenaData.MOSQUITO_RADIUS
		for offset: float in [0.0,.075,radius-.01,radius+.035]:
			var sim=make_sim(tool)
			var eye: Vector3=Pose.view_origin(sim.actors[1])
			sim.actors[2].p=eye+Vector3(offset,0,-.22)
			var expected: bool=offset<radius
			var info: Dictionary=sim.private_for(1).attack
			check(bool(info.candidate)==expected,"private opportunity uses physical footprint tool=%s offset=%.3f" % [tool,offset])
			sim.action(1,1,"attack",0.0,0.0)
			for frame: int in range(20):sim.step(.025)
			check((sim.actors[2].state=="stunned")==expected,"manual close-range face tool=%s offset=%.3f" % [tool,offset])
			check(Vector3(sim.actors[1].strike.direction).is_equal_approx(Vector3.FORWARD) and absf(sim.actors[1].strike.point.x-eye.x)<.00001,"target depth never rotates or snaps captured manual ray")

func _moving_flight() -> void:
	for tool: String in Sim.TOOL_STATS:
		for dt: float in [.05,1.0/60.0]:
			var sim=make_sim(tool)
			sim.actors[2].p=Vector3(7,4.5,-8)
			var plan: Dictionary=sim._strike_plan(1)
			var mosquito: Dictionary=sim.actors[2]
			mosquito.p=Vector3(plan.point)-Vector3.RIGHT*ArenaData.MOSQUITO_SPEED*(dt+.12)
			mosquito.velocity=Vector3.RIGHT*ArenaData.MOSQUITO_SPEED
			sim.action(1,1,"attack",0.0,0.0)
			var first_hit := -1.0
			for frame: int in range(int(ceil(.4/dt))):
				sim.submit_input(2,frame+1,Vector3.RIGHT,0.0,0.0,false)
				sim.step(dt)
				if mosquito.state=="stunned":
					first_hit=sim.elapsed-float(sim.actors[1]._strike_started)
					break
			print("ATTACK07_MOVING tool=",tool," hz=",1.0/dt," contact=",first_hit)
			check(first_hit>=Sim.STRIKE_START and first_hit<=Sim.STRIKE_END+.05,"manual lead catches full-speed crossing within real gesture at%.1fHz tool=%s" % [1.0/dt,tool])

func _attached_opportunity() -> void:
	for tool: String in Sim.TOOL_STATS:
		for crouch: float in [0.0,1.0]:
			for zone: int in range(Sim.FRONT_ZONE_COUNT):
				var sim=make_sim(tool)
				var human: Dictionary=sim.actors[1]
				human.crouch_amount=crouch
				human._crouch=crouch>0.0
				human._last_input=0.0
				sim.actors[2]._assignment={"human":1,"zone":zone,"revision":1}
				sim.actors[2].state="biting"
				sim._update_attached()
				var angles: Vector2=Pose.aim_angles(human,sim.actors[2].p)
				sim.submit_input(1,1,Vector3.ZERO,angles.x,angles.y,false,false,crouch>0.0)
				var info: Dictionary=sim.private_for(1).attack
				check(info.candidate,"visible attached opportunity tool=%s crouch=%.0f zone=%d" % [tool,crouch,zone])
				sim.action(1,1,"attack",angles.x,angles.y)
				sim.step(.3)
				check(sim.actors[2].state=="stunned","same authority geometry hits attached tool=%s crouch=%.0f zone=%d" % [tool,crouch,zone])

func _blocked_and_recovery() -> void:
	var sim=make_sim("broom")
	var human: Dictionary=sim.actors[1]
	human.p=Vector3(-.7,0,-8.2)
	human.yaw=PI*.5
	human.body_yaw=PI*.5
	sim.doors.kitchen.angle=0.0
	sim.doors.kitchen.target_angle=0.0
	var eye: Vector3=Pose.view_origin(human)
	sim.actors[2].p=Vector3(-2.6,eye.y,-8.2)
	var info: Dictionary=sim.private_for(1).attack
	check(not info.candidate and info.status=="blocked","closed leaf never offers hidden insect opportunity")
	sim.action(1,1,"attack",human.yaw,0.0)
	sim.step(.15)
	info=sim.private_for(1).attack
	check(not info.can_swing and not info.candidate and info.status=="recovery" and info.recovery>0.0,"recovery is explicit without promising a second hit")
	sim.step(.3)
	check(sim.actors[2].state=="flying","manual broad face cannot hit through physical door")
	var public: Dictionary=sim.public_snapshot()
	check(not public.actors[1].has("attack") and not public.actors[1].has("candidate") and not public.has("candidate"),"attack opportunity remains private")
	check(not sim.private_for(2).has("attack"),"mosquito never receives human opportunity")
	sim=make_sim()
	eye=Pose.view_origin(sim.actors[1])
	sim.actors[2].p=eye+Vector3.FORWARD*2.0
	check(not sim.private_for(1).attack.candidate,"insect beyond finite physical arm reach gives no opportunity")
	sim.action(1,1,"attack",0.0,0.0)
	sim.step(.3)
	check(sim.actors[2].state=="flying","short hands never hit distant insect")

func _pickup_contract() -> void:
	var sim=make_sim()
	var human: Dictionary=sim.actors[1]
	human.p=Vector3(-.7,0,-8.2)
	sim.doors.kitchen.angle=0.0
	sim.doors.kitchen.target_angle=0.0
	sim.pickups={0:{"tool":"racket","p":Vector3(-2.05,.15,-8.2),"holder":0,"yaw":0.0}}
	check(sim.private_for(1).pickup.can_take,"unheld visible same-side pickup offers R")
	sim.action(1,1,"pickup")
	sim.step(.025)
	check(human.tool=="racket" and sim.pickups[0].holder==1,"private pickup criterion matches atomic authority selection")
	check(not sim.private_for(1).pickup.can_take,"owned tool cannot be offered as free")
	sim.pickups[1]={"tool":"newspaper","p":human.p+Vector3(.2,.15,0),"holder":0,"yaw":0.0}
	sim.action(1,2,"pickup")
	sim.step(.025)
	check(human.tool=="newspaper" and sim.pickups[0].holder==0 and sim.pickups[1].holder==1,"exchange releases old tool and gives exactly one new owner")
	check(ArenaData.clear_segment(human.p+Vector3.UP*.8,sim.pickups[0].p,"house",sim.doors),"exchange drops old item on reachable side of closed leaf")
	sim.actors[1].p=Vector3(-1.3,0,-8.2)
	sim.pickups={0:{"tool":"broom","p":Vector3(-2.6,.15,-8.2),"holder":0,"yaw":0.0}}
	check(not sim.private_for(1).pickup.can_take,"closed leaf suppresses nearby hidden pickup hint")
	sim.action(1,3,"pickup")
	sim.step(.025)
	check(sim.pickups[0].holder==0,"same LOS rejects pickup through door")
	sim.pickups[0].p=Vector3(human.p.x,3.35,human.p.z)
	check(not sim.private_for(1).pickup.can_take,"different floor never offers pickup")
	check(not sim.private_for(2).has("pickup"),"human item interaction is private to human role")

func _same_arm_carry() -> void:
	for tool: String in Sim.TOOL_STATS:
		for crouch: float in [0.0,1.0]:
			var sim=make_sim(tool)
			var human: Dictionary=sim.actors[1]
			human.crouch_amount=crouch
			human._crouch=crouch>0.0
			human._last_input=0.0
			sim.actors[2]._assignment={"human":1,"zone":5,"revision":1}
			sim.actors[2].state="biting"
			sim._update_attached()
			sim.action(1,1,"self_swat",0.0,0.0)
			sim.step(.3)
			check(sim.actors[2].state=="biting","moving the occupied striking forearm is not a hit tool=%s crouch=%.0f" % [tool,crouch])
