extends SceneTree
## Real ConfigFile save/load through isolated paths; user's preferences read only.
const Prefs=preload("res://scripts/preferences.gd")
const CosmeticsData=preload("res://scripts/cosmetics.gd")
var failures:=0
var checks:=0
func check(value: bool,label: String) -> void:
	checks+=1
	print("PREFERENCES_MIGRATION %s %s" % ["PASS" if value else "FAIL",label])
	if not value:failures+=1
func _initialize() -> void:_run.call_deferred()
func _run() -> void:
	var actual:=ProjectSettings.globalize_path(Prefs.FILE_PATH)
	var existed:=FileAccess.file_exists(actual)
	var original:=FileAccess.get_file_as_bytes(actual) if existed else PackedByteArray()
	var folder:=OS.get_user_data_dir().path_join("migration-fixture-%d" % Time.get_ticks_usec())
	DirAccess.make_dir_recursive_absolute(folder)
	var source:=folder.path_join("legacy.cfg")
	var destination:=folder.path_join("migrated.cfg")
	var legacy:=ConfigFile.new()
	var old_profile: Dictionary={"human":{"color":5,"accessory":2,"face":2,"hair":1,"outfit":2,"footwear":1,"accent":4},"mosquito":{"color":1,"accessory":1,"face":1,"hair":2,"outfit":1,"footwear":2,"accent":3}}
	legacy.set_value("appearance","cosmetics",old_profile)
	legacy.set_value("bindings","jump","key:%d" % KEY_J)
	legacy.set_value("bindings","attack","mouse:2")
	legacy.set_value("bindings","bite","key:%d" % KEY_H)
	legacy.set_value("bindings","self_swat","key:%d" % KEY_T)
	legacy.set_value("connection","player_name","Migración")
	legacy.set_value("audio","master_volume",.23)
	check(legacy.save(source)==OK,"writes isolated legacy fixture")
	var source_bytes:=FileAccess.get_file_as_bytes(source)
	check(Prefs._migrate_legacy_settings(source,destination)==OK and FileAccess.get_file_as_bytes(destination)==source_bytes,"valid legacy copies byte-for-byte to absent destination")
	var newer:=ConfigFile.new()
	newer.set_value("connection","player_name","Already saved")
	newer.save(destination)
	var newer_bytes:=FileAccess.get_file_as_bytes(destination)
	check(Prefs._migrate_legacy_settings(source,destination)==OK and FileAccess.get_file_as_bytes(destination)==newer_bytes,"existing destination is never overwritten")
	DirAccess.remove_absolute(destination)
	check(Prefs._migrate_legacy_settings(source,destination)==OK,"isolated destination recreated through migration API")
	Prefs.cosmetics={}
	Prefs._loaded=false
	Prefs.load_settings(destination)
	check(Prefs.cosmetics==CosmeticsData.sanitize(old_profile),"fresh load migrates all prior IDs without loss")
	check(Prefs.cosmetics.human.eyes==2 and Prefs.cosmetics.human.mouth==2 and Prefs.cosmetics.human.brows==2 and Prefs.cosmetics.mosquito.eyes==1 and Prefs.cosmetics.mosquito.mouth==1 and Prefs.cosmetics.mosquito.brows==1,"legacy face expands independently for both roles")
	check(Prefs.cosmetics.human.hair_color==0 and Prefs.cosmetics.human.mustache==0 and Prefs.cosmetics.human.beard==0,"old profiles gain original hair color and no unsolicited facial hair")
	check(FileAccess.get_file_as_bytes(destination)==source_bytes,"loading alone does not rewrite the legacy file")
	check(is_equal_approx(Prefs.master_volume,.23),"old master volume preserved")
	check(is_equal_approx(Prefs.music_volume,.55) and is_equal_approx(Prefs.effects_volume,.8) and is_equal_approx(Prefs.ambience_volume,.45) and is_equal_approx(Prefs.ui_volume,.65),"missing audio categories receive independent defaults")
	Prefs.cosmetics.human.eyes=0
	Prefs.cosmetics.human.mouth=2
	Prefs.cosmetics.human.brows=1
	Prefs.cosmetics.human.mustache=2
	Prefs.cosmetics.human.beard=1
	Prefs.cosmetics.human.hair_color=5
	Prefs.cosmetics.human.accessory=3
	Prefs.cosmetics.mosquito.eyes=2
	Prefs.cosmetics.mosquito.mouth=0
	Prefs.cosmetics.mosquito.brows=1
	Prefs.cosmetics.mosquito.hair_color=4
	Prefs.cosmetics.mosquito.beard=2
	Prefs.music_volume=.12
	Prefs.effects_volume=.34
	Prefs.ambience_volume=.56
	Prefs.ui_volume=.78
	Prefs.local_host_port=28451
	Prefs.server_port=29111
	Prefs.sharing_scope="virtual"
	Prefs.save_settings(destination)
	var expected:=CosmeticsData.sanitize(Prefs.cosmetics)
	var saved:=ConfigFile.new()
	check(saved.load(destination)==OK and saved.get_value("appearance","schema",0)==2,"new piece schema is persisted")
	check(not Dictionary(saved.get_value("appearance","cosmetics")).human.has("face"),"saved current profile omits obsolete combined selector")
	Prefs.cosmetics={}
	Prefs._loaded=false
	Prefs.load_settings(destination)
	check(Prefs.cosmetics==expected,"mixed piece choices survive actual save and reopen")
	check(Prefs.cosmetics.human.eyes==0 and Prefs.cosmetics.human.mouth==2 and Prefs.cosmetics.human.brows==1 and Prefs.cosmetics.human.mustache==2 and Prefs.cosmetics.human.beard==1 and Prefs.cosmetics.human.hair_color==5,"human pieces and shared hair color remain independent")
	check(Prefs.cosmetics.mosquito.eyes==2 and Prefs.cosmetics.mosquito.mouth==0 and Prefs.cosmetics.mosquito.brows==1 and not Prefs.cosmetics.mosquito.has("hair_color") and not Prefs.cosmetics.mosquito.has("beard"),"mosquito pieces persist without human-only fields")
	check(Prefs.cosmetics.human.accessory==3 and Prefs.cosmetics.human.footwear==1 and Prefs.cosmetics.mosquito.footwear==2,"nightcap and previous footwear survive")
	check(is_equal_approx(Prefs.master_volume,.23) and is_equal_approx(Prefs.music_volume,.12) and is_equal_approx(Prefs.effects_volume,.34) and is_equal_approx(Prefs.ambience_volume,.56) and is_equal_approx(Prefs.ui_volume,.78),"five volumes survive save/reopen")
	check(Prefs.local_host_port==28451 and Prefs.server_port==29111 and Prefs.sharing_scope=="virtual","host port remains independent from joined server")
	check(Prefs.binding_text("jump")=="J" and Prefs.binding_text("attack")=="Clic der." and Prefs.binding_text("bite")=="H" and Prefs.binding_text("self_swat")=="T","keyboard and mouse bindings survive migration/reopen")
	check(FileAccess.get_file_as_bytes(source)==source_bytes,"legacy source never changes")
	legacy.erase_section("appearance")
	legacy.save(destination)
	Prefs.cosmetics={}
	Prefs._loaded=false
	Prefs.load_settings(destination)
	check(Prefs.cosmetics==CosmeticsData.default_profile(),"missing appearance creates confirmed nightcap/sleepy default")
	check(is_equal_approx(Prefs.master_volume,.23) and Prefs.binding_text("bite")=="H","new appearance defaults do not reset unrelated settings")
	var invalid_path:=folder.path_join("invalid.cfg")
	var invalid:=FileAccess.open(invalid_path,FileAccess.WRITE)
	invalid.store_string("[broken\n")
	invalid.close()
	var invalid_target:=folder.path_join("invalid-copy.cfg")
	var previous_errors:=Engine.print_error_messages
	Engine.print_error_messages=false
	var invalid_error:=Prefs._migrate_legacy_settings(invalid_path,invalid_target)
	var copy_error:=Prefs._migrate_legacy_settings(source,folder.path_join("missing/destination.cfg"))
	Engine.print_error_messages=previous_errors
	check(invalid_error!=OK and not FileAccess.file_exists(invalid_target),"malformed ConfigFile does not create a destination")
	check(copy_error!=OK,"copy failure is observable")
	var audio_values:=ConfigFile.new()
	for invalid_audio: Variant in [NAN,INF,"loud",true,null]:
		audio_values.set_value("audio","music_volume",invalid_audio)
		check(is_equal_approx(Prefs._audio_value(audio_values,"music_volume",.55),.55),"malformed audio safely defaults")
	audio_values.set_value("audio","music_volume",5.0)
	check(is_equal_approx(Prefs._audio_value(audio_values,"music_volume",.55),1.0),"audio upper bound preserved")
	check(FileAccess.file_exists(actual)==existed and (not existed or FileAccess.get_file_as_bytes(actual)==original),"real user preferences were never modified")
	for path: String in [source,destination,invalid_path]:
		DirAccess.remove_absolute(path)
	DirAccess.remove_absolute(folder)
	print("PREFERENCES_MIGRATION_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
