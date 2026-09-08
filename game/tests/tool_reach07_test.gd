extends SceneTree
const Sim=preload("res://scripts/simulation.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Tools=preload("res://scripts/tool_catalog.gd")
var checks:=0
var failures:=0

func check(value: bool,label: String) -> void:
	checks+=1
	if not value:failures+=1;printerr("TOOL_REACH07_FAIL "+label)

func _initialize() -> void:
	create_timer(20.0).timeout.connect(func() -> void:printerr("TOOL_REACH07 watchdog");quit(1))
	for tool: String in Tools.IDS:
		for beyond: bool in [false,true]:
			var sim:=Sim.new()
			sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"mode":"blood"})
			var human: Dictionary=sim.actors[1]
			human.p=Vector3(-7,0,8)
			human.tool=tool
			var stats:=Tools.melee_stats(tool)
			var empty: Dictionary=sim._strike_plan(1)
			var shoulder: Vector3=Vector3(human.p)+Vector3(Pose.sample(human).shoulder_r)
			check(absf(shoulder.distance_to(empty.point)-float(stats.reach))<.001,"ray endpoint uses shoulder reach without a second shaft "+tool)
			check(absf(float(stats.reach)-Tools.ARM_REACH-Tools.contact_length(tool))<.00001,"catalog includes arm plus object exactly once "+tool)
			print("TOOL_REACH07_MEASURE tool=",tool," shoulder=",stats.reach," eye=",empty.reach," gesture=",stats.gesture," contact=",stats.damage_start,"..",stats.damage_end)
			sim.actors[2].p=Vector3(empty.point)+Vector3.FORWARD*(.30 if beyond else -.025)
			check(bool(sim.private_for(1).attack.candidate)!=beyond,"candidate distinguishes geometric end and beyond "+tool+" beyond="+str(beyond))
			sim.action(1,1,"attack",0.0,0.0)
			var first_hit:=-1.0
			for frame: int in range(36):
				sim.step(1.0/60.0)
				if sim.actors[2].state=="stunned" and first_hit<0:first_hit=sim.elapsed-float(human._strike_started)
			check((first_hit>=0.0)!=beyond,"actual sweep honors the same finite endpoint "+tool+" beyond="+str(beyond))
			if not beyond:
				check(first_hit>=float(stats.damage_start) and first_hit<=float(stats.damage_end)+1.0/60.0,"impact happens within this object's contact window "+tool)
				check(sim.actors[2].impact.tool==tool and sim.actors[2].impact.material==Tools.DATA[tool].material and sim.actors[2].impact.kind=="melee","confirmed impact identifies actual object's material "+tool)
	print("TOOL_REACH07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
