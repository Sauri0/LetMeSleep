extends "res://tests/customization08_checks.gd"
## Actual Session publication into real UI, with inert context/capture/transport.
## No VoiceSession _ready, device enumeration, encoding or microphone access.
const Session = preload("res://scripts/voice_session.gd")

class Context extends Node:
	var ui: CanvasLayer
	var state: Dictionary = {"actors":{1:{"name":"Prueba","alive":true}}}
	var local_id := 1
	var playing := false
	var practice_active := false

class InertTransport extends Node:
	var codec_available := true
	var muted_peers: Dictionary = {}
	func available() -> bool: return codec_available

class InertCapture extends Node:
	var hardware_allowed := true # Test-only presentation flag; has no start API.
	var input_device := "Default"

var context: Context
var session: Node
var transport: InertTransport
var capture: InertCapture
var capture_requests: Array[bool] = []
var output: String

func _run() -> void:
	_backup()
	output = ProjectSettings.globalize_path("res://../outputs/0.9-voice-context-ui")
	DirAccess.make_dir_recursive_absolute(output)
	root.size = Vector2i(1280,720)
	root.content_scale_size = Vector2i(1280,720)
	ui = UI.new()
	ui.voice_test_requested.connect(func(held: bool) -> void: capture_requests.append(held))
	root.add_child(ui)
	await _settle()
	context = Context.new()
	context.ui = ui
	transport = InertTransport.new()
	capture = InertCapture.new()
	session = Session.new()
	session.client = context
	session.transport = transport
	session.capture = capture
	session.encoder = RefCounted.new()
	session._input_devices = PackedStringArray(["Default","Entrada inyectada"])
	var saved_device: String = Prefs.voice_input_device
	var saved_muted: bool = Prefs.voice_muted
	ui.show_lobby({"owner":1,"code":"VOICE9","players":{1:{"name":"Prueba","ready":false}}},1)
	ui.set_lobby_walking(true)
	session._publish_ui()
	_check(ui._voice_state.codec_available and not ui._voice_state.can_transmit,"installed codec does not permit lobby transmission")
	_check(ui._voice_state.reason=="Disponible durante la ronda","actual session publishes lobby reason")
	_check(not ui._voice_indicator.visible,"waiting room respects session visibility rather than advertising PTT")
	_check(not ui._voice_indicator.text_label.text.contains("hablar"),"lobby indicator never promises speaking")
	ui._open_settings()
	session._publish_ui()
	_check(ui._voice_status.text=="Disponible durante la ronda","lobby voice settings explain round requirement")
	_check(not ui._voice_test.disabled and not ui._voice_input_device.disabled,"local microphone test and device controls remain available in lobby")
	_check(capture_requests.is_empty(),"opening lobby settings requests no capture")
	ui._voice_test.button_down.emit()
	ui._voice_test.button_up.emit()
	_check(capture_requests==[true,false],"explicit test button is independent of online permission")
	session.testing = true
	session._capturing = true
	session.level = .3
	session._publish_ui()
	_check(ui._voice_status.text.begins_with("Prueba local") and not ui._voice_status.text.contains("Transmitiendo"),"manual test is labelled local, not network transmission")
	_check(ui._voice_input_device.disabled and ui._voice_devices_refresh.disabled,"manual test retains device-change guard")
	session._capturing = false
	session.testing = false
	session.level = 0
	ui._set_screen("game")
	context.playing = true
	context.practice_active = true
	ui.set_practice(true)
	session._publish_ui()
	_check(ui._voice_state.reason=="Voz sólo online" and not ui._voice_state.can_transmit,"actual session excludes practice")
	_check(ui._voice_indicator.visible and ui._voice_indicator.text_label.text=="Voz sólo online","practice HUD states online requirement without PTT invitation")
	_check(not ui._voice_indicator.level_bar.visible,"unavailable context has no false transmitting meter")
	ui._open_settings()
	session._publish_ui()
	_check(ui._voice_status.text=="Voz sólo online" and not ui._voice_test.disabled,"practice settings retain separate explicit local test")
	ui._close_settings()
	context.practice_active = false
	ui.set_practice(false)
	context.state.actors[1].alive = false
	session._publish_ui()
	_check(not ui._voice_state.can_transmit and ui._voice_state.reason=="No disponible mientras estás eliminado","actual session excludes eliminated actor")
	_check(ui._voice_indicator.text_label.text=="Voz · eliminado" and ui._voice_indicator.tooltip_text==ui._voice_state.reason,"eliminated HUD stays compact and exposes complete reason")
	for viewport_size: Vector2i in [Vector2i(1280,720),Vector2i(1920,1080)]:
		root.size = viewport_size
		root.content_scale_size = viewport_size
		await _settle()
		var indicator_rect: Rect2 = ui._voice_indicator.get_global_rect()
		var text_rect: Rect2 = ui._voice_indicator.text_label.get_global_rect()
		_check(indicator_rect.size.x<=202.5 and indicator_rect.position.x>=0 and indicator_rect.end.x<=float(viewport_size.x),"eliminated indicator stays within 202px and viewport at "+str(viewport_size))
		_check(ui._voice_indicator.text_label.get_minimum_size().x<=text_rect.size.x and text_rect.position.x>=indicator_rect.position.x and text_rect.end.x<=indicator_rect.end.x+.5,"compact eliminated text fits completely inside indicator at "+str(viewport_size))
	root.size = Vector2i(1280,720)
	root.content_scale_size = Vector2i(1280,720)
	await _settle()
	context.state.actors[1].alive = true
	session._publish_ui()
	_check(ui._voice_state.can_transmit and ui._voice_state.reason.is_empty(),"living actor in online round receives actual permission")
	_check(ui._voice_indicator.text_label.text==ui._social_binding("push_to_talk")+" · hablar","PTT invitation appears only for eligible online round")
	var remapped: Dictionary = ui._voice_state.duplicate(true)
	remapped.binding = "Mouse 5"
	ui.set_voice_state(remapped)
	_check(ui._voice_indicator.text_label.text=="Mouse 5 · hablar","eligible HUD respects supplied remapped binding")
	session._capturing = true
	session.level = .4
	session._publish_ui()
	_check(ui._voice_indicator.text_label.text=="Transmitiendo" and ui._voice_indicator.level_bar.visible,"actual capturing state remains visible in eligible round")
	session._capturing = false
	session.muted = true
	session._publish_ui()
	_check(ui._voice_state.can_transmit and ui._voice_indicator.text_label.text=="Mic silenciado","muting is distinct from contextual availability")
	session.muted = false
	ui._open_settings()
	session._publish_ui()
	_check(ui._voice_state.reason=="Cerrá el menú para hablar" and ui._voice_status.text==ui._voice_state.reason,"open menu receives contextual reason instead of active PTT invitation")
	_check(not ui._voice_test.disabled,"open menu still allows explicit local test")
	session.error = "Entrada desconectada · prueba"
	session._publish_ui()
	_check(ui._voice_status.text==session.error,"device error remains visible above contextual reason")
	session.error = ""
	ui._close_settings()
	transport.codec_available = false
	session._publish_ui()
	_check(not ui._voice_state.codec_available and not ui._voice_state.can_transmit,"missing codec cannot advertise online voice")
	_check(not ui._voice_mute.disabled and not ui._voice_test.disabled,"local test controls remain independent of missing transport codec")
	capture.hardware_allowed = false
	session._publish_ui()
	_check(ui._voice_mute.disabled and ui._voice_test.disabled,"no codec or test capability disables unavailable controls")
	ui.set_voice_state({"status":"idle","available":true,"visible":true,"can_test":false})
	_check(not ui._voice_indicator.text_label.text.contains("hablar"),"legacy codec-only payload defaults to no transmission")
	var stale: Dictionary = {"status":"idle","codec_available":true,"can_transmit":true,"visible":true,"reason":""}
	ui.show_lobby({"owner":1,"code":"VOICE9","players":{1:{"name":"Prueba","ready":false}}},1)
	ui.set_lobby_walking(true)
	ui.set_voice_state(stale)
	_check(ui._voice_indicator.text_label.text=="Voz durante la ronda","stale eligible round payload cannot advertise PTT after lobby transition")
	ui._open_settings()
	ui.set_voice_state(stale)
	_check(ui._voice_status.text=="Disponible durante la ronda","settings also guard stale round payload")
	_check(Prefs.voice_input_device==saved_device and Prefs.voice_muted==saved_muted,"context changes preserve device and mute preferences")
	_check(capture_requests==[true,false],"only explicit button action emitted capture request")
	if DisplayServer.get_name()!="headless":
		ui._close_settings()
		await _settle()
		await RenderingServer.frame_post_draw
		_check(root.get_texture().get_image().save_png(output.path_join("lobby-voice-context.png"))==OK,"native lobby context captured")
	await _finish()

func _finish() -> void:
	finishing = true
	session.free()
	context.free()
	transport.free()
	capture.free()
	ui.queue_free()
	await _settle()
	_restore()
	var restored: bool = FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH)
	_check(restored,"original preference bytes restored")
	var file := FileAccess.open(output.path_join("checks.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"preferences_modified":not restored,"real_session_publication":true,"inert_capture_transport":true,"microphone_capture":false,"hardware_enumeration":false,"voice_transmission":false},"\t"));file.close()
	print("VOICE_CONTEXT09_UI_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
