extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
var checks:=0
var failures: Array[String]=[]

func check(ok: bool, message: String) -> void:
	checks+=1
	if not ok: failures.append(message)

func _initialize() -> void:
	var cases: Array=[]
	var corpus: Array[int]=[1,2,7,31,97,257,997,2026,65537,1234567,2147483646]
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--seed="): corpus=[int(argument.trim_prefix("--seed="))]
	for seed_value: int in corpus:
		var generated: Dictionary=Generator.new().generate(seed_value)
		var report: Dictionary=Validation.validate(generated)
		check(report.passed,"Seed %d: %s"%[seed_value,report.errors])
		check(Validation.fingerprint(generated)==Validation.fingerprint(Generator.new().generate(seed_value)),"Determinism %d"%seed_value)
		check(Generator.parse_seed(generated.id)==seed_value,"Seed identity %d"%seed_value)
		var count:=0
		for structure: Dictionary in generated.structures:
			if structure.kind=="furniture": count+=1
		check(count>=48,"Furniture richness %d: %d"%[seed_value,count])
		cases.append({"seed":seed_value,"fingerprint":Validation.fingerprint(generated),"furniture":count,"report":report})
	for invalid: String in ["house-v1-0","house-v1-01","house-v2-1","house-v1--5","house-v1-2147483647"]:
		check(Generator.parse_seed(invalid)==-1,"Reject malformed seed "+invalid)
	var file:=FileAccess.open("res://../work/procedural09-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"cases":cases},"\t"));file.close()
	for failure: String in failures: print("FAIL "+failure)
	print("procedural09 checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
