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
		if failures<12:printerr("POSE_CACHE_FAIL "+label)

func _initialize() -> void:
	var sim=Sim.new()
	var other=Sim.new()
	var roster={1:{"role":"human"},2:{"role":"mosquito"}}
	sim.start(roster,{})
	other.start(roster,{})
	var human: Dictionary=sim.actors[1]
	var tools: Array=Sim.TOOL_STATS.keys()
	for index: int in range(120):
		# Changes deliberately occur without a tick: exact pose inputs, rather
		# than frame numbers or peer IDs, determine cache validity.
		human.p=Vector3(-7+sin(index*.4),float(index%2)*3.2,8)
		human.yaw=sin(index*.19)*PI
		human.body_yaw=cos(index*.3)*PI
		human.pitch=lerpf(-1.92,1.3,float(index%9)/8.0)
		human.crouch_amount=float(index%3)*.5
		human.motion_phase=index*.23
		human.motion_speed=3.1 if index%2==0 else 5.0
		human.motion_blend=float(index%4)/3.0
		human.motion_stride=.72+float(index%5)*.2
		human.motion_direction=Vector3.FORWARD.rotated(Vector3.UP,index*.17)
		human.grounded=index%4!=0
		human.air_blend=0.0 if human.grounded else .8
		human.land_blend=float(index%6)/5.0
		human.sprinting=index%2!=0
		human.pose_time=index*.05
		human.tool=tools[index%tools.size()]
		human.strike={"active":index%4!=0,"progress":float(index%13)/12.0,"point":human.p+Vector3(sin(index),1.2,cos(index)),"normal":Vector3.FORWARD.rotated(Vector3.UP,index*.3),"tool":human.tool,"hand":"left" if index%2==0 else "right"}
		var bundle: Dictionary=sim._pose_bundle(1)
		check(bundle.pose==Pose.sample(human),"cached sample equals uncached sample after in-frame mutation")
		var bounds: AABB=ArenaData.human_envelope(human)
		for capsule: Dictionary in bundle.capsules:
			var a: Vector3=human.p+Vector3(capsule.from).rotated(Vector3.UP,human.body_yaw)
			var b: Vector3=human.p+Vector3(capsule.to).rotated(Vector3.UP,human.body_yaw)
			check(bounds.encloses(AABB(a,Vector3.ZERO).expand(b).grow(capsule.radius)),"broad phase contains each actual animated anatomical capsule")
		for zone: int in range(Sim.BODY_ZONES.size()):
			var assignment={"human":1,"zone":zone}
			var expected: Dictionary=Pose.zone_pose(human,Sim.BODY_ZONES[zone])
			var actual: Dictionary=sim._zone_pose(assignment)
			check(actual==expected,"cached zone preserves exact shared pose and outward normal")
			actual.p=Vector3.INF
			check(sim._zone_pose(assignment)==expected,"returned pose cannot mutate internal cache")
	check(other._zone_pose({"human":1,"zone":0})==Pose.zone_pose(other.actors[1],Sim.BODY_ZONES[0]),"two simulations with same peer IDs have independent pose state")
	sim.start(roster,{})
	check(sim._zone_pose({"human":1,"zone":0})==Pose.zone_pose(sim.actors[1],Sim.BODY_ZONES[0]),"new round clears previous cached pose")
	print("POSE_CACHE07_TEST_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
