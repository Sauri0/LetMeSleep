class_name EmoteCatalog
extends RefCounted
## Shared IDs only. Authority controls eligibility/time; poses never move root.
const DEFINITIONS := {
	"wave": {"id":"wave","label":"Saludar","duration":2.4},
	"celebrate": {"id":"celebrate","label":"Festejar","duration":2.4},
	"shrug": {"id":"shrug","label":"Encoger hombros","duration":1.8},
	"yawn": {"id":"yawn","label":"Bostezar","duration":2.8},
}
const IDS: Array[String] = ["wave","celebrate","shrug","yawn"]

static func is_valid(id: String) -> bool:
	return DEFINITIONS.has(id)

static func get_emote(id: String) -> Dictionary:
	return DEFINITIONS[id].duplicate(true) if is_valid(id) else {}

static func entries() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for id: String in IDS:
		result.append(get_emote(id))
	return result

static func normalize_favorites(value: Variant) -> Array[String]:
	var result: Array[String] = []
	if value is Array or value is PackedStringArray:
		for item: Variant in value:
			if item is String and is_valid(item) and item not in result:
				result.append(item)
			if result.size()==IDS.size(): break
	for id: String in IDS:
		if id not in result: result.append(id)
	return result
