class_name Cosmetics
extends RefCounted
## Shared allowlist. Cosmetic data never changes hitboxes or gameplay values.

const PALETTE: Array[Color] = [
	Color("ef9479"), Color("78bbc2"), Color("b3bb79"),
	Color("be9ccc"), Color("e0b966"), Color("8ca6d0"),
]
const COLOR_NAMES: Array[String] = ["Durazno", "Laguna", "Oliva", "Lila", "Miel", "Niebla"]
const HAIR_PALETTE: Array[Color] = [Color("493529"),Color("815039"),Color("bf813f"),Color("393e46"),Color("b6afa1"),Color("e0c797")]
const HAIR_COLOR_NAMES: Array[String] = ["Castaño oscuro","Castaño","Cobrizo","Negro ceniza","Canoso","Rubio"]
const HUMAN_ACCESSORIES: Array[String] = ["none", "cap", "glasses", "nightcap"]
const MOSQUITO_ACCESSORIES: Array[String] = ["none", "bow", "goggles"]
const HUMAN_CATEGORY_KEYS: Array[String] = ["color","eyes","mouth","brows","hair","hair_color","mustache","beard","outfit","accessory","footwear","accent"]
const MOSQUITO_CATEGORY_KEYS: Array[String] = ["color","eyes","mouth","brows","hair","outfit","accessory","footwear","accent"]
# Union for generic allowlist checks. Role-specific UI and validation must use
# category_keys(role), so mosquitoes never receive human facial-hair fields.
const CATEGORY_KEYS: Array[String] = HUMAN_CATEGORY_KEYS
const HUMAN_OPTIONS := {
	"eyes": ["Abiertos", "Somnolientos", "Entornados"],
	"mouth": ["Sonrisa", "Reposo", "Seria"],
	"brows": ["Suaves", "Arqueadas", "Firmes"],
	"mustache": ["Sin bigote", "Corto", "Caído"],
	"beard": ["Sin barba", "Perilla", "Corta"],
	"hair": ["Corto", "Mechón", "Rulos"],
	"outfit": ["Pijama clásico", "Mangas a rayas", "Chaleco"],
	"accessory": ["Sin accesorio", "Gorra", "Anteojos", "Gorro de noche"],
	"footwear": ["Pantuflas clásicas", "Pantuflas cruzadas", "Pantuflas acolchadas"],
}
const MOSQUITO_OPTIONS := {
	"eyes": ["Redondos", "Alertas", "Somnolientos"],
	"mouth": ["Sonrisa", "Reposo", "Seria"],
	"brows": ["Curvas", "Altas", "Bajas"],
	"hair": ["Antenas rectas", "Antenas curvas", "Antenas plumosas"],
	"outfit": ["Abdomen rayado", "Manchas", "Bandas anchas"],
	"accessory": ["Sin accesorio", "Moño", "Gafas"],
	"footwear": ["Patas lisas", "Patas anilladas", "Patas con puños"],
}

static func default_profile() -> Dictionary:
	# Only new profiles receive the night outfit. Sanitizing existing/network
	# data keeps every explicit 0.5 ID and defaults missing categories to zero.
	var result := sanitize({})
	result.human.accessory = 3
	for part: String in ["eyes","mouth","brows"]:result.human[part]=1
	return result

static func category_keys(role: String) -> Array[String]:
	if role=="human":return HUMAN_CATEGORY_KEYS.duplicate()
	if role=="mosquito":return MOSQUITO_CATEGORY_KEYS.duplicate()
	return []

static func geometry_keys(role: String) -> Array[String]:
	# Classification for coverage, never a collision definition. The renderer's
	# separate invariance gate must confirm that color-only fields move no vertex.
	var result: Array[String]=[]
	for key: String in category_keys(role):
		if key not in ["color","accent","hair_color"]:result.append(key)
	return result

static func geometry_class_id(role: String, appearance: Variant) -> String:
	if role not in ["human","mosquito"]:return ""
	var clean: Dictionary=appearance_for({role:appearance},role)
	var parts: PackedStringArray=["H" if role=="human" else "M"]
	for key: String in geometry_keys(role):parts.append(key+str(clean[key]))
	return "-".join(parts)

static func option_names(role: String, key: String) -> Array:
	if key not in category_keys(role):return []
	if key in ["color", "accent"]:
		return COLOR_NAMES.duplicate()
	if key=="hair_color":return HAIR_COLOR_NAMES.duplicate()
	var options: Dictionary = HUMAN_OPTIONS if role == "human" else MOSQUITO_OPTIONS
	return options.get(key, []).duplicate()

static func option_count(role: String, key: String) -> int:
	return option_names(role, key).size()


static func sanitize(data: Variant) -> Dictionary:
	var result: Dictionary = {"human":{},"mosquito":{}}
	for role: String in ["human", "mosquito"]:
		var appearance: Variant = data.get(role,{}) if data is Dictionary else {}
		if not appearance is Dictionary:appearance={}
		var legacy_face: int=_index(appearance.get("face",0),3)
		for key: String in category_keys(role):
			# A malformed explicit new field becomes zero. Only an ABSENT field
			# inherits its legacy face, preserving mixed profiles deterministically.
			var fallback: int=legacy_face if key in ["eyes","mouth","brows"] else 0
			result[role][key]=_index(appearance.get(key,fallback),option_count(role,key))
	return result

static func _index(value: Variant, count: int) -> int:
	return int(value) if value is int and int(value)>=0 and int(value)<count else 0

static func human_hair_color(appearance: Variant) -> Color:
	# The same palette drives scalp hair, eyebrows, mustache and beard.
	return HAIR_PALETTE[_index(appearance.get("hair_color",0),HAIR_PALETTE.size())] if appearance is Dictionary else HAIR_PALETTE[0]


static func appearance_for(data: Variant, role: String) -> Dictionary:
	if role not in ["human", "mosquito"]:
		return {"color": 0, "accessory": 0}
	var clean: Dictionary = sanitize(data)
	return clean[role].duplicate(true)
