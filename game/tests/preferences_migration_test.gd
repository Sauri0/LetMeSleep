extends SceneTree
## Isolated legacy fixture; real destination is backed up and restored exactly.

const Prefs = preload("res://scripts/preferences.gd")
var failures: int = 0
var checks: int = 0

func check(condition: bool, description: String) -> void:
	checks += 1
	print("PREFERENCES_MIGRATION %s %s" % ["PASS" if condition else "FAIL", description])
	if not condition:
		failures += 1

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var destination: String = ProjectSettings.globalize_path(Prefs.FILE_PATH)
	var existed: bool = FileAccess.file_exists(destination)
	var original: PackedByteArray = FileAccess.get_file_as_bytes(destination) if existed else PackedByteArray()
	var folder: String = OS.get_user_data_dir().path_join("migration-fixture-%d" % Time.get_ticks_usec())
	DirAccess.make_dir_recursive_absolute(folder)
	var source: String = folder.path_join("legacy.cfg")
	var backup: String = folder.path_join("original-preferences.backup")
	if existed:
		var backup_file := FileAccess.open(backup, FileAccess.WRITE)
		backup_file.store_buffer(original)
		backup_file.close()
	var legacy := ConfigFile.new()
	legacy.set_value("appearance", "cosmetics", {"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 1, "accessory": 1}})
	legacy.set_value("bindings", "jump", "key:%d" % KEY_J)
	legacy.set_value("bindings", "attack", "mouse:2")
	legacy.set_value("connection", "player_name", "Migración")
	legacy.save(source)
	var source_bytes: PackedByteArray = FileAccess.get_file_as_bytes(source)
	var isolated_copy: String = folder.path_join("new.cfg")
	check(Prefs._migrate_legacy_settings(source, isolated_copy) == OK, "Valid legacy ConfigFile copies to an absent destination")
	check(FileAccess.get_file_as_bytes(isolated_copy) == source_bytes, "Copy preserves exact bytes")
	var changed := FileAccess.open(isolated_copy, FileAccess.WRITE)
	changed.store_string("[connection]\nplayer_name=\"Already saved\"\n")
	changed.close()
	var newer_bytes: PackedByteArray = FileAccess.get_file_as_bytes(isolated_copy)
	check(Prefs._migrate_legacy_settings(source, isolated_copy) == OK and FileAccess.get_file_as_bytes(isolated_copy) == newer_bytes, "Existing new preferences are never overwritten")
	var invalid_path: String = folder.path_join("invalid.cfg")
	var invalid := FileAccess.open(invalid_path, FileAccess.WRITE)
	invalid.store_string("[broken\n")
	invalid.close()
	var invalid_target: String = folder.path_join("invalid-copy.cfg")
	var previous_errors: bool = Engine.print_error_messages
	Engine.print_error_messages = false
	var invalid_error: Error = Prefs._migrate_legacy_settings(invalid_path, invalid_target)
	var copy_error: Error = Prefs._migrate_legacy_settings(source, folder.path_join("missing/destination.cfg"))
	Engine.print_error_messages = previous_errors
	check(invalid_error != OK and not FileAccess.file_exists(invalid_target), "Malformed legacy file is rejected without creating destination")
	check(copy_error != OK, "Copy failure is returned to the caller")
	# The actual load path proves appearances and bindings are restored, not just copied.
	if existed:
		DirAccess.remove_absolute(destination)
	check(Prefs._migrate_legacy_settings(source) == OK, "Migration populates the actual preferences destination")
	Prefs.cosmetics = {}
	Prefs._loaded = false
	Prefs.load_settings()
	check(Prefs.cosmetics == {"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 1, "accessory": 1}}, "Fresh load restores independent human and mosquito appearances")
	check(Prefs.binding_text("jump") == "J" and Prefs.binding_text("attack") == "Clic der.", "Fresh load restores keyboard and mouse bindings")
	check(FileAccess.get_file_as_bytes(source) == source_bytes, "Legacy source remains unchanged")
	if existed:
		var restored := FileAccess.open(destination, FileAccess.WRITE)
		restored.store_buffer(original)
		restored.close()
		check(FileAccess.get_file_as_bytes(destination) == original, "User preferences restored byte for byte")
	else:
		DirAccess.remove_absolute(destination)
		check(not FileAccess.file_exists(destination), "Originally absent preferences remain absent")
	# Only explicit files made by this fixture are removed; backup survives a failed restore.
	for file: String in [source, isolated_copy, invalid_path]:
		DirAccess.remove_absolute(file)
	if not existed or FileAccess.get_file_as_bytes(destination) == original:
		if FileAccess.file_exists(backup):
			DirAccess.remove_absolute(backup)
		DirAccess.remove_absolute(folder)
	print("PREFERENCES_MIGRATION_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)
