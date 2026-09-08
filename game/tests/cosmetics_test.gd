extends SceneTree
const CosmeticsData=preload("res://scripts/cosmetics.gd")
var checks:=0
var failures:=0
func check(value: bool,label: String) -> void:
	checks+=1
	if not value:failures+=1;printerr("COSMETICS_FAIL "+label)

func _initialize() -> void:
	var baseline: Dictionary=CosmeticsData.sanitize({})
	check(baseline.human.size()==12 and baseline.mosquito.size()==9,"role-specific exact field counts")
	var fresh: Dictionary=CosmeticsData.default_profile()
	check(fresh.human.accessory==3 and fresh.human.eyes==1 and fresh.human.mouth==1 and fresh.human.brows==1,"fresh nightcap and sleepy pieces preserved")
	check(fresh.human.hair_color==0 and fresh.human.mustache==0 and fresh.human.beard==0,"new facial hair is opt-in")
	check(CosmeticsData.sanitize(fresh)==fresh,"default profile is canonical")
	for invalid: Variant in [null,4,"human",[],true,{"human":null},{"mosquito":[]}]:
		check(CosmeticsData.sanitize(invalid)==baseline,"malformed role/container defaults")
	for role: String in ["human","mosquito"]:
		for key: String in CosmeticsData.category_keys(role):
			var proposal:=baseline.duplicate(true)
			proposal[role][key]=CosmeticsData.option_count(role,key)-1
			check(CosmeticsData.sanitize(proposal)==proposal,"maximum valid index "+role+" "+key)
			for invalid: Variant in [-1,CosmeticsData.option_count(role,key),"1",1.0,true,null,NAN,INF,{},[]]:
				proposal[role][key]=invalid
				check(CosmeticsData.sanitize(proposal)[role][key]==0,"strict integer bounds "+role+" "+key)
		for face: int in range(3):
			var old: Dictionary={role:{"face":face,"color":5,"accessory":2,"hair":1,"footwear":2,"accent":4,"outfit":2}}
			var clean:=CosmeticsData.sanitize(old)
			check(clean[role].eyes==face and clean[role].mouth==face and clean[role].brows==face,"legacy face splits without changing selected style "+role+str(face))
			check(clean[role].color==5 and clean[role].accessory==2 and clean[role].hair==1 and clean[role].footwear==2 and clean[role].accent==4 and clean[role].outfit==2,"all prior nonfacial IDs survive migration")
			check(not clean[role].has("face"),"legacy selector never replicates")
			old[role].eyes=0
			old[role].mouth="2"
			clean=CosmeticsData.sanitize(old)
			check(clean[role].eyes==0 and clean[role].mouth==0 and clean[role].brows==face,"explicit fields win; invalid explicit values do not inherit legacy")
		for eyes: int in range(3):
			for mouth: int in range(3):
				for brows: int in range(3):
					var clean:=CosmeticsData.sanitize({role:{"eyes":eyes,"mouth":mouth,"brows":brows}})
					check(clean[role].eyes==eyes and clean[role].mouth==mouth and clean[role].brows==brows,"all27 independent piece combinations "+role)
	for mustache: int in range(3):
		for beard: int in range(3):
			for color: int in range(6):
				var clean:=CosmeticsData.sanitize({"human":{"mustache":mustache,"beard":beard,"hair_color":color},"mosquito":{"mustache":mustache,"beard":beard,"hair_color":color}})
				check(clean.human.mustache==mustache and clean.human.beard==beard and CosmeticsData.human_hair_color(clean.human)==CosmeticsData.HAIR_PALETTE[color],"independent facial hair shares one validated color")
				check(not clean.mosquito.has("mustache") and not clean.mosquito.has("beard") and not clean.mosquito.has("hair_color"),"human-specific appearance cannot leak into mosquito")
	var source: Dictionary={"human":{"eyes":2,"mouth":0,"brows":1,"mustache":2,"beard":1,"hair_color":4,"reach":999,"brow_color":5},"mosquito":{"eyes":0,"mouth":2,"brows":1,"color":3},"owner":123}
	var human:=CosmeticsData.appearance_for(source,"human")
	check(not human.has("reach") and not human.has("brow_color") and not human.has("owner") and not human.has("mosquito"),"only role fields survive; no separate brow color or gameplay field")
	human.eyes=0
	check(source.human.eyes==2,"appearance result is independent of caller dictionary")
	var categories:=CosmeticsData.category_keys("human")
	categories.clear()
	check(CosmeticsData.category_keys("human").size()==12,"caller cannot mutate shared categories")
	check(CosmeticsData.option_count("mosquito","hair_color")==0 and CosmeticsData.category_keys("waiting").is_empty(),"unknown/other-role controls unavailable")
	check(CosmeticsData.HAIR_PALETTE==[Color("493529"),Color("815039"),Color("bf813f"),Color("393e46"),Color("b6afa1"),Color("e0c797")],"six approved natural colors retain original index0")
	print("COSMETICS_TEST_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
