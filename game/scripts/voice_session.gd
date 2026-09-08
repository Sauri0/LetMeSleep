extends Node
## Client coordinator. Hardware capture begins only on explicit input events.
const Codec=preload("res://scripts/voice_codec.gd")
const Capture=preload("res://scripts/voice_capture.gd")
const Playout=preload("res://scripts/voice_playout.gd")
const Jitter=preload("res://scripts/voice_jitter_buffer.gd")
const PCM=preload("res://scripts/voice_pcm.gd")
const Acoustics=preload("res://scripts/voice_acoustics.gd")
const Prefs=preload("res://scripts/preferences.gd")
var client: Node
var transport: Node
var capture: Node
var encoder: RefCounted
var peers: Dictionary={}
var _seen_permits: Dictionary={}
var muted:=false
var testing:=false
var epoch:=0
var sequence:=-1
var level:=0.0
var error:=""
var _capturing:=false
var _held_age:=0.0
var _ui_age:=0.0
var _map_id:=""
var _input_devices:=PackedStringArray()
var listener: AudioListener3D

func _ready() -> void:
	client=get_parent();transport=client.network.voice
	encoder=Codec.create();muted=Prefs.voice_muted
	capture=Capture.new();add_child(capture)
	# Every automated source/export fixture uses --script. No test opens input.
	capture.hardware_allowed=DisplayServer.get_name()!="headless" and not OS.get_cmdline_args().has("--script") and not client.options.has("no-microphone") and not client.options.has("visual-preview")
	capture.input_device=Prefs.voice_input_device
	client.ui.voice_input_device_requested.connect(set_input_device)
	client.ui.voice_devices_refresh_requested.connect(refresh_input_devices)
	transport.permission_changed.connect(_permission)
	transport.packet_received.connect(_packet)
	transport.stream_finished.connect(_finish_peer)
	client.ui.voice_mute_requested.connect(set_muted)
	client.ui.voice_peer_mute_requested.connect(transport.set_peer_muted)
	client.ui.voice_test_requested.connect(func(held: bool)->void:
		if held: start(true)
		else: stop())
	client.get_window().focus_exited.connect(func()->void: stop(true))
	client.ui.screen_changed.connect(func(screen: String)->void:
		if screen!="game": stop(true))
	listener=AudioListener3D.new();client.add_child(listener)
	if is_instance_valid(client.world.audio_fx): client.world.audio_fx.acoustic_listener=listener
	refresh_input_devices()
	_publish_ui()

func can_transmit() -> bool:
	return client.playing and bool(client.state.get("actors",{}).get(client.local_id,{}).get("alive",false)) and not client.practice_active and not client.ui.is_menu_open() and transport.available() and encoder!=null

func start(manual_test: bool=false) -> void:
	if _capturing or muted: return
	if not manual_test and not can_transmit(): return
	error="";testing=manual_test
	if not capture.start(): error=capture.error;testing=false;_publish_ui();return
	_capturing=true;_held_age=0.0;sequence=-1
	if not testing:
		epoch+=1;encoder.reset_encoder();transport.begin(epoch)
	_publish_ui()

func stop(immediate: bool=false) -> void:
	if _capturing and not testing:
		if not immediate:
			for frame: PackedFloat32Array in capture.finish_frames():
				if not _encode_and_send(frame): immediate=true;break
		if immediate: transport.hard_stop(epoch)
		else: transport.finish(epoch,sequence)
	_capturing=false;testing=false;level=0.0
	if is_instance_valid(capture): capture.stop()
	_clear_mouth(int(client.local_id))
	_publish_ui()

func set_muted(value: bool) -> void:
	muted=value;Prefs.voice_muted=value;Prefs.save_settings()
	if muted: stop(true)
	_publish_ui()

func set_input_device(value: String) -> void:
	if _capturing or not _input_devices.has(value): return
	capture.input_device=value;Prefs.voice_input_device=value;Prefs.save_settings()
	error="";_publish_ui()

func refresh_input_devices() -> void:
	if _capturing: return
	_input_devices=PackedStringArray()
	# Enumeration does not open input. Automated runs never touch devices.
	if capture.hardware_allowed:
		_input_devices=AudioServer.get_input_device_list()
		if not _input_devices.is_empty() and not _input_devices.has("Default"):
			_input_devices.insert(0,"Default")
	_publish_ui()

func _encode_and_send(frame: PackedFloat32Array) -> bool:
	var packet: PackedByteArray=encoder.encode_frame(frame)
	if packet.is_empty(): error="No se pudo procesar la voz.";return false
	sequence+=1;transport.send_frame(epoch,sequence,packet)
	return true

func _input(event: InputEvent) -> void:
	if event.is_action_released("push_to_talk"): stop();return
	if event.is_action_pressed("push_to_talk") and not event.is_echo(): start()

func _permission(id: int, permission: Dictionary) -> void:
	if permission.is_empty(): _drop_peer(id);return
	if not client.playing or client.practice_active or str(permission.get("map_id",""))!=str(client.state.get("config",{}).get("map_id","")): return
	if peers.has(id) and int(peers[id].permission.permit)==int(permission.permit):
		peers[id].permission=permission.duplicate();peers[id].playout.set_permission(permission);return
	if int(_seen_permits.get(id,-1))>=int(permission.permit): return
	_seen_permits[id]=int(permission.permit)
	_map_id=str(permission.map_id)
	_drop_peer(id)
	var decoder:=Codec.create()
	if decoder==null: error="No se pudo preparar la reproducción de voz.";return
	var jitter:=Jitter.new();jitter.configure({"max_payload_bytes":160})
	var begun: Dictionary=jitter.begin_stream(int(permission.permit),int(permission.first_sequence),Time.get_ticks_usec())
	if not bool(begun.ok): return
	var playout:=Playout.new();client.world.add_child(playout);playout.configure(permission)
	peers[id]={"permission":permission.duplicate(),"jitter":jitter,"decoder":decoder,"playout":playout,"last_data":Time.get_ticks_msec(),"ended_at":0,"decoded":false}

func _packet(id: int, wire_epoch: int, permit: int, seq: int, packet: PackedByteArray) -> void:
	if not peers.has(id): return
	var peer: Dictionary=peers[id]
	if int(peer.permission.epoch)!=wire_epoch or int(peer.permission.permit)!=permit: return
	var result: Dictionary=peer.jitter.push_packet(permit,seq,packet,Time.get_ticks_usec())
	if bool(result.accepted): peer.last_data=Time.get_ticks_msec()

func _finish_peer(id: int, wire_epoch: int, permit: int, last_sequence: int) -> void:
	if not peers.has(id): return
	var peer: Dictionary=peers[id]
	if int(peer.permission.epoch)==wire_epoch and int(peer.permission.permit)==permit:
		peer.jitter.finish_stream(permit,last_sequence,Time.get_ticks_usec())

func _clear_mouth(id: int) -> void:
	if not is_instance_valid(client) or not is_instance_valid(client.world): return
	var actor: Node3D=client.world.get_actor(id)
	if actor!=null and actor.has_method("clear_voice_level"): actor.clear_voice_level()

func _drop_peer(id: int) -> void:
	_clear_mouth(id)
	if not peers.has(id): return
	var peer: Dictionary=peers[id]
	peer.jitter.stop("revoked")
	if is_instance_valid(peer.playout): peer.playout.shutdown();peer.playout.queue_free()
	peers.erase(id)

func clear() -> void:
	stop(true)
	for id: int in peers.keys(): _drop_peer(id)
	_map_id="";_seen_permits.clear()
	if is_instance_valid(listener): listener.clear_current()

func _process(dt: float) -> void:
	if _capturing:
		_held_age+=dt
		if _held_age>119.0 or (not testing and not can_transmit()): stop(true)
		else:
			var frames: Array[PackedFloat32Array]=capture.read_frames(dt)
			if not capture.active: error=capture.error;stop(true)
			for frame: PackedFloat32Array in frames:
				level=PCM.mouth_level(frame)
				if not testing:
					if not _encode_and_send(frame): stop(true);break
				var local: Node3D=client.world.get_actor(client.local_id)
				if local!=null: local.set_voice_level(level)
	var actors: Dictionary=client.state.get("actors",{})
	var map_id: String=str(client.state.get("config",{}).get("map_id",""))
	if not client.playing or map_id!=_map_id:
		for id: int in peers.keys(): _drop_peer(id)
		_map_id=map_id
	for id: int in transport.permissions:
		if not peers.has(id): _permission(id,transport.permissions[id])
	var own: Dictionary=actors.get(client.local_id,{})
	if client.playing and bool(own.get("alive",false)):
		listener.global_position=Acoustics.mouth(own);listener.global_basis=client.rig.global_basis;listener.make_current()
	else:
		listener.clear_current()
		for id: int in peers.keys(): _drop_peer(id)
	for id: int in peers.keys():
		var actor: Dictionary=actors.get(id,{})
		if actor.is_empty() or not bool(actor.get("alive",false)): _drop_peer(id);continue
		var peer: Dictionary=peers[id]
		var playout: Node3D=peer.playout
		playout.global_position=Acoustics.mouth(actor)
		var result: Dictionary=peer.jitter.poll(Time.get_ticks_usec())
		if not bool(result.active) and str(result.reason)=="idle_timeout":
			# Recovery needs a new authority barrier. Never revive an expired
			# permit with delayed packets from its previous playback generation.
			transport.request_resume(id,int(peer.permission.epoch),int(peer.permission.permit))
			_drop_peer(id);continue
		for event: Dictionary in result.events:
			if bool(event.reset_decoder):
				peer.decoder.reset_decoder()
				if bool(peer.decoded):
					playout.shutdown();playout.configure(peer.permission);_clear_mouth(id)
			var pcm: PackedFloat32Array=peer.decoder.decode_frame(event.payload) if str(event.type)=="data" else peer.decoder.conceal_frame()
			if not playout.push(pcm): error=playout.error;_drop_peer(id);break
			peer.decoded=true
		if not peers.has(id): continue
		if not bool(result.active) and int(peer.ended_at)==0:
			peer.ended_at=Time.get_ticks_msec();playout.finish_input()
		# A one-second watchdog is a failure bound, never the normal phrase cutoff.
		if int(peer.ended_at)>0 and Time.get_ticks_msec()-int(peer.ended_at)>1000:
			error="La salida de voz no terminó a tiempo.";_drop_peer(id);continue
		var mouth_level: float=playout.advance(dt)
		if playout.finished(): _drop_peer(id);continue
		if not str(playout.error).is_empty(): error=playout.error;_drop_peer(id);continue
		var view: Node3D=client.world.get_actor(id)
		if view!=null: view.set_voice_level(mouth_level)
	_ui_age+=dt
	if _ui_age>.1: _ui_age=0.0;_publish_ui()

func _publish_ui() -> void:
	if not is_instance_valid(client) or not is_instance_valid(client.ui) or not is_instance_valid(capture): return
	var members: Array=[]
	for id: int in client.state.get("actors",{}):
		if id!=int(client.local_id): members.append({"id":id,"name":str(client.state.actors[id].get("name","Jugador")),"muted":transport.muted_peers.has(id)})
	client.ui.set_voice_state({"status":"capturing" if _capturing else "muted" if muted else "idle" if transport.available() else "unavailable", "available":transport.available(),"can_test":capture.hardware_allowed,"muted":muted,"level":level,"error":error,"testing":testing,"visible":client.playing,"peers":members,"input_devices":_input_devices,"input_device":capture.input_device})

func _exit_tree() -> void:
	clear()
	if is_instance_valid(listener): listener.queue_free()
