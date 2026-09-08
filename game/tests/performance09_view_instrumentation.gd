extends RefCounted
## Source-only factories for the live profiler. Production files are read, never
## rewritten. Exact dependency substitutions retain the original callback order,
## ActorView inheritance, scene setup and signal connections.

static func _write(path: String, source: String, files: Dictionary) -> bool:
	var file := FileAccess.open(path,FileAccess.WRITE)
	if file==null:return false
	file.store_string(source);file.close()
	files[path]=FileAccess.get_sha256(path)
	return true

static func _substitute(source_path: String, find_text: String, replacement: String, output: String, files: Dictionary, edits: Array) -> bool:
	if not FileAccess.file_exists(source_path):return false
	var source := FileAccess.get_file_as_string(source_path)
	if source.count(find_text)!=1:return false
	var changed := source.replace(find_text,replacement)
	# No regex, parser rewrite or broad string substitutions. Reversing this one
	# declared replacement must recover the original bytes as loaded by Godot.
	if changed.replace(replacement,find_text)!=source:return false
	edits.append({"source":source_path,"source_sha256":FileAccess.get_sha256(source_path),"find":find_text,"replacement":replacement,"output":output,"reversible_exact":true})
	return _write(output,changed,files)

static func _wrapper(base: String, recorder: String, scopes: Array) -> String:
	var source := "extends "+JSON.stringify(base)+"\nconst Recorder = preload("+JSON.stringify(recorder)+")\n"
	for scope: Array in scopes:
		# [method, signature, arguments, return type, label expression]
		source+="\nfunc %s(%s) -> %s:\n\tvar trace: RefCounted=Recorder.current\n\tvar label: String=%s\n\tvar tracked: bool=trace.enter(label) if trace!=null else false\n"%[scope[0],scope[1],scope[3],scope[4]]
		if scope[3]=="void":source+="\tsuper.%s(%s)\n"%[scope[0],scope[2]]
		else:source+="\tvar result: %s=super.%s(%s)\n"%[scope[3],scope[0],scope[2]]
		source+="\tif trace!=null:trace.leave(label,tracked)\n"
		if scope[3]!="void":source+="\treturn result\n"
	return source

static func _close_exit(source: String, output: String, files: Dictionary, edits: Array) -> bool:
	# Main owns the real music shutdown and network grace. Change only its final
	# exit code in this test copy, so a failed gate cannot become exit 0 there.
	if not FileAccess.file_exists(source):return false
	var original := FileAccess.get_file_as_string(source)
	var newline := "\r\n" if original.contains("\r\n") else "\n"
	var close_text := "\t\t_cleanup_server()"+newline+"\t\tget_tree().quit()"
	return _substitute(source,close_text,"\t\t_cleanup_server()"+newline+"\t\tget_tree().quit(int(get_tree().get_meta(\"profile09_exit_code\",0)))",output,files,edits)

static func prepare_close_only() -> Dictionary:
	var directory := ProjectSettings.globalize_path("res://../work/perf09-view-instrumentation/"+str(OS.get_process_id())).simplify_path()
	if DirAccess.make_dir_recursive_absolute(directory)!=OK:return {"error":"cannot create test close directory"}
	var files: Dictionary={};var edits: Array=[]
	var main := directory.path_join("main-close.gd")
	if not _close_exit("res://scripts/main.gd",main,files,edits):return {"error":"Main shutdown exit substitution must match exactly once"}
	var script: Script=load(main)
	if script==null or not script.can_instantiate():return {"error":"Main shutdown copy parse"}
	return {"main":script,"manifest":{"directory":directory,"files_sha256":files,"substitutions":edits,"source_only":true,"runtime_files_modified":false,"view_scopes":false}}

static func prepare(collector: RefCounted) -> Dictionary:
	var directory := ProjectSettings.globalize_path("res://../work/perf09-view-instrumentation/"+str(OS.get_process_id())).simplify_path()
	if DirAccess.make_dir_recursive_absolute(directory)!=OK:return {"error":"cannot create isolated instrumentation directory"}
	var files: Dictionary={}
	var substitutions: Array=[]
	var recorder := directory.path_join("recorder.gd")
	if not _write(recorder,"extends RefCounted\nstatic var current: RefCounted\n",files):return {"error":"recorder write"}
	var actor := directory.path_join("actor.gd")
	var actor_scopes: Array=[
		["update_state","data: Dictionary, dt: float","data,dt","void","\"Actor.\"+actor_role+\".update_state\""],
		["_apply_human_pose","data: Dictionary, dt: float, authoritative_data: Dictionary={}","data,dt,authoritative_data","void","\"Actor.human.pose\""],
		["_apply_human_colliders","data: Dictionary","data","void","\"Actor.human.colliders\""],
		["_process","dt: float","dt","void","\"Actor.\"+actor_role+\".voice_process\""]]
	if not _write(actor,_wrapper("res://scripts/actor_view.gd",recorder,actor_scopes),files):return {"error":"actor wrapper write"}
	var world_base := directory.path_join("world-base.gd")
	if not _substitute("res://scripts/world.gd","const ActorModel = preload(\"res://scripts/actor_view.gd\")","const ActorModel = preload("+JSON.stringify(actor)+")",world_base,files,substitutions):return {"error":"World actor factory substitution must match exactly once"}
	var world := directory.path_join("world.gd")
	var world_scopes: Array=[
		["_process","dt: float","dt","void","\"World._process\""],
		["sync_actors","data: Dictionary, local_id: int, dt: float, critical_human_id: int=0","data,local_id,dt,critical_human_id","void","\"World.sync_actors\""],
		["sync_doors","states: Dictionary, dt: float","states,dt","void","\"World.sync_doors\""],
		["sync_pickups","pickups: Dictionary, dt: float=1.0/60.0","pickups,dt","void","\"World.sync_pickups\""]]
	if not _write(world,_wrapper(world_base,recorder,world_scopes),files):return {"error":"world wrapper write"}
	var client_base := directory.path_join("client-base.gd")
	if not _substitute("res://scripts/client.gd","const WorldScript = preload(\"res://scripts/world.gd\")","const WorldScript = preload("+JSON.stringify(world)+")",client_base,files,substitutions):return {"error":"Client world factory substitution must match exactly once"}
	var client := directory.path_join("client.gd")
	var client_scopes: Array=[
		["_process","dt: float","dt","void","\"Client._process\""],
		["_physics_process","dt: float","dt","void","\"Client._physics_process\""],
		["_flush_game_hud","","","void","\"Client._flush_game_hud\""],
		["_snapshot","data: Dictionary","data","void","\"Client._snapshot\""],
		["_private","data: Dictionary","data","void","\"Client._private\""]]
	if not _write(client,_wrapper(client_base,recorder,client_scopes),files):return {"error":"client wrapper write"}
	var main_factory := directory.path_join("main-factory.gd")
	if not _substitute("res://scripts/main.gd","load(\"res://scripts/client.gd\")","load("+JSON.stringify(client)+")",main_factory,files,substitutions):return {"error":"Main client factory substitution must match exactly once"}
	var main := directory.path_join("main.gd")
	if not _close_exit(main_factory,main,files,substitutions):return {"error":"Main shutdown exit substitution must match exactly once"}
	var recorder_script: Script=load(recorder)
	if recorder_script==null or not recorder_script.can_instantiate():return {"error":"recorder parse"}
	# Explicitly compile every generated dependency before Main starts. A dynamic
	# Client load would otherwise defer its parse error until scene creation.
	for path: String in [actor,world_base,world,client_base,client,main]:
		var script: Script=load(path)
		if script==null or not script.can_instantiate():return {"error":"generated source failed parse: "+path}
	recorder_script.current=collector
	return {"main":load(main),"recorder":recorder_script,"manifest":{"directory":directory,"files_sha256":files,"substitutions":substitutions,"source_only":true,"runtime_files_modified":false,"callback_order":"same nodes and callbacks; super implementations retain original order","limits":"Actor pose includes imported skin work; World process includes visibility/rays; native render/audio threads outside these callbacks are not timed"}}
