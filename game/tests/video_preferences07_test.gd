extends SceneTree
const Prefs = preload("res://scripts/preferences.gd")
var checks := 0
var failures := 0
func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1
	print("VIDEO_PREFS07 %s %s" % ["PASS" if ok else "FAIL", message])
func _initialize() -> void: _run.call_deferred()
func reload_from(path: String) -> void:
	Prefs._loaded = false
	Prefs.load_settings(path)
func _run() -> void:
	var existed := FileAccess.file_exists(Prefs.FILE_PATH)
	var original := FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if existed else PackedByteArray()
	var path := OS.get_user_data_dir().path_join("video-profile-fixture-%d-%d.cfg" % [OS.get_process_id(),Time.get_ticks_usec()])
	check(not FileAccess.file_exists(path), "temporary profile starts absent")
	if FileAccess.file_exists(path): quit(1); return
	Prefs.video_fps = 60
	Prefs.video_vsync = true
	reload_from(path)
	check(Prefs.video_fps == 0 and not Prefs.video_vsync, "absent profile defaults to uncapped and no forced VSync")
	var old_profile := ConfigFile.new()
	old_profile.set_value("controls", "human_sensitivity", .003)
	old_profile.save(path)
	reload_from(path)
	check(Prefs.video_fps == 0 and not Prefs.video_vsync, "profile without video values also defaults uncapped")
	check(is_equal_approx(Prefs.human_sensitivity,.003), "new video defaults preserve an existing control choice")
	for cap: int in [60,120,144,165,240,0]:
		Prefs.video_fps = cap
		Prefs.video_vsync = true
		Prefs.save_settings(path)
		Prefs.video_fps = -1
		Prefs.video_vsync = false
		reload_from(path)
		check(Prefs.video_fps == cap and Prefs.video_vsync, "explicit FPS and enabled VSync survive save/reload: %d" % cap)
	Prefs.video_vsync = false
	Prefs.save_settings(path)
	Prefs.video_vsync = true
	reload_from(path)
	check(not Prefs.video_vsync, "explicit disabled VSync survives save/reload")
	check(DirAccess.remove_absolute(path) == OK, "only the temporary test profile is removed")
	check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH) == original if existed else not FileAccess.file_exists(Prefs.FILE_PATH), "actual user preferences were never written")
	print("VIDEO_PREFS07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures == 0 else 1)
