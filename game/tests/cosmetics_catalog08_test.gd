extends SceneTree
## Exhaustive discrete geometry-class sanitizer coverage. This does NOT inspect
## meshes or certify that the excluded color fields are geometrically invariant.
const CosmeticsData=preload("res://scripts/cosmetics.gd")
var checks:=0
var failures:=0
func check(value: bool,label: String) -> void:
	checks+=1
	if not value:failures+=1;printerr("COSMETICS_CATALOG08_FAIL "+label)
func _initialize() -> void:
	var output: String="res://../work/cosmetics-catalog08.json"
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--catalog="):output=argument.trim_prefix("--catalog=")
	# Binary script export can omit the original .gd. The release runner binds
	# this data-only report to the exact EXE and records external source hashes.
	var source_hash: Variant = FileAccess.get_sha256("res://scripts/cosmetics.gd") if FileAccess.file_exists("res://scripts/cosmetics.gd") else null
	var catalog: Dictionary={"schema":2,"game_version":"0.7.0","protocol":preload("res://scripts/invitation.gd").PROTOCOL,"cosmetics_source_sha256":source_hash,"roles":{},"validation":{"scope":"Sanitizer/domain enumeration only; no mesh, fit, overlap, animation or visibility claim.","excluded_color_fields_require_renderer_invariance":true,"method":"Exhaustive geometry tuples plus fieldwise valid-domain separability; total color product is represented symbolically, not claimed to have been rendered.","separability_proof":"Canonical profiles contain every role field. sanitize computes each clean[key] solely as _index(profile[key], option_count(role,key)); bounds depend only on role/key, and no other selected value changes that result. Every valid scalar is a fixed point, therefore their full Cartesian product is accepted without exclusions. Legacy face fallback is inactive when all current fields are present.","class_definition":"Distinct geometry-selector tuples; no claim that every tuple produces a unique visible mesh."}}
	for role: String in ["human","mosquito"]:
		var keys:=CosmeticsData.category_keys(role)
		var geometry:=CosmeticsData.geometry_keys(role)
		var profiles:=1
		var geometries:=1
		var domains: Dictionary={}
		var colors: Array[String]=[]
		for key: String in keys:
			var count:=CosmeticsData.option_count(role,key)
			var ids: Array[int]=[]
			for id: int in range(count):ids.append(id)
			domains[key]={"ids":ids,"labels":CosmeticsData.option_names(role,key),"geometry_class_member":key in geometry}
			profiles*=count
			if key in geometry:geometries*=count
			else:colors.append(key)
		check(geometries==(26244 if role=="human" else 2187),"exact geometry-class domain product "+role)
		check(profiles==(5668704 if role=="human" else 78732),"exact complete appearance-domain product "+role)
		var classes: Array=[]
		var unique: Dictionary={}
		var canonical: Dictionary=CosmeticsData.appearance_for({},role)
		# Each valid scalar is independent of the all-maximum context. Exhaustive
		# geometry tuples below then exercise every discrete part combination.
		var maximum: Dictionary=canonical.duplicate(true)
		for key: String in keys:maximum[key]=CosmeticsData.option_count(role,key)-1
		for key: String in keys:
			for value: int in range(CosmeticsData.option_count(role,key)):
				var proposal:=maximum.duplicate(true)
				proposal[key]=value
				check(CosmeticsData.appearance_for({role:proposal},role)==proposal,"valid field is never suppressed by other maximum IDs "+role+" "+key)
		for ordinal: int in range(geometries):
			var remainder:=ordinal
			var appearance: Dictionary=canonical.duplicate(true)
			var indices: Array[int]=[]
			for key: String in geometry:
				var count:=CosmeticsData.option_count(role,key)
				appearance[key]=remainder%count
				indices.append(remainder%count)
				remainder=int(remainder/count)
			var class_id:=CosmeticsData.geometry_class_id(role,appearance)
			check(not unique.has(class_id),"unique stable ID "+class_id)
			unique[class_id]=true
			# All 216 human /36 mosquito color tuples occur among this enumeration;
			# no color participates in any geometry selector or validation bound.
			var color_ordinal:=ordinal
			for key: String in colors:
				var count:=CosmeticsData.option_count(role,key)
				appearance[key]=color_ordinal%count
				color_ordinal=int(color_ordinal/count)
			check(CosmeticsData.appearance_for({role:appearance},role)==appearance,"complete tuple is not silently clamped "+class_id)
			check(CosmeticsData.geometry_class_id(role,appearance)==class_id,"classification excludes only documented color fields "+class_id)
			classes.append({"id":class_id,"indices":indices})
		catalog.roles[role]={"ordered_keys":keys,"domains":domains,"geometry_keys":geometry,"color_only_keys":colors,"geometry_class_count":geometries,"valid_profile_count":profiles,"profiles_per_geometry_class":int(profiles/geometries),"geometry_classes":classes}
	catalog.paired_saved_profile_count=int(catalog.roles.human.valid_profile_count)*int(catalog.roles.mosquito.valid_profile_count)
	var target:=FileAccess.open(output,FileAccess.WRITE)
	check(target!=null,"audit catalog file is writable")
	catalog.validation["checks"]=checks
	catalog.validation["failures"]=failures
	catalog.validation["passed"]=failures==0
	if target!=null:
		target.store_string(JSON.stringify(catalog,"\t",false)+"\n")
		target.close()
	print("COSMETICS_CATALOG08_RESULT checks=%d failures=%d human_classes=26244 mosquito_classes=2187 human_profiles=5668704 mosquito_profiles=78732 output=%s" % [checks,failures,ProjectSettings.globalize_path(output)])
	quit(failures)
