extends "res://tests/network09_voice_checks.gd"
## Actual sessions and ENet; only capture input and scene/round setup are staged.
## Run headless with --audio-driver Dummy --frame-delay 2. Never opens hardware.
const Session=preload("res://scripts/voice_session.gd")
const UI=preload("res://scripts/ui.gd")
const World=preload("res://scripts/world.gd")
const Prefs=preload("res://scripts/preferences.gd")
const PCM=preload("res://scripts/voice_pcm.gd")

class Context:
	extends Node
	var network: Node
	var ui: CanvasLayer
	var world: Node3D
	var rig: Node3D
	var options: Dictionary={"no-microphone":true}
	var local_id:=0
	var playing:=true
	var practice_active:=false
	var state: Dictionary={}
	var session: Node

class SyntheticCapture:
	extends "res://scripts/voice_capture.gd"
	## Test-only provider. No AudioServer input, microphone stream or device query.
	var emitted:=0
	var starts:=0
	var stops:=0
	var limit:=50
	var born:=0
	func start() -> bool:
		starts+=1;active=true;error="";emitted=0;born=Time.get_ticks_usec();converter.reset()
		return true
	func read_frames(_dt: float) -> Array[PackedFloat32Array]:
		var frames: Array[PackedFloat32Array]=[]
		if not active: return frames
		var due:=mini(limit,int((Time.get_ticks_usec()-born)/20000))
		if due-emitted>3:
			error="Synthetic source scheduler exceeded three frames";active=false;return frames
		while emitted<due:
			var frame:=PackedFloat32Array();frame.resize(960)
			for sample_index: int in range(960):
				var at:=emitted*960+sample_index
				var frequency:=880.0 if emitted>=limit-10 else 440.0
				frame[sample_index]=.16*sin(TAU*frequency*float(at)/48000.0)
			frames.append(frame);emitted+=1
		return frames
	func stop() -> void:
		if active: stops+=1
		super.stop()

class EncodingObserver:
	extends RefCounted
	## Optional test-only passthrough; the real native Opus instance does work.
	var codec: RefCounted
	var rows: Array[Dictionary]=[]
	var ordinal:=0
	func reset_encoder() -> void:
		codec.reset_encoder();ordinal=0
	func encode_frame(pcm: PackedFloat32Array) -> PackedByteArray:
		var packet: PackedByteArray=codec.encode_frame(pcm)
		rows.append({"t_us":Time.get_ticks_usec(),"ordinal":ordinal,"samples":pcm.size(),"bytes":packet.size(),"valid":codec.validate_frame(packet)})
		ordinal+=1
		return packet

var contexts: Array[Context]=[]
var provider: SyntheticCapture
var recorder: AudioEffectCapture
var raw:=PackedVector2Array()
var trace: Array[Dictionary]=[]
var stream_rows: Array[Dictionary]=[]
var active_case:=""
var case_started:=0
var output_path:=""
var original:=PackedByteArray()
var had_preferences:=false
var capture_peak:=0.0
var base_buses:=0
var first_decode_ms:=-1.0
var first_mouth_ms:=-1.0
var peak_mouth:=0.0
var peak_channel:=0.0
var target_mismatches:=0
var premature_mouth:=0
var peer_ref: Dictionary={}
var source_hashes: Dictionary={}
var received_frames: Array[Dictionary]=[]
var dropped_receiver_frames:=0
var phrase_packet_start:=0
var phrase_permits: Dictionary={}
var encoding_observer: EncodingObserver
var phrase_encode_start:=0
var phrase_ingress_start:=0
var setup_trace: Array[Dictionary]=[]
var setup_pending_label:=""
var link_warmups: Array[Dictionary]=[]

func _setup_stats(label: String, sample_after_poll: bool=true) -> void:
	if not probe_ingress:return
	var row: Dictionary={"label":label,"t_us":Time.get_ticks_usec(),"links":[]}
	for node: Node in clients:
		if not node._client_connected():continue
		var link: ENetPacketPeer=node.multiplayer.multiplayer_peer.get_peer(1)
		row.links.append({"client":id(node),"throttle":link.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE),"limit":link.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_LIMIT),"rtt":link.get_statistic(ENetPacketPeer.PEER_ROUND_TRIP_TIME),"last_rtt":link.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME),"last_variance":link.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME_VARIANCE),"throttle_epoch":link.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_EPOCH)})
	setup_trace.append(row)
	if sample_after_poll:setup_pending_label=label

func _backup_preferences() -> void:
	had_preferences=FileAccess.file_exists(Prefs.FILE_PATH)
	if had_preferences:
		original=FileAccess.get_file_as_bytes(Prefs.FILE_PATH)
		var backup:=FileAccess.open(OS.get_user_data_dir().path_join("voice09-session-backup-%d.cfg"%Time.get_ticks_usec()),FileAccess.WRITE)
		backup.store_buffer(original);backup.close()

func _restore_preferences() -> bool:
	if had_preferences:
		var file:=FileAccess.open(Prefs.FILE_PATH,FileAccess.WRITE);file.store_buffer(original);file.close()
	elif FileAccess.file_exists(Prefs.FILE_PATH): DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
	return FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH)

func _context(network: Node) -> Context:
	_setup_stats("before_context_"+str(id(network)))
	var context:=Context.new();context.network=network;context.local_id=id(network)
	network.get_parent().add_child(context)
	context.world=World.new();context.add_child(context.world);context.world.set_process(false)
	context.rig=Node3D.new();context.add_child(context.rig)
	var camera:=Camera3D.new();context.rig.add_child(camera);camera.make_current()
	context.ui=UI.new();context.add_child(context.ui);context.ui._set_screen("game")
	context.session=Session.new();context.add_child(context.session)
	contexts.append(context)
	_setup_stats("after_context_"+str(id(network)))
	return context

func _prepare_roles(speaker_role: String) -> void:
	_setup_stats("before_roles_"+speaker_role)
	for context: Context in contexts: context.session.clear()
	var simulation:=Sim.new()
	var roster: Dictionary={id(clients[0]):{"role":speaker_role},id(clients[1]):{"role":"mosquito" if speaker_role=="human" else "human"}}
	simulation.start(roster,{"mode":"survival","human_count":1,"map_id":"house","round_seconds":180})
	authority.sim=simulation
	check(simulation.phase=="playing","valid prepared survival round for "+speaker_role)
	for index: int in range(2):
		var actor: Dictionary=simulation.actors[id(clients[index])]
		actor.p=Vector3(0,0,-.6 if index==0 else .6)+Vector3.UP*(1.48 if actor.role=="mosquito" else 0.0)
		actor.facial_preview="neutral";actor.preview_only=true
	for context: Context in contexts:
		context.state={"config":simulation.config.duplicate(true),"actors":simulation.actors.duplicate(true)}
		context.world.sync_actors(context.state.actors,context.local_id,.02)
		context.ui.show_game(context.state,{},context.local_id)
		context.session.muted=false
		context.session.error=""
		context.session._publish_ui()
		for actor: Node in context.world.actors.values(): actor.set_local(false)
	_setup_stats("after_roles_"+speaker_role)

func _poll_network() -> void:
	for node: Node in peers:
		if is_instance_valid(node) and node.multiplayer.multiplayer_peer is ENetMultiplayerPeer: node.multiplayer.poll()
	if not setup_pending_label.is_empty():
		var label:=setup_pending_label;setup_pending_label="";_setup_stats("first_poll_after_"+label,false)

func _observe() -> void:
	_poll_network()
	if contexts.size()!=2 or active_case.is_empty(): return
	var receiver: Context=contexts[1]
	var speaker:=id(clients[0])
	var age:=float(Time.get_ticks_usec()-case_started)/1000.0
	var actor: Node=receiver.world.get_actor(speaker)
	var entry: Dictionary=receiver.session.peers.get(speaker,{})
	if not entry.is_empty():
		peer_ref=entry
		phrase_permits[int(entry.permission.permit)]=entry.jitter
		if bool(entry.decoded) and first_decode_ms<0: first_decode_ms=age
		var playout: Node=entry.playout
		if absf(actor.voice_envelope.target-float(playout.last_level))>.00001: target_mismatches+=1
		if actor.voice_envelope.target>.001 and not bool(entry.decoded): premature_mouth+=1
		if recorder==null and AudioServer.get_bus_index(&"VoiceMaster")>=0:
			recorder=AudioEffectCapture.new();recorder.buffer_length=4.0
			AudioServer.add_bus_effect(AudioServer.get_bus_index(&"VoiceMaster"),recorder)
	var level: float=actor.get_voice_level()
	peak_mouth=maxf(peak_mouth,level)
	if level>.02 and first_mouth_ms<0:first_mouth_ms=age
	for mesh: MeshInstance3D in actor.imported_skin.face_channels:
		if not mesh.visible: continue
		var channel: int=actor.imported_skin.face_channels[mesh].get("MouthOpen",-1)
		if channel>=0:peak_channel=maxf(peak_channel,mesh.get_blend_shape_value(channel))
	if recorder!=null:
		var count:=recorder.get_frames_available()
		if count>0:raw.append_array(recorder.get_buffer(count))
	var row: Dictionary={"t_ms":age,"source_sequence":contexts[0].session.sequence,"permission":receiver.network.voice.permissions.has(speaker),"peer":not entry.is_empty(),"decoded":bool(entry.get("decoded",false)),"playout_level":float(entry.playout.last_level) if not entry.is_empty() else 0.0,"mouth":level,"target":actor.voice_envelope.target,"skin":actor.imported_skin.voice_mouth_level,"captured_frames":raw.size()}
	if probe_ingress:
		var connection: ENetPacketPeer=clients[0].multiplayer.multiplayer_peer.get_peer(1)
		row.throttle=connection.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE)
		row.throttle_limit=connection.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_LIMIT)
		row.rtt=connection.get_statistic(ENetPacketPeer.PEER_ROUND_TRIP_TIME)
		row.last_rtt=connection.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME)
		row.last_variance=connection.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME_VARIANCE)
		row.throttle_epoch=connection.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_EPOCH)
		var outgoing: ENetPacketPeer=authority.multiplayer.multiplayer_peer.get_peer(receiver.local_id)
		row.host_to_receiver={"throttle":outgoing.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE),"limit":outgoing.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_LIMIT),"rtt":outgoing.get_statistic(ENetPacketPeer.PEER_ROUND_TRIP_TIME)}
		row.server=authority.voice.streams.get(speaker,{}).duplicate(true)
		row.server_rate=authority.voice._packet_rates.get(speaker,{}).duplicate(true)
	trace.append(row)

func _wait_seconds(duration: float) -> void:
	var until_usec:=Time.get_ticks_usec()+int(duration*1000000.0)
	while Time.get_ticks_usec()<until_usec: await process_frame

func _wait_for(predicate: Callable,label: String,seconds: float=2.0) -> bool:
	var until_usec:=Time.get_ticks_usec()+int(seconds*1000000.0)
	while Time.get_ticks_usec()<until_usec:
		if predicate.call(): return check(true,label)
		await process_frame
	return check(false,label)

func _stable_audio_link(role: String) -> bool:
	# A clean-phrase control starts after real connection adaptation. This is
	# test preparation, not a production delay or an override of ENet fields.
	if OS.get_cmdline_user_args().has("--no-link-warmup"):return true
	var began:=Time.get_ticks_usec();var next_ping:=began;var next_sample:=began
	var stable_since:=-1;var observations: Array[Dictionary]=[]
	var stable_rtts: Array[float]=[]
	var sender_link: ENetPacketPeer=clients[0].multiplayer.multiplayer_peer.get_peer(1)
	var receiver_link: ENetPacketPeer=authority.multiplayer.multiplayer_peer.get_peer(id(clients[1]))
	while Time.get_ticks_usec()-began<8000000:
		var now:=Time.get_ticks_usec()
		if now>=next_ping:
			sender_link.ping();receiver_link.ping();next_ping=now+250000
		if now>=next_sample:
			var row: Dictionary={"elapsed_ms":float(now-began)/1000.0,"links":[]}
			var stable:=true
			var rtts: Array[float]=[]
			for link: ENetPacketPeer in [sender_link,receiver_link]:
				var throttle:=link.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE)
				var limit:=link.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_LIMIT)
				var rtt:=link.get_statistic(ENetPacketPeer.PEER_ROUND_TRIP_TIME)
				var variance:=link.get_statistic(ENetPacketPeer.PEER_ROUND_TRIP_TIME_VARIANCE)
				rtts.append(rtt)
				row.links.append({"throttle":throttle,"limit":limit,"rtt":rtt,"variance":variance,"last_rtt":link.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME),"last_variance":link.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME_VARIANCE),"throttle_epoch":link.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_EPOCH)})
				stable=stable and throttle==32 and limit==32 and rtt>0 and rtt<=30
			if stable_since>=0:
				for index: int in range(rtts.size()):stable=stable and absf(rtts[index]-stable_rtts[index])<=2.0
			observations.append(row);next_sample=now+50000
			if stable:
				if stable_since<0:stable_since=now;stable_rtts=rtts.duplicate()
				if now-stable_since>=250000:
					link_warmups.append({"role":role,"ready":true,"duration_ms":float(now-began)/1000.0,"samples":observations})
					return check(true,role+" real ENet links settle at throttle32 with low measured RTT before clean phrase")
			else:stable_since=-1
		await process_frame
	link_warmups.append({"role":role,"ready":false,"duration_ms":float(Time.get_ticks_usec()-began)/1000.0,"samples":observations})
	return check(false,role+" clean-link precondition timed out after eight seconds without forcing throttle")

func _reset_recording(label: String) -> void:
	_remove_recorder()
	active_case=label;raw.clear();trace.clear();peer_ref={};case_started=Time.get_ticks_usec()
	first_decode_ms=-1;first_mouth_ms=-1;peak_mouth=0;peak_channel=0;target_mismatches=0;premature_mouth=0
	phrase_packet_start=received_frames.size();phrase_permits.clear()
	phrase_encode_start=encoding_observer.rows.size() if encoding_observer!=null else 0
	phrase_ingress_start=authority.voice.ingress.size() if probe_ingress else 0

func _remove_recorder() -> void:
	if recorder==null:return
	var index:=AudioServer.get_bus_index(&"VoiceMaster")
	if index>=0:
		for effect: int in range(AudioServer.get_bus_effect_count(index)-1,-1,-1):
			if AudioServer.get_bus_effect(index,effect)==recorder:AudioServer.remove_bus_effect(index,effect)
	recorder=null

func _save_case(role: String) -> void:
	var peak:=0.0;var square:=0.0
	var data:=PackedByteArray();data.resize(raw.size()*4)
	for index: int in range(raw.size()):
		peak=maxf(peak,maxf(absf(raw[index].x),absf(raw[index].y)))
		square+=raw[index].length_squared()*.5
		data.encode_s16(index*4,roundi(clampf(raw[index].x,-1,1)*32767.0))
		data.encode_s16(index*4+2,roundi(clampf(raw[index].y,-1,1)*32767.0))
	var wave:=AudioStreamWAV.new();wave.format=AudioStreamWAV.FORMAT_16_BITS;wave.mix_rate=48000;wave.stereo=true;wave.data=data
	check(wave.save_to_wav(output_path.path_join(role+"-actual-playout.wav"))==OK,role+" raw post-DSP output saved")
	var jitter: Dictionary=peer_ref.jitter.get_stats() if not peer_ref.is_empty() else {}
	var all_jitters: Dictionary={};var data_frames:=0;var plc_frames:=0
	for permit: int in phrase_permits:
		var stats: Dictionary=phrase_permits[permit].get_stats();all_jitters[str(permit)]=stats
		data_frames+=int(stats.data_frames);plc_frames+=int(stats.plc_frames)
	var sequences: Array[int]=[]
	for received: Dictionary in received_frames.slice(phrase_packet_start):sequences.append(int(received.sequence))
	check(sequences==range(50),role+" all 50 encoded packets arrive once and in order on clean loopback")
	check(data_frames==50 and plc_frames==0 and phrase_permits.size()==1 and int(jitter.streams_ended)==1,role+" clean phrase decodes completely under one permit without PLC or idle recovery")
	var metrics: Dictionary={"role":role,"source_frames":provider.emitted,"received_sequences":sequences,"data_frames":data_frames,"plc_frames":plc_frames,"all_jitters":all_jitters,"samples_per_frame":960,"raw_frames":raw.size(),"raw_peak":peak,"raw_rms":sqrt(square/maxi(1,raw.size())),"first_decode_ms":first_decode_ms,"first_mouth_ms":first_mouth_ms,"peak_mouth":peak_mouth,"peak_MouthOpen":peak_channel,"target_mismatches":target_mismatches,"premature_mouth":premature_mouth,"jitter":jitter,"trace":trace.duplicate(true)}
	if probe_ingress:
		metrics.encode=encoding_observer.rows.slice(phrase_encode_start)
		metrics.ingress=authority.voice.ingress.slice(phrase_ingress_start)
	var file:=FileAccess.open(output_path.path_join(role+"-trace.json"),FileAccess.WRITE);file.store_string(JSON.stringify(metrics,"\t"));file.close()
	stream_rows.append(metrics.duplicate(true));stream_rows.back().erase("trace")
	check(raw.size()>48000 and peak>.01 and peak<.98,role+" actual limited DSP output is nonzero, finite and unclipped")
	check(peak_mouth>.25 and peak_channel>.15,role+" actual selected imported mouth channel responds to played audio")
	check(first_mouth_ms>=first_decode_ms and first_decode_ms>=0 and premature_mouth==0,role+" remote mouth waits for decoded playback")
	check(target_mismatches==0,role+" ActorView target is exactly production playout level")
	active_case="";_remove_recorder()

func _phrase(role: String) -> void:
	_prepare_roles(role)
	await _wait_seconds(.2)
	if not await _stable_audio_link(role):return
	_reset_recording(role)
	var sender: Context=contexts[0];var receiver: Context=contexts[1]
	provider.limit=50
	sender.session.start()
	check(sender.session._capturing and provider.active,role+" explicit start uses synthetic input provider")
	if not await _wait_for(func()->bool:return provider.emitted>=50 or not provider.error.is_empty(),role+" one-second source completed",2.0):return
	check(provider.error.is_empty() and sender.session.sequence==49,role+" all 50 source frames encoded at original 20ms cadence")
	var permit: Dictionary=receiver.network.voice.permissions.get(sender.local_id,{}).duplicate(true)
	check(not permit.is_empty() and float(permit.get("pitch",0))==(1.55 if role=="mosquito" else 1.0),role+" authenticated acoustic permission reaches real Session")
	if not receiver.session.peers.is_empty():
		var playout: Node=receiver.session.peers[sender.local_id].playout
		check((playout.wet_capture!=null)==(role=="mosquito") and playout.output.pitch_scale==1.0,role+" pitch DSP selected without accelerating output player")
	sender.session.stop()
	check(not provider.active and not sender.session._capturing and sender.world.get_actor(sender.local_id).get_voice_level()==0.0,role+" normal release closes input and local mouth immediately")
	await _wait_for(func()->bool:return not receiver.session.peers.has(sender.local_id),role+" final frame and DSP tail drain to peer removal",2.0)
	await _wait_seconds(.2)
	check(sender.session.error.is_empty() and receiver.session.error.is_empty(),role+" sessions finish without decoder, queue or watchdog errors")
	check(receiver.world.get_actor(sender.local_id).get_voice_level()==0 and receiver.world.get_actor(sender.local_id).imported_skin.voice_mouth_level==0,role+" remote mouth closes exactly after playback")
	_save_case(role)
	# Reliable server finish revocation may arrive after normal local drain.
	await _wait_seconds(.4)

func _revoke_and_close() -> void:
	_prepare_roles("mosquito");await _wait_seconds(.2)
	var sender: Context=contexts[0];var receiver: Context=contexts[1]
	provider.limit=50;sender.session.start()
	if not await _wait_for(func()->bool:return receiver.session.peers.has(sender.local_id) and receiver.world.get_actor(sender.local_id).get_voice_level()>.1,"remote mouth opens before revocation"):return
	var old: Dictionary=receiver.network.voice.permissions[sender.local_id].duplicate(true)
	var old_peer: Dictionary=receiver.session.peers[sender.local_id]
	var old_playout: Node=old_peer.playout
	receiver.ui.voice_peer_mute_requested.emit(sender.local_id,true)
	check(not receiver.session.peers.has(sender.local_id) and receiver.world.get_actor(sender.local_id).get_voice_level()==0,"real peer-mute signal immediately revokes playback and mouth")
	check(old_peer.jitter._queue.is_empty() and not old_playout._active and old_playout._envelopes.is_empty() and old_playout._bus_names.is_empty(),"revocation discards jitter, output envelope and audio buses")
	# Use an actual valid old Opus frame; never bypass the transport gate.
	var silence:=PackedFloat32Array();silence.resize(960)
	var packet: PackedByteArray=sender.session.encoder.encode_frame(silence)
	authority.voice._deliver.rpc_id(receiver.local_id,sender.local_id,int(old.epoch),int(old.permit),49,packet)
	await _wait_seconds(.15)
	check(not receiver.session.peers.has(sender.local_id) and receiver.world.get_actor(sender.local_id).get_voice_level()==0,"late valid packet under revoked permit cannot reopen output or mouth")
	sender.session.stop(true)
	receiver.network.voice.set_peer_muted(sender.local_id,false)
	await _wait_seconds(.15)
	provider.limit=50;sender.session.start()
	await _wait_for(func()->bool:return receiver.session.peers.has(sender.local_id),"fresh epoch resumes after unmute")
	sender.ui.screen_changed.emit("preferences")
	await _wait_for(func()->bool:return not receiver.session.peers.has(sender.local_id),"opening menu hard-stops remote playback")
	check(not sender.session._capturing and not provider.active,"menu closes synthetic capture")
	await _wait_seconds(.2)
	sender.ui.show_game(sender.state,{},sender.local_id);sender.session.start()
	await _wait_for(func()->bool:return authority.voice.streams.has(sender.local_id),"new stream exists before focus loss")
	root.focus_exited.emit()
	await _wait_for(func()->bool:return not authority.voice.streams.has(sender.local_id),"focus loss revokes stream over ENet")
	check(not provider.active and not sender.session._capturing,"focus loss closes capture")
	for context: Context in contexts: context.session.clear()
	check(contexts[1].session.peers.is_empty() and contexts[1].session._seen_permits.is_empty(),"room clear forgets permissions and decoder state")

func _partial_release_and_death() -> void:
	_prepare_roles("human");await _wait_seconds(.2)
	var sender: Context=contexts[0];var receiver: Context=contexts[1]
	provider.limit=0;sender.session.start()
	var half:=PackedFloat32Array();half.resize(480)
	for index: int in range(half.size()):half[index]=.16*sin(TAU*440.0*index/48000.0)
	check(provider.converter.append_mono(half,48000),"production converter retains a 480-sample final capture fragment")
	var at:=received_frames.size()
	sender.session.stop()
	check(sender.session.sequence==0 and not provider.active,"normal release pads and encodes the only partial frame")
	await _wait_for(func()->bool:return received_frames.size()>at,"final partial-frame packet reaches receiver over ENet")
	if received_frames.size()>at:
		var decoder: RefCounted=Codec.create()
		var decoded: PackedFloat32Array=decoder.decode_frame(received_frames[at].payload)
		check(decoded.size()==960 and PCM.rms(decoded.slice(0,600))>.01,"real Opus decoding preserves final sub-frame speech")
	await _wait_seconds(.9)
	provider.limit=0;sender.session.start();provider.converter.append_mono(half,48000)
	var epoch_before: int=sender.session.epoch
	sender.session.stop(true)
	check(sender.session.sequence==-1 and provider.converter._output.is_empty(),"hard stop discards residual PCM instead of encoding a tail")
	await _wait_seconds(.25)
	check(not authority.voice.streams.has(sender.local_id) and int(authority.voice.last_epochs.get(sender.local_id,0))==epoch_before,"hard stop reaches authority for the same epoch")
	provider.limit=50;sender.session.start()
	await _wait_for(func()->bool:return provider.emitted>=3,"capture running before local survival death")
	var stops_before:=provider.stops
	sender.state.actors[sender.local_id].alive=false;authority.sim.actors[sender.local_id].alive=false
	await _wait_for(func()->bool:return not sender.session._capturing and not authority.voice.streams.has(sender.local_id),"local death hard-stops capture and authority stream while PTT remains held")
	var sequence_after: int=sender.session.sequence
	await _wait_seconds(.2)
	check(not provider.active and provider.stops==stops_before+1 and sender.session.sequence==sequence_after,"dead held PTT closes once and never encodes more frames")
	sender.session.start()
	check(not sender.session._capturing and not sender.session.can_transmit(),"dead actor cannot restart PTT")

func _drop_receiver_frame(_speaker: int,_epoch: int,_permit: int,_sequence: int,_packet: PackedByteArray) -> void:
	dropped_receiver_frames+=1

func _receiver_loss() -> void:
	_prepare_roles("human");await _wait_seconds(.2)
	var sender: Context=contexts[0];var receiver: Context=contexts[1]
	provider.limit=100;sender.session.start()
	if not await _wait_for(func()->bool:return receiver.session.peers.has(sender.local_id) and receiver.world.get_actor(sender.local_id).get_voice_level()>.1,"real playback before receiver-only loss"):return
	var old: Dictionary=receiver.network.voice.permissions[sender.local_id].duplicate(true)
	var highest_before: int=authority.voice.streams[sender.local_id].highest
	# Deliberate loss at receiver application ingress. Transport authentication,
	# the sender->host route, clocks, control and resume RPCs remain production.
	var on_packet: Callable=receiver.session._packet
	receiver.network.voice.packet_received.disconnect(on_packet)
	receiver.network.voice.packet_received.connect(_drop_receiver_frame)
	await _wait_seconds(.36)
	receiver.network.voice.packet_received.disconnect(_drop_receiver_frame)
	receiver.network.voice.packet_received.connect(on_packet)
	check(dropped_receiver_frames>=12 and int(authority.voice.streams[sender.local_id].highest)>highest_before+10,"360ms receive-only loss leaves sender-to-host stream active")
	await _wait_for(func()->bool:return receiver.network.voice.permissions.has(sender.local_id) and int(receiver.network.voice.permissions[sender.local_id].permit)>int(old.permit),"idle receiver requests and receives a fresh authorized permit")
	var renewed: Dictionary=receiver.network.voice.permissions.get(sender.local_id,{})
	check(not renewed.is_empty() and int(renewed.first_sequence)>highest_before,"resume starts from future source sequence, never replays old speech")
	await _wait_for(func()->bool:return receiver.world.get_actor(sender.local_id).get_voice_level()>.1,"same held PTT resumes real playout and mouth after loss")
	check(sender.session._capturing and sender.session.epoch==int(old.epoch),"resume preserves ongoing sender epoch")
	sender.session.stop(true)
	await _wait_for(func()->bool:return not receiver.session.peers.has(sender.local_id),"hard close after receiver loss leaves no peer")

func _run() -> void:
	output_path=ProjectSettings.globalize_path("res://../outputs/0.9-voice-session")
	DirAccess.make_dir_recursive_absolute(output_path)
	_backup_preferences()
	Prefs.load_settings();Prefs.voice_muted=false
	AudioServer.set_bus_mute(0,true);AudioServer.set_bus_volume_db(0,-80)
	base_buses=AudioServer.bus_count
	create_timer(40).timeout.connect(func()->void:if not finished:check(false,"40 second watchdog");_finish.call_deferred())
	# Godot removes --audio-driver from get_cmdline_args(); the actual launch
	# command and log accompany the report rather than inventing a driver query.
	check(DisplayServer.get_name()=="headless","headless fixture cannot open hardware capture")
	check(int(AudioServer.get_mix_rate())==48000,"actual mixer uses 48kHz")
	for path: String in ["res://tests/voice09_session_checks.gd","res://scripts/voice_session.gd","res://scripts/voice_transport.gd","res://scripts/voice_playout.gd","res://scripts/voice_jitter_buffer.gd","res://scripts/voice_capture.gd","res://scripts/voice_pcm.gd","res://scripts/actor_view.gd","res://addons/lms_opus/bin/lms_opus.windows.x86_64.dll"]:source_hashes[path]=FileAccess.get_sha256(path)
	authority=peer("SessionAuthority")
	var port:=43000+int(Time.get_ticks_msec()%7000)
	if not check(authority.host(port)==OK,"real ENet authority binds"):await _finish();return
	for index: int in range(2):
		var node:=peer("SessionPeer%d"%index);clients.append(node)
		node.connect_room("127.0.0.1",port,"Session fixture%d"%index,"" if index==0 else authority.room_code,index==0)
		if not await until(func()->bool:return node._had_session and authority.players.has(id(node)),"real room authentication "+str(index)):await _finish();return
		_setup_stats("authenticated_"+str(index))
	authority.set_physics_process(false)
	for node: Node in clients:_context(node)
	clients[1].voice.packet_received.connect(func(speaker: int,e: int,permit: int,sequence: int,payload: PackedByteArray)->void:received_frames.append({"speaker":speaker,"epoch":e,"permit":permit,"sequence":sequence,"payload":payload.duplicate()}))
	process_frame.connect(_observe)
	_prepare_roles("human")
	var sender: Context=contexts[0]
	check(not sender.session.capture.hardware_allowed and sender.session.capture.source==null and sender.session._input_devices.is_empty(),"production Session denies hardware and enumeration under --script")
	sender.ui.voice_test_requested.emit(true)
	check(not sender.session._capturing and not sender.session.capture.active and sender.session.capture.source==null,"explicit test still cannot open hardware in automation")
	sender.ui.voice_test_requested.emit(false)
	sender.session.capture.queue_free();provider=SyntheticCapture.new();sender.session.add_child(provider);sender.session.capture=provider
	if probe_ingress:
		encoding_observer=EncodingObserver.new();encoding_observer.codec=sender.session.encoder;sender.session.encoder=encoding_observer
	sender.session.error=""
	await _phrase("human")
	await _phrase("mosquito")
	if OS.get_cmdline_user_args().has("--phrases-only"):await _finish();return
	await _revoke_and_close()
	await _partial_release_and_death()
	await _receiver_loss()
	await _finish()

func _finish() -> void:
	if finished:return
	finished=true;active_case=""
	if process_frame.is_connected(_observe):process_frame.disconnect(_observe)
	_remove_recorder()
	for context: Context in contexts:
		if is_instance_valid(context):context.session.clear();context.queue_free()
	for node: Node in clients:
		if is_instance_valid(node):node.close_client()
	if is_instance_valid(authority) and authority.multiplayer.multiplayer_peer!=null:authority.multiplayer.multiplayer_peer.close()
	for surface: Node in surfaces:surface.queue_free()
	await process_frame;await process_frame
	# Audio-driver teardown acknowledgement, never a runtime phrase timeout.
	await create_timer(.25).timeout
	check(_restore_preferences(),"original preference bytes restored")
	var remaining: Array[String]=[]
	for index: int in range(AudioServer.bus_count):
		var name:=str(AudioServer.get_bus_name(index))
		if name.begins_with("Voice_") or name.begins_with("VoiceCapture_"):remaining.append(name)
	check(remaining.is_empty(),"no per-peer DSP or capture buses remain after session close")
	var report: Dictionary={"checks":checks,"failures":failures,"results":results,"streams":stream_rows,"source_sha256":source_hashes,"protocol":Net.PROTOCOL,"microphone":false,"hardware_enumeration":false,"hardware_output":false,"synthetic_capture_provider":true,"actual_enet":true,"actual_session_codec_jitter_playout_actor":true,"dummy_driver_short_run_only":true,"remaining_voice_buses":remaining,"setup_trace":setup_trace,"link_warmups":link_warmups,"diagnostic_wrappers":probe_ingress}
	var file:=FileAccess.open(output_path.path_join("checks.json"),FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("VOICE09_SESSION_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
