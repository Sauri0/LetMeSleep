extends SceneTree

const CosmeticsData = preload("res://scripts/cosmetics.gd")

func _initialize() -> void:
	var baseline: Dictionary = {"human": {"color": 0, "accessory": 0}, "mosquito": {"color": 0, "accessory": 0}}
	for invalid: Variant in [null, 4, "human", [], true, {"human": null}, {"mosquito": []}]:
		assert(CosmeticsData.sanitize(invalid) == baseline)
	var malformed: Dictionary = {"human": {"color": 3.0, "accessory": "1", "reach": 999}, "mosquito": {"color": -1, "accessory": 999}, "owner": true}
	assert(CosmeticsData.sanitize(malformed) == baseline)
	var original: Dictionary = {"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 1, "accessory": 1}}
	var clean: Dictionary = CosmeticsData.sanitize(original)
	assert(clean == original)
	var appearance: Dictionary = CosmeticsData.appearance_for(clean, "mosquito")
	assert(appearance == {"color": 1, "accessory": 1})
	assert(not appearance.has("human") and not appearance.has("mosquito"))
	appearance.color = 4
	assert(clean.mosquito.color == 1 and original.mosquito.color == 1)
	assert(CosmeticsData.appearance_for(original, "waiting") == {"color": 0, "accessory": 0})
	assert(CosmeticsData.PALETTE.size() == 6)
	print("COSMETICS_TEST_PASS: allowlisted integers, bounds, malformed data, role-only appearance, independent copies.")
	quit(0)
