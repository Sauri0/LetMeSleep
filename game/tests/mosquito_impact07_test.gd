extends SceneTree
const Sim=preload("res://scripts/simulation.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Insect=preload("res://scripts/mosquito_pose.gd")
const ArenaData=preload("res://scripts/arena.gd")
var checks:=0
var failures:=0

func check(ok: bool,label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		printerr("MOSQUITO_IMPACT07_FAIL "+label)

func _initialize() -> void:
	_anatomy()
	_public_orientation()
	_continuous_capsule()
	_manual_tip()
	_door_and_privacy()
	print("MOSQUITO_IMPACT07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)

func _contains(point: Vector3, capsules: Array[Dictionary]) -> bool:
	for capsule: Dictionary in capsules:
		if point.distance_to(Insect.closest_axis(point,capsule.from,capsule.to))<=float(capsule.radius)+.000001: return true
	return false

func _anatomy() -> void:
	var capsules:=Insect.local_segments()
	check(ArenaData.MOSQUITO_RADIUS==.04,"impact never changes .04 locomotion radius")
	check(capsules.size()==3,"three anatomical parts, no accessory hitboxes")
	for point: Vector3 in [Vector3.ZERO,Vector3(0,-.0104125,.111776),Vector3(0,.015,-.060),Vector3(0,.043,-.038)]:
		check(_contains(point,capsules),"core/head/visible B tail included "+str(point))
	for point: Vector3 in [Vector3(.09,.021,.023),Vector3(0,-.015,-.092),Vector3(.018,.064,-.039),Vector3(0,-.045,.06),Vector3(.06,0,.085),Vector3(0,0,.145)]:
		check(not _contains(point,capsules),"wings/proboscis/antennae/legs/air excluded "+str(point))
	for capsule: Dictionary in capsules:
		check(maxf(Vector3(capsule.from).length(),Vector3(capsule.to).length())+float(capsule.radius)<=Insect.BOUND_RADIUS,"broadphase encloses "+str(capsule.key))
	# Preserve the complete old sphere, not only its central point.
	for latitude: int in range(9):
		for longitude: int in range(16):
			var v:=Vector3(sin(latitude*PI/8)*cos(longitude*TAU/16),cos(latitude*PI/8),sin(latitude*PI/8)*sin(longitude*TAU/16))*.04
			check(_contains(v,capsules),"previous core surface remains included")

func _public_orientation() -> void:
	for state: String in ["flying","biting","perched","stunned"]:
		for yaw: float in [0.0,PI*.5,PI,-2.1]:
			for pitch: float in [-1.5,0.0,1.5]:
				for normal: Vector3 in [Vector3.UP,Vector3.DOWN,Vector3.LEFT,Vector3.RIGHT,Vector3.FORWARD,Vector3.BACK]:
					var actor: Dictionary={"p":Vector3(-7,1.3,8),"yaw":yaw,"pitch":pitch,"state":state,"surface_normal":normal,"velocity":Vector3(1.1,-.3,2.4)}
					var basis:=Insect.orientation(actor)
					check(absf(basis.determinant()-1.0)<.00001,"orthonormal public basis "+state)
					if state in ["biting","perched"]: check(basis.y.is_equal_approx(normal),"surface orientation agrees on all six faces")
					var hidden:=actor.duplicate(true)
					hidden._assignment={"human":123,"zone":9}
					hidden._next_rotation=1234
					check(Insect.orientation(hidden).is_equal_approx(basis),"private reservation cannot affect public orientation")
					var capsules:=Insect.collision_segments(actor)
					for local: Vector3 in [Vector3.ZERO,Vector3(0,-.0104125,.111776)]:
						var point: Vector3=Vector3(actor.p)+basis*local
						check(_contains(point,capsules),"centre and tail follow each public orientation")

func _continuous_capsule() -> void:
	var shape: Dictionary={"from":Vector3(0,0,-.03),"to":Vector3(0,0,.03),"radius":.016}
	for hz: float in [20.0,60.0]:
		for speed: float in [3.8,30.0]:
			var before:=shape.duplicate()
			var after:=shape.duplicate()
			for key: String in ["from","to"]:
				before[key]+=Vector3.LEFT*speed/hz*.5
				after[key]+=Vector3.RIGHT*speed/hz*.5
			var hit:=Insect.swept_contact(Vector3.ZERO,Vector3.ZERO,before,after,.06)
			check(not hit.is_empty(),"capsule crossing between endpoints cannot tunnel at%.0fHz speed%.1f" % [hz,speed])
			check(Insect.swept_contact(Vector3(0,.09,0),Vector3(0,.09,0),before,after,.06).is_empty(),"continuous crossing outside combined footprint misses")
	var rotated:=shape.duplicate()
	rotated.from=Vector3(-.03,0,0)
	rotated.to=Vector3(.03,0,0)
	check(not Insect.swept_contact(Vector3(.013,0,.013),Vector3(.013,0,.013),shape,rotated,.003).is_empty(),"rotating shaft contact between endpoints")

func _sim(tool: String="newspaper") -> RefCounted:
	var sim:=Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"mode":"blood","rotation_seconds":40})
	sim.actors[1].p=Vector3(-8,0,8)
	sim.actors[1].yaw=-PI*.5
	sim.actors[1].body_yaw=-PI*.5
	sim.actors[1].tool=tool
	return sim

func _manual_tip() -> void:
	for dt: float in [.05,1.0/60.0]:
		for local: Vector3 in [Vector3.ZERO,Vector3(0,-.0104125,.111776),Vector3(0,0,.20)]:
			var sim=_sim()
			var eye:=Pose.view_origin(sim.actors[1])
			var face:=eye+Vector3.RIGHT*.32
			sim.actors[2].p=face-local
			var expected:=local.z<.15
			var info: Dictionary=sim.private_for(1).attack
			check(bool(info.candidate)==expected,"private opportunity matches anatomy at point"+str(local))
			sim.action(1,1,"attack",-PI*.5,0.0)
			for tick: int in range(int(ceil(.32/dt))): sim.step(dt)
			check((sim.actors[2].state=="stunned")==expected,"real manual LMB reaches centre/tail, misses air, dt="+str(dt)+" point="+str(local))
			check(absf(Vector3(sim.actors[1].strike.direction).dot(Vector3.RIGHT)-1.0)<.00001,"depth candidate never redirects manual ray")
			if expected: check(float(sim.actors[2]._stun_remaining)>34.6 and float(sim.actors[2]._stun_remaining)<=35.0,"hit retains 35 second stun")

func _door_and_privacy() -> void:
	var sim=_sim("broom")
	var human: Dictionary=sim.actors[1]
	human.p=Vector3(-.7,0,-8.2)
	human.yaw=PI*.5
	human.body_yaw=PI*.5
	var eye:=Pose.view_origin(human)
	sim.doors.kitchen.angle=0.0
	sim.doors.kitchen.target_angle=0.0
	sim.actors[2].p=Vector3(-2.22,eye.y,-8.2)
	sim.actors[2].yaw=PI*.5
	check(not sim.private_for(1).attack.candidate,"abdomen behind closed leaf never offers opportunity")
	sim.action(1,1,"attack",PI*.5,0.0)
	sim.step(.3)
	check(sim.actors[2].state=="flying","body and extended tail cannot be hit through leaf")
	var first: Dictionary=sim.public_snapshot()
	sim.actors[2]._assignment={"human":1,"zone":7,"revision":99}
	var second: Dictionary=sim.public_snapshot()
	check(first.actors[2]==second.actors[2],"changing free reservation preserves entire public actor")
	check(not first.actors[1].has("candidate") and not first.actors[2].has("_assignment"),"new impact data adds no private target fields")
	check(not sim.private_for(2).has("attack"),"human opportunity never appears in mosquito private packet")
	check(Sim.HELP_RATE==4.0 and Sim.STUN_SECONDS==35.0,"help and stun constants unchanged")
