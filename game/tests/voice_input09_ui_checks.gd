extends "res://tests/customization08_checks.gd"
## Presentation only: injected device names, no AudioServer/VoiceSession access.
var device_requests: Array[String]=[]
var refresh_requests:=0
var capture_requests: Array[bool]=[]
var output: String

func _run() -> void:
	_backup()
	output=ProjectSettings.globalize_path("res://../outputs/0.9-voice-input-ui")
	DirAccess.make_dir_recursive_absolute(output)
	root.size=Vector2i(1280,720)
	root.content_scale_size=Vector2i(1280,720)
	ui=UI.new()
	ui.voice_input_device_requested.connect(func(device: String) -> void: device_requests.append(device))
	ui.voice_devices_refresh_requested.connect(func() -> void: refresh_requests+=1)
	ui.voice_test_requested.connect(func(held: bool) -> void: capture_requests.append(held))
	root.add_child(ui)
	await _settle()
	var saved_device: String=Prefs.voice_input_device
	_check(ui._voice_input_device.disabled and ui._voice_input_device.get_item_metadata(0)=="Default","default device remains disabled before backend supplies list")
	_check(device_requests.is_empty() and refresh_requests==0 and capture_requests.is_empty(),"construction never requests device enumeration, selection or capture")
	var state: Dictionary={"status":"idle","available":true,"can_test":true,"muted":false,"level":0.0,"input_devices":["Default","Micrófono USB · prueba"],"input_device":"Default"}
	ui.set_voice_state(state)
	ui._open_settings()
	await _settle()
	var scroll: ScrollContainer=_ancestor_scroll(ui._voice_input_device)
	_check(scroll!=null,"device controls belong to existing settings scroll")
	if scroll!=null: scroll.ensure_control_visible(ui._voice_input_device)
	await _settle()
	_check(not ui._voice_input_device.disabled and ui._voice_input_device.item_count==2 and ui._voice_input_device.selected==0,"backend list and Default selection render without emitting")
	_check(device_requests.is_empty() and refresh_requests==0 and capture_requests.is_empty(),"opening voice settings does not access microphone")
	ui._voice_input_device.grab_focus()
	await _key(KEY_ENTER)
	var popup: PopupMenu=ui._voice_input_device.get_popup()
	_check(popup.visible,"keyboard opens microphone dropdown")
	await _key(KEY_DOWN)
	for value: int in range(12):
		state.level=float(value)/12.0
		ui.set_voice_state(state)
	_check(popup.visible and popup.get_focused_item()==1,"frequent meter updates preserve open dropdown and focused item")
	await _key(KEY_ENTER)
	_check(device_requests==["Micrófono USB · prueba"],"keyboard selection emits exact backend device name once")
	_check(Prefs.voice_input_device==saved_device,"UI selection does not modify saved preference itself")
	state.input_device="Micrófono USB · prueba"
	ui.set_voice_state(state)
	_check(ui._voice_input_device.get_item_metadata(ui._voice_input_device.selected)==state.input_device,"acknowledged device selection is reflected")
	ui._voice_input_device.grab_focus()
	await _key(KEY_ENTER)
	state.status="capturing"
	ui.set_voice_state(state)
	_check(ui._voice_input_device.disabled and ui._voice_devices_refresh.disabled and not popup.visible,"capture disables changing or refreshing device and closes popup")
	ui._voice_input_device.item_selected.emit(0)
	ui._voice_devices_refresh.pressed.emit()
	_check(device_requests.size()==1 and refresh_requests==0,"disabled controls cannot submit changes during capture")
	state.status="idle"
	state.input_devices=[]
	state.input_device="Default"
	ui.set_voice_state(state)
	_check(ui._voice_input_device.disabled and ui._voice_input_device.get_item_metadata(ui._voice_input_device.selected)=="Default","empty backend list shows Default disabled")
	_check(not ui._voice_devices_refresh.disabled,"refresh remains available when device list is empty")
	ui._voice_devices_refresh.grab_focus()
	await _key(KEY_ENTER)
	_check(refresh_requests==1 and capture_requests.is_empty(),"explicit refresh emits one signal without requesting capture")
	await _key(KEY_TAB)
	_check(ui._settings.is_ancestor_of(root.gui_get_focus_owner()),"Tab remains within settings after refresh")
	var long_device: String="Micrófono de prueba USB con nombre largo de interfaz y fabricante · entrada estéreo"
	state.input_devices=PackedStringArray(["Default",long_device,long_device])
	state.input_device="Micrófono USB · prueba"
	ui.set_voice_state(state)
	_check(ui._voice_input_device.item_count==3 and ui._voice_input_device.is_item_disabled(2),"disconnected selection remains visibly unavailable without duplicate options")
	_check(ui._voice_input_device.get_item_metadata(ui._voice_input_device.selected)=="Micrófono USB · prueba" and device_requests.size()==1,"missing selection does not silently switch or save another device")
	state.input_device=long_device
	ui.set_voice_state(state)
	_check(ui._voice_input_device.item_count==2 and ui._voice_input_device.tooltip_text==long_device,"full device identity remains in tooltip and metadata")
	_check(ui._voice_input_device.get_item_text(1).length()<=55 and not ui._voice_input_device.fit_to_longest_item,"long device display cannot force a wide settings column")
	if scroll!=null: scroll.ensure_control_visible(ui._voice_input_device)
	await _settle()
	var device_rect: Rect2=ui._voice_input_device.get_global_rect()
	var refresh_rect: Rect2=ui._voice_devices_refresh.get_global_rect()
	_check(device_rect.size.x>80 and refresh_rect.position.x>=device_rect.end.x and refresh_rect.end.x<=root.size.x,"compact device row fits 1280px without overlapping controls")
	_check(device_rect.position.y>=0 and device_rect.end.y<=root.size.y,"settings scroll reveals complete device control at 720p")
	if DisplayServer.get_name()!="headless":
		await RenderingServer.frame_post_draw
		_check(root.get_texture().get_image().save_png(output.path_join("voice-devices.png"))==OK,"native microphone settings screenshot saved")
	await _key(KEY_ESCAPE)
	_check(not ui._settings_open,"Escape returns from voice settings")
	ui._voice_input_device.item_selected.emit(0)
	ui._voice_devices_refresh.pressed.emit()
	_check(device_requests.size()==1 and refresh_requests==1,"hidden device controls cannot submit requests")
	_check(capture_requests.is_empty(),"device selection and refresh never start microphone capture")
	await _finish()

func _ancestor_scroll(node: Node) -> ScrollContainer:
	var parent: Node=node.get_parent()
	while parent!=null:
		if parent is ScrollContainer: return parent
		parent=parent.get_parent()
	return null

func _finish() -> void:
	finishing=true
	ui.queue_free()
	await _settle()
	_restore()
	var restored: bool=FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH)
	_check(restored,"original preference bytes restored")
	var file:=FileAccess.open(output.path_join("checks.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"preferences_modified":not restored,"hardware_enumeration":false,"microphone_capture":false,"injected_device_names":true},"\t"));file.close()
	print("VOICE_INPUT09_UI_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
