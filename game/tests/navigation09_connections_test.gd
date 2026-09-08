extends SceneTree
## Exact differential against the pre-optimization navigation implementation.
const Current = preload("res://scripts/map_navigation.gd")
const Reference = preload("res://tests/navigation09_reference.gd")
const Geometry = preload("res://scripts/navigation_geometry.gd")
var checks := 0
var failures: Array[String] = []
var report_path := ""

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):report_path=argument.trim_prefix("--report=")
	_run.call_deferred()

func _check(condition: bool, message: String) -> void:
	checks+=1
	if not condition:failures.append(message);printerr("NAVIGATION09_FAIL "+message)

func _run() -> void:
	var totals: Dictionary={}
	for map_id: String in ["house","house-v1-1","house-v1-2","lobby"]:
		for human: bool in [false,true]:
			var before: Dictionary=Reference._graph(human,map_id)
			var after: Dictionary=Current._graph(human,map_id)
			_check(before.nodes==after.nodes and before.edges==after.edges and before.invalid_edges==after.invalid_edges,"graph exact "+map_id+" "+str(human))
			var starts := checks
			for index: int in range(before.nodes.size()):
				var point: Vector3=before.nodes[index]
				# Close offsets include both sides of same-floor and reachability
				# boundaries. The old implementation remains the expected result.
				for offset: Vector3 in [Vector3.ZERO,Vector3(.00001,0,0),Vector3(-.00001,0,0),Vector3(0,.25,0),Vector3(0,.25001,0)]:
					var query := point+offset
					_check(Current._connections(query,after)==Reference._connections(query,before),"connections exact %s/%s/%d/%s"%[map_id,human,index,offset])
				# Compare full paths across levels, through junctions and both
				# directions; do not stop at comparing nearest-node candidates.
				for delta: int in [1,17]:
					var destination: Vector3=before.nodes[posmod(index+delta,before.nodes.size())]
					_check(Current.path(point,destination,human,map_id)==Reference.path(point,destination,human,map_id),"full path exact %s/%s/%d/%d"%[map_id,human,index,delta])
			totals[map_id+("/human" if human else "/mosquito")]=checks-starts
	# Ties are especially important: sorting equal distances must produce the
	# same chosen first three nodes. Include repeated nodes and shuffled order.
	var data: Dictionary={"obstacles":[],"floors":[],"steps":[],"half_x":100.0,"half_z":100.0,"ceiling":100.0}
	for human: bool in [false,true]:
		var graph: Dictionary=Geometry.create(data,human)
		for count: int in [3,4,8,16,32,64]:
			var nodes: Array[Vector3]=[]
			var center := Vector3.ZERO if human else Vector3(0,1.2,0)
			for index: int in range(count):
				var offsets: Array[Vector3]=[Vector3.LEFT,Vector3.RIGHT,Vector3.FORWARD,Vector3.BACK]
				nodes.append(center+offsets[posmod(index*3,4)])
			graph.nodes=nodes
			for query: Vector3 in [center,center+Vector3(.0000001,0,0),center+Vector3(0,0,.0000001)]:
				_check(Current._connections(query,graph)==Reference._connections(query,graph),"strict ties and repeated nodes %s/%d/%s"%[human,count,query])
	var report: Dictionary={"checks":checks,"failures":failures,"map_checks":totals,"scope":"exact connections and full routes; same-floor borders; ties and repeated nodes; no navigation predicate changed","current_sha256":FileAccess.get_sha256("res://scripts/map_navigation.gd"),"reference_sha256":FileAccess.get_sha256("res://tests/navigation09_reference.gd")}
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
	print("NAVIGATION09_CONNECTIONS checks=%d failures=%d"%[checks,failures.size()])
	quit.call_deferred(0 if failures.is_empty() else 1)
