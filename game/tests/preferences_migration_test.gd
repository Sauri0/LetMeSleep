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
	legacy.set_value("bindings", "bite", "key:%d" % KEY_H)
	legacy.set_value("bindings", "self_swat", "key:%d" % KEY_T)
	legacy.set_value("connection", "player_name", "Migración")
	legacy.set_value("audio", "master_volume", 0.23)
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
		check(DirAccess.remove_absolute(destination) == OK, "Backed-up destination can be removed for the migration fixture")
	check(not FileAccess.file_exists(destination), "Destination is actually absent immediately before migration")
	check(Prefs._migrate_legacy_settings(source) == OK, "Migration populates the actual preferences destination")
	if FileAccess.get_file_as_bytes(destination) != source_bytes:
		print("MIGRATION_DIAGNOSTIC equals_original=%s source_unchanged=%s destination_exists=%s" % [FileAccess.get_file_as_bytes(destination) == original, FileAccess.get_file_as_bytes(source) == source_bytes, FileAccess.file_exists(destination)])
	check(FileAccess.get_file_as_bytes(destination) == source_bytes, "Actual destination contains the exact migration fixture before loading")
	Prefs.cosmetics = {}
	Prefs._loaded = false
	Prefs.load_settings()
	check(Prefs.cosmetics == {"human": {"color": 5, "accessory": 2, "face": 0, "hair": 0, "outfit": 0, "footwear": 0, "accent": 0}, "mosquito": {"color": 1, "accessory": 1, "face": 0, "hair": 0, "outfit": 0, "footwear": 0, "accent": 0}}, "Fresh load preserves old appearance IDs and adds default footwear")
	check(is_equal_approx(Prefs.master_volume,0.23), "Migration preserves the exact old master volume")
	check(is_equal_approx(Prefs.music_volume,0.55) and is_equal_approx(Prefs.effects_volume,0.8) and is_equal_approx(Prefs.ambience_volume,0.45) and is_equal_approx(Prefs.ui_volume,0.65), "Missing audio categories use independent defaults")
	Prefs.cosmetics.human.face = 2
	Prefs.cosmetics.human.hair = 1
	Prefs.cosmetics.mosquito.outfit = 2
	Prefs.cosmetics.mosquito.accent = 4
	Prefs.cosmetics.human.accessory = 3
	Prefs.cosmetics.human.footwear = 2
	Prefs.cosmetics.mosquito.footwear = 1
	Prefs.music_volume = 0.12
	Prefs.effects_volume = 0.34
	Prefs.ambience_volume = 0.56
	Prefs.ui_volume = 0.78
	Prefs.local_host_port = 28451
	Prefs.server_port = 29111
	Prefs.sharing_scope = "virtual"
	Prefs.save_settings()
	Prefs.cosmetics = {}
	Prefs._loaded = false
	Prefs.load_settings()
	check(Prefs.cosmetics.human.face == 2 and Prefs.cosmetics.human.hair == 1 and Prefs.cosmetics.mosquito.outfit == 2 and Prefs.cosmetics.mosquito.accent == 4, "Expanded role profiles persist across fresh settings load")
	check(Prefs.cosmetics.human.accessory == 3 and Prefs.cosmetics.human.footwear == 2 and Prefs.cosmetics.mosquito.footwear == 1, "Nightcap and independent footwear persist for both roles")
	check(is_equal_approx(Prefs.master_volume,0.23) and is_equal_approx(Prefs.music_volume,0.12) and is_equal_approx(Prefs.effects_volume,0.34) and is_equal_approx(Prefs.ambience_volume,0.56) and is_equal_approx(Prefs.ui_volume,0.78), "All five volume controls persist across a real save and reload")
	check(Prefs.local_host_port == 28451 and Prefs.server_port == 29111 and Prefs.sharing_scope == "virtual", "Host port persists independently from previously joined server")
	check(Prefs.binding_text("jump") == "J" and Prefs.binding_text("attack") == "Clic der.", "Fresh load restores keyboard and mouse bindings")
	check(Prefs.binding_text("bite") == "H" and Prefs.binding_text("self_swat") == "T", "Migration preserves reassigned concentration and defense controls")
	check(FileAccess.get_file_as_bytes(source) == source_bytes, "Legacy source remains unchanged")
	# A new profile gets the confirmed night outfit; an old explicit ID never does.
	legacy.erase_section("appearance")
	legacy.save(destination)
	Prefs.cosmetics = {}
	Prefs._loaded = false
	Prefs.load_settings()
	check(Prefs.cosmetics.human.accessory == 3 and Prefs.cosmetics.human.face == 1 and Prefs.cosmetics.human.footwear == 0, "Absent appearance section gets nightcap, sleepy face and classic slippers")
	check(is_equal_approx(Prefs.master_volume,0.23) and Prefs.binding_text("bite") == "H", "New appearance defaults do not reset existing audio or bindings")
	var audio_values := ConfigFile.new()
	for invalid_audio: Variant in [NAN, INF, "loud", true, null]:
		audio_values.set_value("audio","music_volume",invalid_audio)
		check(is_equal_approx(Prefs._audio_value(audio_values,"music_volume",0.55),0.55), "Malformed audio level safely defaults: " + str(invalid_audio))
	audio_values.set_value("audio","music_volume",5.0)
	check(is_equal_approx(Prefs._audio_value(audio_values,"music_volume",0.55),1.0), "Audio level is clamped to its upper bound")
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
