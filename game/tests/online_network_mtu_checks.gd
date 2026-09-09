extends SceneTree
## Captures actual Godot SceneMultiplayer RPC encoding, including its initial
## node-path cache packet. Does not emulate or certify the EOS native wire.
const Transport=preload("res://scripts/online_transport.gd")
class CapturePeer extends MultiplayerPeerExtension:
	var packets: Array[Dictionary]=[]
	var target:=0
	var channel:=0
	var mode:=MultiplayerPeer.TRANSFER_MODE_RELIABLE
	func _get_connection_status() -> MultiplayerPeer.ConnectionStatus: return MultiplayerPeer.CONNECTION_CONNECTED
	func _get_unique_id() -> int: return 1
	func _is_server() -> bool: return true
	func _get_max_packet_size() -> int: return 1164
	func _get_available_packet_count() -> int: return 0
	func _get_packet_peer() -> int: return 2
	func _get_packet_channel() -> int: return 0
	func _get_packet_mode() -> MultiplayerPeer.TransferMode: return MultiplayerPeer.TRANSFER_MODE_RELIABLE
	func _poll() -> void: pass
	func _close() -> void: pass
	func _disconnect_peer(_peer: int,_force: bool) -> void: pass
	func _set_target_peer(value: int) -> void: target=value
	func _get_transfer_mode() -> MultiplayerPeer.TransferMode: return mode
	func _set_transfer_mode(value: MultiplayerPeer.TransferMode) -> void: mode=value
	func _get_transfer_channel() -> int: return channel
	func _set_transfer_channel(value: int) -> void: channel=value
	func _is_server_relay_supported() -> bool: return false
	func _put_packet_script(buffer: PackedByteArray) -> Error:
		packets.append({"target":target,"bytes":buffer.size(),"mode":mode,"channel":channel})
		return OK
var checks:=0
var failures:=0
func _initialize() -> void: call_deferred("run")
func check(value: bool,label: String) -> void:
	checks+=1
	if not value: failures+=1;push_error(label)
func run() -> void:
	var surface:=Node.new();surface.name="Game";root.add_child(surface)
	var mp:=SceneMultiplayer.new();set_multiplayer(mp,surface.get_path())
	var peer:=CapturePeer.new();mp.multiplayer_peer=peer;mp.server_relay=false
	var network:=Node.new();network.name="Network";surface.add_child(network)
	var transport:=Transport.new();transport.name="OnlineTransport";network.add_child(transport)
	transport.reset(7)
	peer.peer_connected.emit(2)
	await process_frame
	peer.packets.clear()
	transport.send_message(2,"_receive_lobby",[{"random":Crypto.new().generate_random_bytes(20000)}])
	check(peer.packets.size()>1,"actual Godot RPC produced multiple captured packets")
	var maximum:=0
	var all_reliable:=true
	for packet: Dictionary in peer.packets:
		maximum=maxi(maximum,packet.bytes)
		all_reliable=all_reliable and packet.mode==MultiplayerPeer.TRANSFER_MODE_RELIABLE
	check(maximum>1032 and maximum<=1164,"actual RPC framing including node path stays within EOS MTU")
	check(all_reliable,"fragmented lobby is reliable")
	peer.packets.clear()
	transport.send_message(2,"_request_movement",[1,Vector3.FORWARD,0.2,0.3,false,false,false,false])
	var has_unreliable:=false
	for packet: Dictionary in peer.packets:
		if packet.mode==MultiplayerPeer.TRANSFER_MODE_UNRELIABLE: has_unreliable=true
	check(has_unreliable,"movement RPC uses actual unreliable mode")
	print("ONLINE_NETWORK_MTU_CHECKS checks=%d failures=%d largest_actual_rpc_bytes=%d"%[checks,failures,maximum])
	mp.multiplayer_peer=OfflineMultiplayerPeer.new()
	surface.queue_free();await process_frame
	quit(0 if failures==0 else 1)
