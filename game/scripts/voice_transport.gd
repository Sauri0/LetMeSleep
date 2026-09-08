extends Node
## Separate RPC node preserves the parent network's version-handshake table.
signal permission_changed(speaker: int, permission: Dictionary)
signal packet_received(speaker: int, epoch: int, permit: int, sequence: int, packet: PackedByteArray)
signal stream_finished(speaker: int, epoch: int, permit: int, last_sequence: int)
const Codec=preload("res://scripts/voice_codec.gd")
const Acoustics=preload("res://scripts/voice_acoustics.gd")
const MAX_PAYLOAD:=160
const MAX_SEQUENCE:=6000
var network: Node
var streams: Dictionary={}
var last_epochs: Dictionary={}
var _packet_rates: Dictionary={}
var permissions: Dictionary={}
var muted_peers: Dictionary={}
var _receiver_mutes: Dictionary={}
var _permit_counter:=0
var _acoustics:=Acoustics.new()
var _validator: RefCounted
var _age:=0.0
var _map_id:=""
var _round_instance:=0

func _ready() -> void:
	network=get_parent()
	_validator=Codec.create()

func available() -> bool:
	return is_instance_valid(_validator) and _validator.has_method("validate_frame") and _validator.is_ready()

func begin(epoch: int) -> void:
	if network._client_connected(): _request_control.rpc_id(1,"begin",epoch,-1)

func finish(epoch: int, last_sequence: int) -> void:
	if network._client_connected(): _request_control.rpc_id(1,"finish",epoch,last_sequence)

func hard_stop(epoch: int) -> void:
	if network._client_connected(): _request_control.rpc_id(1,"stop",epoch,-1)

func send_frame(epoch: int, sequence: int, packet: PackedByteArray) -> void:
	if network._client_connected() and packet.size()>0 and packet.size()<=MAX_PAYLOAD:
		_request_frame.rpc_id(1,epoch,sequence,packet)

func request_resume(speaker: int, epoch: int, permit: int) -> void:
	if network._client_connected(): _request_resume.rpc_id(1,speaker,epoch,permit)

@rpc("any_peer","call_remote","reliable",3)
func _request_resume(speaker: int, epoch: int, permit: int) -> void:
	if not network.is_server or network.sim==null or network.sim.phase!="playing": return
	var receiver:=multiplayer.get_remote_sender_id()
	if not network.players.has(receiver) or not network.players.has(speaker) or not network._menu_rate_ok(receiver): return
	_ensure_map()
	if not streams.has(speaker): return
	var stream: Dictionary=streams[speaker]
	var previous: Dictionary=stream.recipients.get(receiver,{})
	if previous.is_empty() or int(stream.epoch)!=epoch or int(previous.permit)!=permit or int(stream.finish_at)>0: return
	stream.recipients.erase(receiver)
	_permission.rpc_id(receiver,speaker,{})
	_update_permissions(speaker,int(stream.highest)+1,true)

func set_peer_muted(id: int, muted: bool) -> void:
	if muted: muted_peers[id]=true
	else: muted_peers.erase(id)
	if muted and permissions.has(id): permissions.erase(id);permission_changed.emit(id,{})
	if network._client_connected(): _request_mute.rpc_id(1,id,muted)

func reset() -> void:
	for id: int in permissions: permission_changed.emit(id,{})
	permissions.clear();streams.clear();last_epochs.clear();_receiver_mutes.clear();muted_peers.clear();_packet_rates.clear()
	_map_id="";_round_instance=0;_age=0.0

@rpc("any_peer","call_remote","reliable",3)
func _request_mute(speaker: int, muted: bool) -> void:
	if not network.is_server: return
	var receiver:=multiplayer.get_remote_sender_id()
	if not network.players.has(receiver) or not network.players.has(speaker) or not network._menu_rate_ok(receiver): return
	if not _receiver_mutes.has(receiver): _receiver_mutes[receiver]={}
	if muted: _receiver_mutes[receiver][speaker]=true
	else: _receiver_mutes[receiver].erase(speaker)
	if streams.has(speaker): _update_permissions(speaker,int(streams[speaker].highest)+1,true)

@rpc("any_peer","call_remote","reliable",3)
func _request_control(verb: String, epoch: int, last_sequence: int) -> void:
	if not network.is_server or not available() or network.sim==null or network.sim.phase!="playing": return
	var sender:=multiplayer.get_remote_sender_id()
	_ensure_map()
	if not network.players.has(sender) or not network._menu_rate_ok(sender) or epoch<1 or epoch>2147483647: return
	if verb=="begin":
		if epoch<=int(last_epochs.get(sender,0)): return
		# A held key cannot reset the packet rate limiter by replacing its epoch.
		var old: Dictionary=streams.get(sender,{})
		var now:=Time.get_ticks_msec()
		if not old.is_empty() and now-int(old.started)<200: return
		_revoke(sender)
		last_epochs[sender]=epoch
		streams[sender]={"epoch":epoch,"highest":-1,"seen":{},"last":now,"started":now,"rate_time":now,"tokens":12.0,"recipients":{},"finish":-1,"finish_at":0,"permission_due":0}
	elif streams.has(sender) and int(streams[sender].epoch)==epoch:
		if verb=="stop": _revoke(sender)
		elif verb=="finish" and last_sequence>=-1 and last_sequence<=MAX_SEQUENCE:
			streams[sender].finish=last_sequence;streams[sender].finish_at=Time.get_ticks_msec()+750
			for receiver: int in streams[sender].recipients:
				_end.rpc_id(receiver,sender,epoch,int(streams[sender].recipients[receiver].permit),last_sequence)

@rpc("any_peer","call_remote","unreliable",3)
func _request_frame(epoch: int, sequence: int, packet: PackedByteArray) -> void:
	if not network.is_server or not available() or network.sim==null or network.sim.phase!="playing": return
	var sender:=multiplayer.get_remote_sender_id()
	if not network.players.has(sender) or not streams.has(sender): return
	var stream: Dictionary=streams[sender]
	if epoch!=int(stream.epoch) or sequence<0 or sequence>MAX_SEQUENCE or sequence<int(stream.highest)-12 or stream.seen.has(sequence): return
	if packet.is_empty() or packet.size()>MAX_PAYLOAD: return
	var now:=Time.get_ticks_msec()
	var rate: Dictionary=_packet_rates.get(sender,{"tokens":12.0,"time":now})
	rate.tokens=minf(12.0,float(rate.tokens)+float(now-int(rate.time))*.05);rate.time=now
	_packet_rates[sender]=rate
	if float(rate.tokens)<1.0: return
	rate.tokens-=1.0
	if not _validator.validate_frame(packet): return
	if int(stream.finish_at)>0 and sequence>int(stream.finish): return
	stream.last=now;stream.highest=maxi(int(stream.highest),sequence);stream.seen[sequence]=true
	for seen: int in stream.seen.keys():
		if seen<int(stream.highest)-12: stream.seen.erase(seen)
	_update_permissions(sender,maxi(sequence,int(stream.highest)))
	for receiver: int in stream.recipients:
		var permit: Dictionary=stream.recipients[receiver]
		if sequence>=int(permit.first_sequence): _deliver.rpc_id(receiver,sender,epoch,int(permit.permit),sequence,packet)

func _update_permissions(sender: int, first_sequence: int, force: bool=false) -> void:
	if not streams.has(sender) or network.sim==null: return
	var stream: Dictionary=streams[sender]
	if not force and Time.get_ticks_msec()<int(stream.permission_due): return
	stream.permission_due=Time.get_ticks_msec()+100
	var actors: Dictionary=network.sim.actors
	for receiver: int in network.players:
		if receiver==sender: continue
		var permission: Dictionary={}
		if not _receiver_mutes.get(receiver,{}).has(sender) and actors.has(sender) and actors.has(receiver):
			permission=_acoustics.permission(actors[sender],actors[receiver],network.sim.doors)
		if permission.is_empty():
			if stream.recipients.has(receiver): stream.recipients.erase(receiver);_permission.rpc_id(receiver,sender,{})
			continue
		if not stream.recipients.has(receiver):
			_permit_counter+=1
			permission.merge({"epoch":int(stream.epoch),"permit":_permit_counter,"first_sequence":maxi(0,first_sequence),"map_id":_map_id},true)
			stream.recipients[receiver]=permission
			_permission.rpc_id(receiver,sender,permission)
		else:
			var previous: Dictionary=stream.recipients[receiver]
			if absf(float(previous.gain)-float(permission.gain))>.025 or absf(float(previous.cutoff)-float(permission.cutoff))>400:
				previous.merge(permission,true);_permission.rpc_id(receiver,sender,previous)

func _revoke(sender: int) -> void:
	if not streams.has(sender): return
	for receiver: int in streams[sender].recipients:
		if network._peer_can_receive(receiver): _permission.rpc_id(receiver,sender,{})
	streams.erase(sender)

func _process(dt: float) -> void:
	if not is_instance_valid(network) or not network.is_server: return
	_age+=dt
	if _age<.1: return
	_age=0.0
	if network.sim==null or network.sim.phase!="playing":
		for sender: int in streams.keys(): _revoke(sender)
		return
	_ensure_map()
	var now:=Time.get_ticks_msec()
	for sender: int in streams.keys():
		var stream: Dictionary=streams[sender]
		if not network.players.has(sender) or (int(stream.finish_at)==0 and now-int(stream.last)>500) or (int(stream.finish_at)>0 and now>=int(stream.finish_at)):
			_revoke(sender);continue
		_update_permissions(sender,int(stream.highest)+1)

func _ensure_map() -> void:
	var id: String=str(network.sim.config.get("map_id","house"))
	if id!=_map_id or int(network.sim.get_instance_id())!=_round_instance:
		for sender: int in streams.keys(): _revoke(sender)
		_map_id=id;_round_instance=int(network.sim.get_instance_id());_acoustics.configure(id)

func peer_left(id: int) -> void:
	if network.is_server:
		_revoke(id);last_epochs.erase(id);_packet_rates.erase(id);_receiver_mutes.erase(id)
		for stream: Dictionary in streams.values(): stream.recipients.erase(id)
	else:
		permissions.erase(id);permission_changed.emit(id,{})

@rpc("authority","call_remote","reliable",3)
func _permission(speaker: int, value: Dictionary) -> void:
	if network.is_server: return
	if value.is_empty() or muted_peers.has(speaker):
		permissions.erase(speaker);permission_changed.emit(speaker,{});return
	permissions[speaker]=value.duplicate();permission_changed.emit(speaker,value)

@rpc("authority","call_remote","unreliable",3)
func _deliver(speaker: int, epoch: int, permit: int, sequence: int, packet: PackedByteArray) -> void:
	var allowed: Dictionary=permissions.get(speaker,{})
	if network.is_server or allowed.is_empty() or muted_peers.has(speaker): return
	if epoch!=int(allowed.epoch) or permit!=int(allowed.permit) or sequence<int(allowed.first_sequence): return
	if packet.is_empty() or packet.size()>MAX_PAYLOAD: return
	packet_received.emit(speaker,epoch,permit,sequence,packet)

@rpc("authority","call_remote","reliable",3)
func _end(speaker: int, epoch: int, permit: int, last_sequence: int) -> void:
	var allowed: Dictionary=permissions.get(speaker,{})
	if not network.is_server and not allowed.is_empty() and int(allowed.epoch)==epoch and int(allowed.permit)==permit:
		stream_finished.emit(speaker,epoch,permit,last_sequence)
