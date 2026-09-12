extends RefCounted
## Alfa assets are visual instances. World/map own physics and placement.
## Origin: floor centre (wall pieces: back plane), metres, Y up. No scaling.
const MANIFEST_PATH := "res://assets/art/house/alfa/manifest.json"
static var _specs: Dictionary = {}
static var _scenes: Dictionary = {}

static func _ensure_catalog() -> void:
	if not _specs.is_empty(): return
	var document: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
	if not document is Dictionary or document.get("schema", "") != "lms.alfa.assets/1":
		push_error("Invalid alfa asset manifest")
		return
	for entry: Dictionary in document.get("assets", []):
		_specs[str(entry.id)] = entry

static func has_asset(id: String) -> bool:
	_ensure_catalog()
	return _specs.has(id)

static func spec(id: String) -> Dictionary:
	_ensure_catalog()
	return Dictionary(_specs.get(id, {})).duplicate(true)

static func catalog() -> Array[Dictionary]:
	_ensure_catalog()
	var result: Array[Dictionary] = []
	for id: String in _specs: result.append(spec(id))
	return result

static func _vector(values: Array) -> Vector3:
	return Vector3(float(values[0]), float(values[1]), float(values[2]))

static func _box(value: Dictionary) -> AABB:
	return AABB(_vector(value.position), _vector(value.size))

static func bounds(id: String) -> AABB:
	var entry := spec(id)
	return _box(entry.visual_bounds) if not entry.is_empty() else AABB()

static func placement_bounds(id: String) -> AABB:
	var entry := spec(id)
	return _box(entry.placement_bounds) if not entry.is_empty() else AABB()

static func collision_boxes(id: String) -> Array[AABB]:
	var result: Array[AABB] = []
	for value: Dictionary in spec(id).get("collision_boxes", []): result.append(_box(value))
	return result

static func support_surfaces(id: String) -> Array:
	return spec(id).get("support_surfaces", []).duplicate(true)

static func instantiate_asset(id: String) -> Node3D:
	var entry := spec(id)
	if entry.is_empty():
		push_error("Unknown alfa asset: " + id)
		return null
	if not _scenes.has(id): _scenes[id] = load(str(entry.path))
	var packed: PackedScene = _scenes[id]
	if packed == null: return null
	var root := Node3D.new()
	root.name = id
	var model: Node3D = packed.instantiate()
	model.position += _vector(entry.get("import_offset", [0, 0, 0]))
	root.add_child(model)
	root.set_meta("alfa_asset_id", id)
	root.set_meta("visual_bounds", bounds(id))
	root.set_meta("placement_bounds", placement_bounds(id))
	return root
