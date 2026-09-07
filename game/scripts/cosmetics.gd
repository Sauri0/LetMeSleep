class_name Cosmetics
extends RefCounted
## Shared allowlist. Cosmetic data never changes hitboxes or gameplay values.

const PALETTE: Array[Color] = [
	Color("ef9479"), Color("78bbc2"), Color("b3bb79"),
	Color("be9ccc"), Color("e0b966"), Color("8ca6d0"),
]
const COLOR_NAMES: Array[String] = ["Durazno", "Laguna", "Oliva", "Lila", "Miel", "Niebla"]
const HUMAN_ACCESSORIES: Array[String] = ["none", "cap", "glasses"]
const MOSQUITO_ACCESSORIES: Array[String] = ["none", "bow", "goggles"]


static func sanitize(data: Variant) -> Dictionary:
	var result: Dictionary = {
		"human": {"color": 0, "accessory": 0},
		"mosquito": {"color": 0, "accessory": 0},
	}
	if not data is Dictionary:
		return result
	for role: String in ["human", "mosquito"]:
		var appearance: Variant = data.get(role, {})
		if not appearance is Dictionary:
			continue
		var color: Variant = appearance.get("color", 0)
		var accessory: Variant = appearance.get("accessory", 0)
		if color is int and int(color) >= 0 and int(color) < PALETTE.size():
			result[role]["color"] = int(color)
		var count: int = HUMAN_ACCESSORIES.size() if role == "human" else MOSQUITO_ACCESSORIES.size()
		if accessory is int and int(accessory) >= 0 and int(accessory) < count:
			result[role]["accessory"] = int(accessory)
	return result


static func appearance_for(data: Variant, role: String) -> Dictionary:
	if role not in ["human", "mosquito"]:
		return {"color": 0, "accessory": 0}
	var clean: Dictionary = sanitize(data)
	return clean[role].duplicate(true)
