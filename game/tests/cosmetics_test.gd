extends SceneTree

const CosmeticsData = preload("res://scripts/cosmetics.gd")

func _initialize() -> void:
	var baseline: Dictionary = {"human": {"color": 0, "accessory": 0, "face": 0, "hair": 0, "outfit": 0, "accent": 0}, "mosquito": {"color": 0, "accessory": 0, "face": 0, "hair": 0, "outfit": 0, "accent": 0}}
	for invalid: Variant in [null, 4, "human", [], true, {"human": null}, {"mosquito": []}]:
		assert(CosmeticsData.sanitize(invalid) == baseline)
	var malformed: Dictionary = {"human": {"color": 3.0, "accessory": "1", "reach": 999}, "mosquito": {"color": -1, "accessory": 999}, "owner": true}
	assert(CosmeticsData.sanitize(malformed) == baseline)
	var original: Dictionary = {"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 1, "accessory": 1}}
	var clean: Dictionary = CosmeticsData.sanitize(original)
	assert(clean.human.color == 5 and clean.human.accessory == 2)
	assert(clean.mosquito.color == 1 and clean.mosquito.accessory == 1)
	assert(clean.human.face == 0 and clean.mosquito.outfit == 0)
	for role: String in ["human", "mosquito"]:
		for key: String in CosmeticsData.CATEGORY_KEYS:
			var proposal := baseline.duplicate(true)
			proposal[role][key] = CosmeticsData.option_count(role, key) - 1
			assert(CosmeticsData.sanitize(proposal) == proposal)
			for invalid: Variant in [-1, CosmeticsData.option_count(role, key), "1", 1.0, true, null]:
				proposal[role][key] = invalid
				assert(CosmeticsData.sanitize(proposal)[role][key] == 0)
	var appearance: Dictionary = CosmeticsData.appearance_for(clean, "mosquito")
	assert(appearance == {"color": 1, "accessory": 1, "face": 0, "hair": 0, "outfit": 0, "accent": 0})
	assert(not appearance.has("human") and not appearance.has("mosquito"))
	appearance.color = 4
	assert(clean.mosquito.color == 1 and original.mosquito.color == 1)
	assert(CosmeticsData.appearance_for(original, "waiting") == {"color": 0, "accessory": 0})
	assert(CosmeticsData.PALETTE.size() == 6)
	print("COSMETICS_TEST_PASS: allowlisted integers, bounds, malformed data, role-only appearance, independent copies.")
	quit(0)
