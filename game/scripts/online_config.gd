extends RefCounted
## Build-specific Peer2Peer client configuration; never log credential values.
const PACKAGED_PATH := "res://eos.local.cfg"
const FIELDS := ["product_id", "sandbox_id", "deployment_id", "client_id", "client_secret"]

static func load_values(override_path: String = "") -> Dictionary:
	var path := PACKAGED_PATH if override_path.is_empty() else override_path
	var config := ConfigFile.new()
	if config.load(path) != OK:
		return {}
	var values := {}
	for key: String in FIELDS:
		var value: Variant = config.get_value("eos", key, null)
		if not value is String or value.is_empty():
			return {}
		values[key] = value
	return values
