extends SceneTree
const Sim = preload("res://scripts/simulation.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Routes = preload("res://scripts/map_navigation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Pose = preload("res://scripts/human_pose.gd")
const DoorData = preload("res://scripts/door_catalog.gd")
const Generator = preload("res://scripts/procedural_house.gd")
const DT := .025
var checks := 0
var failures := 0
var cases: Array[Dictionary] = []

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		if failures<=20: printerr("MAP_TASKS09_FAIL "+label)

func _initialize() -> void:
	_identity()
	_generated_routes()
	_doors_and_extension()
	_pending_and_deadlines()
	var file=FileAccess.open("res://../work/map-tasks09-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"cases":cases},"\t"))
	file.close()
	print("MAP_TASKS09_RESULT checks=%d failures=%d"%[checks,failures])
	quit(0 if failures==0 else 1)

func make_sim(map_id: String="house",extra: Dictionary={}) -> RefCounted:
	var settings: Dictionary={"mode":"sleep","map_id":map_id,"round_seconds":180,"task_goal":99}
	settings.merge(extra,true)
	var sim=Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},settings)
	return sim

func _identity() -> void:
	var sim=make_sim("house",{"map_fingerprint":"spoof","map_generator_version":999,"map_seed":999})
	check(sim.phase=="playing" and sim.config.map_fingerprint==Maps.Validation.fingerprint(Maps.get_map("house")),"authored map fingerprint comes from real geometry")
	check(sim.config.map_seed==0 and sim.config.map_generator_version==0,"authored map does not accept client generator metadata")
	for invalid: Variant in ["lobby","unknown","house-v1-1","house-v2-1","house-v3-0","house-v3-01","house-v4-1","house-v3-2147483647",42,null]:
		sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"map_id":invalid})
		check(sim.phase=="lobby" and not sim.reason.is_empty() and sim.winner.is_empty(),"invalid map fails explicitly: "+str(invalid))
		check(sim.actors.is_empty() and sim.pickups.is_empty() and sim.doors.is_empty() and sim._map_data.is_empty(),"failed start retains no old actors/doors/map: "+str(invalid))
	# Fault injection at the catalog boundary simulates a syntactically valid
	# identity whose generated geometry failed validation; no fallback is legal.
	var failed_id := Generator.map_id(2147483000)
	var had_value: bool=Maps._generated.has(failed_id)
	var previous: Variant=Maps._generated.get(failed_id)
	Maps._generated[failed_id]={}
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"map_id":failed_id})
	check(sim.phase=="lobby" and sim.actors.is_empty() and sim.reason.contains("validación"),"failed generation never becomes authored house")
	if had_value: Maps._generated[failed_id]=previous
	else: Maps._generated.erase(failed_id)
	for seed_value: int in [1,2]:
		var id := Generator.map_id(seed_value)
		var map: Dictionary=Maps.get_map(id)
		var generated=make_sim(id,{"map_fingerprint":"spoof","map_generator_version":999,"map_seed":999})
		check(generated.phase=="playing" and generated.config.map_id==id,"validated generated map starts "+id)
		if generated.phase!="playing": continue
		check(generated.config.map_fingerprint==map.fingerprint and generated.config.map_generator_version==Generator.VERSION and generated.config.map_seed==seed_value,"server stamps generated identity "+id)
		check(generated.public_snapshot().config==generated.config and generated.actors[1].p==map.human_spawns[0],"public config and spawn correspond to the same generated map "+id)
		check(generated.pickups.size()==map.pickups.size() and generated.doors.size()==map.doors.size(),"round state uses selected map's pickups and doors "+id)

func _generated_routes() -> void:
	for seed_value: int in [1,2]:
		var sim=make_sim(Generator.map_id(seed_value),{"task_deadline":24,"task_floor":24})
		if sim.phase!="playing": check(false,"generated route fixture starts");continue
		var map: Dictionary=sim._map_data
		var goal: int=sim.task_goal
		var sources: Array=map.human_spawns.duplicate()
		var highest := Vector3(map.nav_nodes[0])
		for node: Vector3 in map.nav_nodes:
			if node.y>highest.y or (is_equal_approx(node.y,highest.y) and node.distance_squared_to(map.stations[0].p)>highest.distance_squared_to(map.stations[0].p)): highest=node
		sources.append(highest)
		for source: Vector3 in sources:
			sim.actors[1].p=source
			var selected: Dictionary=sim._select_task(sim.actors[1],0,35.5)
			check(not selected.is_empty(),"an actual route fits from each spawn/top-floor witness "+str(seed_value))
			if selected.is_empty(): continue
			check(selected.required_seconds<=selected.budget+.000001 and selected.budget<=35.5,"selected route/work fits visible budget")
			check(not Routes.path(source,map.stations[selected.station].p,true,sim.config.map_id).is_empty(),"route is supported through floors and stairs")
		sim.actors[1].p=highest
		sim.actors[1]._next_task=0.0
		sim.step(DT)
		var task: Dictionary=sim.private_for(1).task
		check(not task.is_empty(),"real dispatcher selects from top floor")
		if task.is_empty(): continue
		var chosen: int=task.station
		var budget: float=task.budget
		var result: Dictionary=walk_task(sim)
		check(result.completed and result.collision_free and not result.sprinted,"generated selected route completes via real walking and work "+str(seed_value))
		check(result.seconds<budget and sim.actors[1]._failures==0,"generated travel finishes within displayed budget "+str(seed_value))
		check(sim.task_goal==goal and sim.config.task_interval==36 and sim.actors[1]._deadline==24,"selection preserves goal, cadence and personal deadline")
		cases.append({"seed":seed_value,"floors":map.floor_levels.size(),"station":chosen,"budget":budget,"travel_and_work":result.seconds,"route_meters":task.route_meters,"completed":result.completed})

func walk_task(sim: RefCounted) -> Dictionary:
	var task: Dictionary=sim.private_for(1).task
	var route: PackedVector3Array=Routes.path(sim.actors[1].p,task.p,true,sim.config.map_id)
	var current := 0
	var started: float=sim.elapsed
	var before: int=sim.tasks_done
	var collision_free := true
	var sprinted := false
	for tick: int in range(1800):
		if sim.actors[1]._task.is_empty(): break
		var position: Vector3=sim.actors[1].p
		var near: bool=position.distance_to(task.p)<1.18 and ArenaData.clear_segment(position+Vector3.UP*.65,Vector3(task.p)+Vector3.UP*.65,sim.config.map_id,sim.doors)
		var move := Vector3.ZERO
		if not near and not route.is_empty():
			var delta: Vector3=route[current]-position
			if Vector2(delta.x,delta.z).length()<.10 and absf(delta.y)<.23 and current<route.size()-1:
				current+=1
				delta=route[current]-position
			delta.y=0
			move=delta.normalized()*minf(1.0,delta.length()/(ArenaData.HUMAN_SPEED*DT))
		sim.submit_input(1,int(sim.actors[1]._input_seq)+1,move.rotated(Vector3.UP,-float(sim.actors[1].yaw)),sim.actors[1].yaw,sim.actors[1].pitch,near,false,false,false)
		sim.step(DT)
		collision_free=collision_free and ArenaData.can_fit_human(sim.actors[1].p,ArenaData.HUMAN_HEIGHT,sim.config.map_id,sim.doors)
		sprinted=sprinted or bool(sim.actors[1].sprinting)
	return {"completed":sim.tasks_done==before+1,"seconds":sim.elapsed-started,"collision_free":collision_free,"sprinted":sprinted}

func _doors_and_extension() -> void:
	var sim=make_sim()
	sim.actors[1].p=Vector3(0,0,-8.2)
	var open_estimate: Dictionary=sim._task_route(sim.actors[1].p,0)
	sim.doors.kitchen.angle=0.0;sim.doors.kitchen.target_angle=0.0
	var closed_estimate: Dictionary=sim._task_route(sim.actors[1].p,0)
	check(closed_estimate.door_count>=1 and closed_estimate.required_seconds>open_estimate.required_seconds,"closed door adds a full opening/alignment allowance to the same physical route")
	sim.actors[1]._next_task=0
	sim.step(DT)
	var original_task: Dictionary=sim.actors[1]._task.duplicate(true)
	var handle: Vector3=DoorData.handle_point(sim.door_state.definitions.kitchen,0)
	for turn: int in range(4):
		var delta: Vector3=(handle-Pose.view_origin(sim.actors[1])).normalized()
		sim.submit_input(1,turn+1,Vector3.ZERO,atan2(-delta.x,-delta.z),asin(delta.y),false)
		sim.step(DT)
	check(not sim.private_for(1).interaction.is_empty(),"closed leaf is operable from actual approach")
	sim.action(1,1,"door",sim.actors[1].yaw,sim.actors[1].pitch)
	for tick: int in range(42): sim.step(DT)
	check(sim.doors.kitchen.angle>DoorData.OPEN_ANGLE-.01,"real aimed action opens the route leaf without teleport")
	var travel: Dictionary=walk_task(sim)
	check(travel.completed and travel.collision_free and travel.seconds+42*DT<original_task.budget,"door operation plus actual walk/work fits issued budget")
	# Isolate the extension branch with one far, genuine station on the same
	# validated three-floor geometry. This is an explicit restricted-catalog
	# fixture, not a claim that the generator emits a one-station map.
	var extended=make_sim(Generator.map_id(1),{"task_deadline":24,"task_floor":24})
	var farthest: Dictionary={}
	for source: Vector3 in extended._map_data.nav_nodes:
		if source.y<float(extended._map_data.ceiling)-3.3: continue
		for station_index: int in range(extended._map_data.stations.size()):
			var estimate: Dictionary=extended._task_route(source,station_index)
			if not estimate.is_empty() and estimate.required_seconds>24 and estimate.required_seconds<=35.5 and (farthest.is_empty() or estimate.required_seconds>farthest.required_seconds):
				farthest=estimate;farthest.source=source;farthest.station_index=station_index
	check(not farthest.is_empty(),"real three-floor route exercises a needed extension")
	if not farthest.is_empty():
		extended._map_data.stations=[extended._map_data.stations[farthest.station_index]]
		extended.actors[1].p=farthest.source
		extended.actors[1]._next_task=0
		extended.step(DT)
		var task: Dictionary=extended.actors[1]._task.duplicate(true)
		check(not task.is_empty() and task.extended and task.budget==task.required_seconds and task.base_deadline==24,"only issued visible budget extends, personal penalty clock remains intact")
		if not task.is_empty():
			var result: Dictionary=walk_task(extended)
			check(result.completed and result.collision_free and result.seconds<task.budget,"extended real route is physically completable without sprint")
			cases.append({"scope":"restricted-station extension witness","budget":task.budget,"seconds":result.seconds,"route_meters":task.route_meters,"completed":result.completed})

func _pending_and_deadlines() -> void:
	var sim=make_sim(Generator.map_id(2))
	var goal: int=sim.task_goal
	sim.actors[1]._next_task=0
	sim.actors[1].p.y+=.4
	sim.actors[1].grounded=false
	sim.actors[1].velocity.y=1.0
	sim.step(DT)
	check(sim.actors[1]._task.is_empty() and sim.private_for(1).task_status.pending,"airborne dispatch waits rather than assigning a route from a projected floor")
	for tick: int in range(50): sim.step(DT)
	check(not sim.actors[1]._task.is_empty() and not sim.private_for(1).task_status.pending,"grounding retries within the same fixed slot")
	check(sim.task_goal==goal and sim.actors[1]._next_task==36.0,"retry never lowers collective goal or reschedules cadence")
	# Deliberately invalid test position supplies no walkable origin. Time out
	# the pending slot directly to prove it cannot create an impossible task or
	# lower a collective target; movement authority is covered separately.
	var no_route=make_sim()
	var fixed_goal: int=no_route.task_goal
	no_route.actors[1].p=Vector3(100,0,100)
	no_route.actors[1]._task_dispatch={"preferred":0,"end":10.0}
	no_route._dispatch_task(1)
	check(no_route.actors[1]._task.is_empty() and not no_route.actors[1]._task_dispatch.is_empty(),"no route stays pending without an impossible obligation")
	no_route.elapsed=9.0
	no_route._dispatch_task(1)
	check(no_route.actors[1]._task_dispatch.is_empty() and no_route.task_goal==fixed_goal and no_route.actors[1]._failures==0,"expired impossible slot is not a penalty or goal-reduction exploit")
	var late=make_sim(Generator.map_id(2),{"round_seconds":120,"task_goal":0})
	late.elapsed=110.99
	late.actors[1]._next_task=111.0
	late.step(DT)
	check(late.actors[1]._task.is_empty() and late.actors[1]._task_dispatch.is_empty() and late.task_goal==2,"original conservative end-of-round cutoff and two-thirds goal survive variable geometry")
	var own: Dictionary=sim.private_for(1)
	var public: Dictionary=sim.public_snapshot()
	check(not public.actors[1].has("task") and not public.actors[1].has("task_status") and not sim.private_for(2).has("task_status"),"route, budget and pending status stay private to their human")
	check(own.task.remaining<=own.task.budget and own.task.required_seconds<=own.task.budget,"private countdown describes an achievable issued budget")
