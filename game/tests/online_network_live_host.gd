extends SceneTree
## Requires owner-provided private EOS config and a reviewed EOSG native build.
## Real EOS lobby/listen-server/host lifecycle only; not a two-machine WAN test.
const Network=preload("res://scripts/network.gd")
const Configuration=preload("res://scripts/online_config.gd")
const Invitation=preload("res://scripts/online_invitation.gd")
var network: Node
var checks:=0
var failures: Array[String]=[]
var complete:=false
func _initialize() -> void: run.call_deferred()
func check(value: bool,label: String) -> bool:
	checks+=1
	print("ONLINE_LIVE_HOST %s %s"%["PASS" if value else "FAIL",label])
	if not value: failures.append(label)
	return value
func wait_until(predicate: Callable, seconds: float) -> bool:
	var deadline:=Time.get_ticks_msec()+int(seconds*1000)
	while not predicate.call() and Time.get_ticks_msec()<deadline:
		await process_frame
	return predicate.call()
func run() -> void:
	var path:=""
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--config="): path=arg.trim_prefix("--config=")
	var config:=Configuration.load_values(path)
	if not check(not config.is_empty(),"private configuration available without logging values"):
		quit(1);return
	network=Network.new();network.name="Network";root.add_child(network)
	network.set_physics_process(false)
	for iteration: int in 2:
		network.host_online(config,"Host lifecycle check")
		var joined: bool=await wait_until(func()->bool:return network._had_session or network._connection_phase=="failed",50.0)
		if not check(joined and network._had_session,"real EOS host joins own game room %d"%iteration): break
		check(network.is_server and network._local_host and network.players.has(1) and network.room_owner==1,"host is authoritative actor 1 without child server")
		var invitation: Dictionary=Invitation.decode(network.invitation_text)
		check(invitation.get("ok",false) and invitation.capability==network._online_capability,"generated invitation carries validated lobby capability")
		check(network.online_session.state=="transport_ready" and network.multiplayer.multiplayer_peer.get_connection_status()==MultiplayerPeer.CONNECTION_CONNECTED,"native EOS server peer connected")
		network.send_input(1,Vector3.RIGHT,0.3,0.1,false)
		await process_frame
		check(network.waiting_inputs.get(1,{}).get("seq")==1,"input reaches authoritative waiting room on actual EOS host")
		network.request_close_room()
		var closed: bool=await wait_until(func()->bool:return not network._had_session and not network.online_cleanup_pending(),25.0)
		if not check(closed and network.invitation_text.is_empty(),"room closes, destroys EOS lobby and clears invitation %d"%iteration): break
	network.close_client()
	check(await wait_until(func()->bool:return not network.online_cleanup_pending(),25.0),"outstanding EOS cleanup drained")
	check(network.shutdown_online_backend(),"EOS release and shutdown completed")
	network.queue_free();await process_frame
	complete=true
	print("ONLINE_LIVE_HOST_RESULT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
