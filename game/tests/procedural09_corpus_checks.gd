extends SceneTree
## Bounded batches: independent processes can compare every geometry fingerprint.
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")

func _initialize() -> void:
	var batch:=0
	var batch_size:=100
	var repeat_run:=false
	var output := ProjectSettings.globalize_path("res://../outputs/%s-generated-house/corpus" % str(ProjectSettings.get_setting("application/config/version")))
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--batch="): batch=int(argument.trim_prefix("--batch="))
		if argument.begins_with("--batch-size="): batch_size=int(argument.trim_prefix("--batch-size="))
		if argument=="--repeat": repeat_run=true
		if argument.begins_with("--output="): output=argument.trim_prefix("--output=")
	if batch_size<1 or batch_size>100 or 1000%batch_size!=0: quit(2);return
	if batch<0 or batch>=1000/batch_size: quit(2);return
	var path: String=output+"/batch-%02d%s.json"%[batch,"-repeat" if repeat_run else ""]
	if FileAccess.file_exists(path):
		printerr("Corpus evidence already exists; choose a new --output directory")
		quit(2);return
	var cases: Array=[]
	var failures: Array=[]
	var hashes: Dictionary={}
	var started:=Time.get_ticks_msec()
	var source_hashes: Dictionary={}
	for script_name: String in ["procedural_house","house_validation","furniture_blueprint","navigation_geometry","door_geometry","pickup_placement","pickup_support_geometry"]:
		source_hashes[script_name]=FileAccess.get_sha256("res://scripts/%s.gd"%script_name)
	for index: int in range(batch*batch_size,(batch+1)*batch_size):
		# Half consecutive seeds, half distributed across the supported domain.
		var seed_value: int=index+1 if index<500 else ((index-499)*15485863)%2147483646+1
		var data: Dictionary=Generator.new().generate(seed_value)
		var report: Dictionary=Validation.validate(data)
		var fingerprint: String=Validation.fingerprint(data)
		var shell: Array=[]
		for structure: Dictionary in data.structures:
			if str(structure.kind) in ["wall","floor","ceiling","step"]: shell.append([structure.kind,structure.box])
		# Exclude seed, names, colors and furnishings when checking real layout
		# diversity. Cosmetic variety alone is not a new house distribution.
		var layout_signature: String=JSON.stringify(Validation._canonical({"bounds":data.bounds,"floors":data.floor_levels,"shell":shell})).sha256_text()
		var errors: Array=report.errors.duplicate()
		if hashes.has(layout_signature): errors.append("Repeated structural layout")
		hashes[layout_signature]=true
		if data.id!=Generator.map_id(seed_value): errors.append("Seed identity changed")
		var entry: Dictionary={"seed":seed_value,"fingerprint":fingerprint,"layout_signature":layout_signature,"rooms":data.rooms.size(),"floors":data.floor_levels.size(),"errors":errors}
		cases.append(entry)
		if not errors.is_empty(): failures.append(entry)
	DirAccess.make_dir_recursive_absolute(output)
	var file:=FileAccess.open(path,FileAccess.WRITE)
	file.store_string(JSON.stringify({"batch":batch,"batch_size":batch_size,"count":cases.size(),"source_hashes":source_hashes,"elapsed_ms":Time.get_ticks_msec()-started,"failures":failures,"cases":cases},"\t"));file.close()
	for failure: Dictionary in failures: print("FAIL seed=%d %s"%[failure.seed,failure.errors])
	print("procedural09_corpus batch=%d checks=%d failures=%d"%[batch,cases.size(),failures.size()])
	quit(0 if failures.is_empty() else 1)
