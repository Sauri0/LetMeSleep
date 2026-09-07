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
const CATEGORY_KEYS: Array[String] = ["color", "face", "hair", "outfit", "accessory", "accent"]
const HUMAN_OPTIONS := {
	"face": ["Despierto", "Soñoliento", "Cejas firmes"],
	"hair": ["Corto", "Mechón", "Rulos"],
	"outfit": ["Pijama clásico", "Mangas a rayas", "Chaleco"],
	"accessory": ["Sin accesorio", "Gorra", "Anteojos"],
}
const MOSQUITO_OPTIONS := {
	"face": ["Redondo", "Alerta", "Soñoliento"],
	"hair": ["Antenas rectas", "Antenas curvas", "Antenas plumosas"],
	"outfit": ["Abdomen rayado", "Manchas", "Bandas anchas"],
	"accessory": ["Sin accesorio", "Moño", "Gafas"],
}

static func option_names(role: String, key: String) -> Array:
	if key in ["color", "accent"]:
		return COLOR_NAMES.duplicate()
	var options: Dictionary = HUMAN_OPTIONS if role == "human" else MOSQUITO_OPTIONS
	return options.get(key, []).duplicate()

static func option_count(role: String, key: String) -> int:
	return option_names(role, key).size()


static func sanitize(data: Variant) -> Dictionary:
	var result: Dictionary = {
		"human": {"color": 0, "accessory": 0, "face": 0, "hair": 0, "outfit": 0, "accent": 0},
		"mosquito": {"color": 0, "accessory": 0, "face": 0, "hair": 0, "outfit": 0, "accent": 0},
	}
	if not data is Dictionary:
		return result
	for role: String in ["human", "mosquito"]:
		var appearance: Variant = data.get(role, {})
		if not appearance is Dictionary:
			continue
		for key: String in CATEGORY_KEYS:
			var value: Variant = appearance.get(key, 0)
			if value is int and int(value) >= 0 and int(value) < option_count(role, key):
				result[role][key] = int(value)
	return result


static func appearance_for(data: Variant, role: String) -> Dictionary:
	if role not in ["human", "mosquito"]:
		return {"color": 0, "accessory": 0}
	var clean: Dictionary = sanitize(data)
	return clean[role].duplicate(true)
