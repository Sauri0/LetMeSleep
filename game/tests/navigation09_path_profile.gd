extends SceneTree
## SOURCE-ONLY diagnostic. The frozen reference differs from MapNavigation
## 229fd8b9... only by removing class_name. Instrumentation decorates that exact
## source; it never replaces production navigation or changes a route predicate.
const Current = preload("res://scripts/map_navigation.gd")
const Reference = preload("res://tests/navigation09_reference.gd")
const ORIGINAL_SHA := "229fd8b9836259a1ff36e8ffc5553418050a7bb3140fd368af7d6a456bd6c265"
var input_path := ""
var output_path := ""
var repetitions := 3
var instrument_current := false
var failures: Array[String] = []
var checks := 0
var spans: Dictionary = {}
var query_rows: Array = []

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--input="):input_path=argument.trim_prefix("--input=")
		if argument.begins_with("--report="):output_path=argument.trim_prefix("--report=")
		if argument.begins_with("--repetitions="):repetitions=clampi(int(argument.trim_prefix("--repetitions=")),1,12)
		if argument=="--instrument-current":instrument_current=true
	_run.call_deferred()

func _check(condition: bool, message: String) -> void:
	checks+=1
	if not condition:failures.append(message);printerr("NAVIGATION09_FAIL "+message)

func _vector(values: Array) -> Vector3:
	return Vector3(float(values[0]),float(values[1]),float(values[2]))

func _instrumented() -> GDScript:
	var source_path := "res://scripts/map_navigation.gd" if instrument_current else "res://tests/navigation09_reference.gd"
	var source := FileAccess.get_file_as_string(source_path).replace("\r\n","\n").replace("class_name MapNavigation\n","")
	# Move the byte-identical search body into a test-only span. Its inputs and
	# strict tie ordering are unchanged. Every result is checked against both
	# the untouched reference and current production implementation below.
	var marker := "\tvar count: int = graph.nodes.size()\n"
	var start := source.find(marker,source.find("static func path("))
	if start<0:return null
	var body := source.substr(start)
	source=source.substr(0,start)+"\treturn _search(origin,destination,starts,ends,graph)\n\nstatic func _search(origin: Vector3, destination: Vector3, starts: Array[int], ends: Array[int], graph: Dictionary) -> PackedVector3Array:\n\tvar empty := PackedVector3Array()\n"+body
	var methods: Array = [
		["path","from: Vector3, to: Vector3, human: bool, map_id: String = \"house\"","PackedVector3Array","from,to,human,map_id"],
		["_graph","human: bool, map_id: String","Dictionary","human,map_id"],
		["_project_human","point: Vector3, geometry: Dictionary","Vector3","point,geometry"],
		["_connections","point: Vector3, graph: Dictionary","Array[int]","point,graph"],
		["_segment","from: Vector3, to: Vector3, geometry: Dictionary","bool","from,to,geometry"],
		["_search","origin: Vector3, destination: Vector3, starts: Array[int], ends: Array[int], graph: Dictionary","PackedVector3Array","origin,destination,starts,ends,graph"]]
	for method: Array in methods:
		var name: String=method[0]
		source=source.replace("static func "+name+"(","static func _raw"+name+"(")
		source+="\nstatic func %s(%s) -> %s:\n\t_trace_enter(\"%s\")\n\tvar result: %s = _raw%s(%s)\n\t_trace_leave(\"%s\")\n\treturn result\n"%[name,method[1],method[2],name,method[2],name,method[3],name]
	source+='''
static var _trace_enabled := false
static var _trace_stack: Array = []
static var _trace_rows: Dictionary = {}
static func _trace_enter(label: String) -> void:
	if _trace_enabled:_trace_stack.append([label,Time.get_ticks_usec(),0])
static func _trace_leave(label: String) -> void:
	if not _trace_enabled:return
	var ended := Time.get_ticks_usec()
	var frame: Array = _trace_stack.pop_back()
	assert(frame[0]==label)
	var elapsed: int=ended-int(frame[1])
	var parent: String="outside"
	if not _trace_stack.is_empty():
		parent=str(_trace_stack[-1][0]);_trace_stack[-1][2]+=elapsed
	var key: String=parent+" -> "+label
	if not _trace_rows.has(key):_trace_rows[key]={"calls":0,"inclusive_us":0,"residual_us":0}
	_trace_rows[key].calls+=1
	_trace_rows[key].inclusive_us+=elapsed
	_trace_rows[key].residual_us+=elapsed-int(frame[2])
'''
	var script := GDScript.new()
	script.source_code=source
	if script.reload()!=OK:return null
	return script

func _distribution(values: Array) -> Dictionary:
	var sorted := values.duplicate();sorted.sort()
	if sorted.is_empty():return {}
	var total := 0.0
	for value: float in sorted:total+=value
	return {"samples":sorted.size(),"total_ms":total,"mean_ms":total/sorted.size(),"p50_ms":sorted[int(sorted.size()*.5)],"p90_ms":sorted[int(sorted.size()*.9)],"p99_ms":sorted[mini(sorted.size()-1,int(sorted.size()*.99))],"max_ms":sorted[-1]}

func _run() -> void:
	if not FileAccess.file_exists("res://tests/navigation09_reference.gd") or not FileAccess.file_exists(input_path):
		_check(false,"requires source reference and --input exact query trace");_finish({});return
	var input: Variant=JSON.parse_string(FileAccess.get_file_as_string(input_path))
	if not input is Dictionary:
		_check(false,"input must be profile JSON");_finish({});return
	var queries: Array=input.get("cpu_attribution",{}).get("path_queries",[])
	_check(not queries.is_empty(),"profile contains actual route rebuild requests")
	_check(input.get("failures",["missing"]).is_empty(),"source profile completed without failures")
	var traced := _instrumented()
	if traced==null:_check(false,"instrumented reference parses");_finish({});return
	var warmup: Array=[]
	var maps: Dictionary={}
	for query: Dictionary in queries:maps[str(query.map_id)]=true
	for map_id: String in maps:
		for human: bool in [false,true]:
			var before := Time.get_ticks_usec()
			var expected: Dictionary=Reference.graph_info(human,map_id)
			var old_ms := float(Time.get_ticks_usec()-before)/1000.0
			before=Time.get_ticks_usec()
			_check(Current.graph_info(human,map_id)==expected,"current graph exact "+map_id+" "+str(human))
			var new_ms := float(Time.get_ticks_usec()-before)/1000.0
			_check(traced.graph_info(human,map_id)==expected,"instrumented graph exact "+map_id+" "+str(human))
			warmup.append({"map_id":map_id,"human":human,"reference_ms":old_ms,"current_ms":new_ms})
	var expected_routes: Array=[]
	for index: int in range(queries.size()):
		var query: Dictionary=queries[index]
		var route: PackedVector3Array=Reference.path(_vector(query.from),_vector(query.to),bool(query.human),str(query.map_id))
		expected_routes.append(route)
		_check(Current.path(_vector(query.from),_vector(query.to),bool(query.human),str(query.map_id))==route,"production exact captured request "+str(index))
	traced._trace_enabled=true
	for repetition: int in range(repetitions):
		for index: int in range(queries.size()):
			var query: Dictionary=queries[index]
			traced._trace_rows={}
			var result: PackedVector3Array=traced.path(_vector(query.from),_vector(query.to),bool(query.human),str(query.map_id))
			_check(result==expected_routes[index],"instrumentation exact query %d repetition %d"%[index,repetition])
			query_rows.append({"query":index,"repetition":repetition,"human":query.human,"route_points":result.size(),"spans":traced._trace_rows.duplicate(true)})
			for key: String in traced._trace_rows:
				if not spans.has(key):spans[key]={"calls":0,"inclusive":[],"residual":[]}
				var row: Dictionary=traced._trace_rows[key]
				spans[key].calls+=int(row.calls)
				spans[key].inclusive.append(float(row.inclusive_us)/1000.0)
				spans[key].residual.append(float(row.residual_us)/1000.0)
	traced._trace_enabled=false
	var summarized: Dictionary={}
	for key: String in spans:
		summarized[key]={"calls":spans[key].calls,"inclusive_per_query":_distribution(spans[key].inclusive),"residual_per_query":_distribution(spans[key].residual)}
	# Uninstrumented ABBA; same exact query order, graph warm, output compared.
	var timing: Array=[]
	for label: String in ["reference","current","current","reference"]:
		var script: Script=Reference if label=="reference" else Current
		var samples: Array=[]
		for index: int in range(queries.size()):
			var query: Dictionary=queries[index]
			var from := _vector(query.from);var to := _vector(query.to)
			var before := Time.get_ticks_usec()
			var route: PackedVector3Array=script.path(from,to,bool(query.human),str(query.map_id))
			samples.append(float(Time.get_ticks_usec()-before)/1000.0)
			_check(route==expected_routes[index],label+" ABBA exact "+str(index))
		timing.append({"implementation":label,"path":_distribution(samples)})
	_finish({"input_sha256":FileAccess.get_sha256(input_path),"input_path":input_path,"profile_runtime_sha256":input.get("source_sha256",{}),"reference_original_sha256":ORIGINAL_SHA,"reference_file_sha256":FileAccess.get_sha256("res://tests/navigation09_reference.gd"),"current_sha256":FileAccess.get_sha256("res://scripts/map_navigation.gd"),"geometry_sha256":FileAccess.get_sha256("res://scripts/navigation_geometry.gd"),"query_count":queries.size(),"repetitions":repetitions,"warmup":warmup,"spans":summarized,"queries":query_rows,"abba":timing,"contract":{"scope":"exact real bot path requests; immutable graph; source-only replay, not live FPS","search":"byte-identical A* search body extracted into test-only _search; includes queue setup and reconstruction","residual":"inclusive minus measured child spans; includes wrapper bookkeeping; connection residual includes sorting and filtering","no_sum":"inclusive hierarchy overlaps; use child edges","trace_precision":"Vector3 stored as numeric triples from original call arguments","geometry_unchanged":true}})

func _finish(report: Dictionary) -> void:
	report.checks=checks;report.failures=failures
	report.instrumented_implementation="current" if instrument_current else "reference"
	if not output_path.is_empty():
		var file:=FileAccess.open(output_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
		else:_check(false,"cannot write report")
	print("NAVIGATION09_PROFILE checks=%d failures=%d"%[checks,failures.size()])
	quit.call_deferred(0 if failures.is_empty() else 1)
