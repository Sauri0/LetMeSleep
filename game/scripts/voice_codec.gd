extends RefCounted
const PATH:="res://addons/lms_opus/lms_opus.gdextension"
static var attempted:=false
static func create() -> RefCounted:
	if not ClassDB.class_exists("LMSOpusCodec") and not attempted:
		attempted=true
		if OS.get_name()=="Windows" and FileAccess.file_exists(PATH): GDExtensionManager.load_extension(PATH)
	if not ClassDB.class_exists("LMSOpusCodec"): return null
	var codec: RefCounted=ClassDB.instantiate("LMSOpusCodec")
	return codec if codec.is_ready() else null
