extends SceneTree
## Integration of the actual Network + SceneMultiplayer + OnlineTransport codec.
## ENet loopback substitutes ONLY the EOS peer. Session identity/membership are
## fixtures: this is not an EOS SDK, NAT, relay, two-machine, or WAN test.
const Network = preload("res://scripts/network.gd")
const Invite = preload("res://scripts/online_invitation.gd")
const LOBBY := "loopback-integration-fixture"
const HOST_USER := "fixture-host-user"
const GUEST_USER := "fixture-guest-user"

class LoopbackPeer extends ENetMultiplayerPeer:
	var users: Dictionary = {}
	func get_peer_user_id(id: int) -> String:
		return str(users.get(id, ""))

class FixtureBackend extends RefCounted:
	var busy := false
	func shutdown() -> Dictionary: return {"ok":true}

class FixtureSession extends Node:
	signal state_changed(state: String)
	signal failed(reason: String)
	signal transport_ready(info: Dictionary)
	signal member_departed(user_id: String)
	var backend := FixtureBackend.new()
	var _cleanup: Array = []
	var peer: LoopbackPeer
	var is_host := false
	var members: Array[String] = [HOST_USER, GUEST_USER]
	var admitted: Array[String] = []
	var closed := false
	func start_host(_config: Dictionary, _name: String, protocol: int, capability: String) -> void:
		if protocol != Invite.PROTOCOL or not Invite.valid_capability(capability):
			failed.emit("invalid_fixture_request");return
		transport_ready.emit({"peer":peer,"is_host":true,"lobby_id":LOBBY})
	func start_join(_config: Dictionary, _name: String, invitation: Dictionary) -> void:
		if invitation.get("lobby_id") != LOBBY:
			failed.emit("invalid_fixture_lobby");return
		transport_ready.emit({"peer":peer,"is_host":false,"lobby_id":LOBBY})
	func is_lobby_member(user: String) -> bool: return user in members
	func mark_peer_admitted(user: String) -> bool:
		if user not in members: return false
		admitted.append(user);return true
	func reject_peer(user: String) -> void:
		members.erase(user);member_departed.emit(user)
	func close() -> void: closed = true

var checks := 0
var failures: Array[String] = []
var host_side: Node
var guest_side: Node
var host_network: Node
var guest_network: Node
var host_api: SceneMultiplayer
var guest_api: SceneMultiplayer
var host_session: FixtureSession
var guest_session: FixtureSession
var host_peer: LoopbackPeer
var guest_peer: LoopbackPeer
var guest_id := 0
var observed := {"host_lobby":{},"guest_lobby":{},"host_public":0,"guest_public":0,"host_private":0,"guest_private":0,"guest_accepted":0,"notices":[]}

func _initialize() -> void: call_deferred("run")

func check(value: bool, label: String) -> bool:
	checks += 1
	if not value:
		failures.append(label)
		printerr("ONLINE_PAIR_FAIL " + label)
	return value

func until(condition: Callable, label: String, seconds: float = 4.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(seconds * 1000)
	while not condition.call() and Time.get_ticks_msec() < deadline:
		await create_timer(0.01).timeout
	return check(bool(condition.call()), label)

func attach_session(network: Node, peer: LoopbackPeer, hosting: bool) -> FixtureSession:
	var session := FixtureSession.new()
	session.name = "OnlineSession"
	session.peer = peer
	session.is_host = hosting
	network.add_child(session)
	network.online_session = session
	# Match the production OnlineSession wiring; all Network handlers remain real.
	session.state_changed.connect(network._online_state)
	session.failed.connect(network._online_failed)
	session.transport_ready.connect(network._online_ready)
	session.member_departed.connect(network._online_member_departed)
	return session

func run() -> void:
	print("ONLINE_PAIR_START transport=enet_loopback session=fixture eos_sdk=false relay=false wan=false")
	host_peer = LoopbackPeer.new()
	if not check(host_peer.create_server(0, 4, 4) == OK, "open loopback authority peer"):
		await finish();return
	host_peer.get_host().bandwidth_limit(0, 0)
	var port: int = host_peer.get_host().get_local_port()
	guest_peer = LoopbackPeer.new()
	if not check(guest_peer.create_client("127.0.0.1", port, 4) == OK, "open loopback guest peer"):
		await finish();return
	guest_id = guest_peer.get_unique_id()
	check(guest_id > 1, "guest receives a distinct Godot peer ID")
	host_peer.users = {1:HOST_USER, guest_id:GUEST_USER}
	guest_peer.users = host_peer.users.duplicate()
	host_side = Node.new();host_side.name = "PairHost";root.add_child(host_side)
	guest_side = Node.new();guest_side.name = "PairGuest";root.add_child(guest_side)
	host_api = SceneMultiplayer.new();set_multiplayer(host_api, host_side.get_path())
	guest_api = SceneMultiplayer.new();set_multiplayer(guest_api, guest_side.get_path())
	host_network = Network.new();host_network.name = "Network";host_side.add_child(host_network)
	guest_network = Network.new();guest_network.name = "Network";guest_side.add_child(guest_network)
	host_session = attach_session(host_network, host_peer, true)
	guest_session = attach_session(guest_network, guest_peer, false)
	host_network.lobby_updated.connect(func(data: Dictionary): observed.host_lobby = data.duplicate(true))
	guest_network.lobby_updated.connect(func(data: Dictionary): observed.guest_lobby = data.duplicate(true))
	host_network.snapshot_updated.connect(func(_data: Dictionary): observed.host_public += 1)
	guest_network.snapshot_updated.connect(func(_data: Dictionary): observed.guest_public += 1)
	host_network.private_updated.connect(func(_data: Dictionary): observed.host_private += 1)
	guest_network.private_updated.connect(func(_data: Dictionary): observed.guest_private += 1)
	guest_network.accepted.connect(func(id: int): observed.guest_accepted = id)
	host_network.notice.connect(func(text: String): observed.notices.append(text))
	guest_network.notice.connect(func(text: String): observed.notices.append(text))
	host_network.host_online({}, "Pair Host")
	if not await until(func(): return host_network._had_session, "local host accepted through online entry"):
		await finish();return
	check(host_network.is_server and host_network.room_owner == 1, "host is authority and player one")
	check(Invite.decode(host_network.invitation_text).get("ok", false), "host issues a valid LMS1 invitation")
	guest_network.join_online({}, "Pair Guest", host_network.invitation_text)
	if not await until(func(): return guest_network._had_session and host_network.players.size() == 2, "remote handshake traverses real SceneMultiplayer RPC"):
		await finish();return
	check(observed.guest_accepted == guest_id and host_session.admitted == [GUEST_USER], "remote SDK identity fixture admitted after handshake")
	check(host_network.online_transport._epoch == guest_network.online_transport._epoch and host_network.online_transport._epoch != 0, "both codecs use the invitation lobby epoch")
	if not await until(func(): return observed.guest_lobby.get("players", {}).size() == 2 and guest_network.latest.get("actors", {}).size() == 2, "guest receives lobby and two waiting actors"):
		await finish();return
	check(observed.host_lobby.get("players", {}).size() == 2, "host local delivery contains both players")
	var original_config: Dictionary = host_network.config.duplicate(true)
	guest_network.lobby_action("config", {"mode":"sleep", "map_id":"house-patio-v1"})
	await create_timer(.08).timeout
	check(host_network.config == original_config, "guest cannot change owner settings")
	for invalid_map: Variant in ["lobby", "house", "house-v3-123", "island", 12]:
		host_network.lobby_action("config", {"map_id":invalid_map})
		check(host_network.config == original_config, "unavailable map rejected without mutating settings: " + str(invalid_map))
	host_network.lobby_action("config", {"map_id":"house-patio-v1", "mode":"survival"})
	if not await until(func(): return observed.guest_lobby.get("config", {}).get("mode") == "survival", "owner map and mode synchronized to guest"):
		await finish();return
	guest_network.send_input(1, Vector3.RIGHT, 0.25, 0.1, false)
	host_network.send_input(1, Vector3.FORWARD, 0.0, 0.0, false)
	if not await until(func(): return host_network.waiting_inputs.get(guest_id, {}).get("seq") == 1 and host_network.waiting_inputs.get(1, {}).get("seq") == 1, "local and remote waiting inputs reach authority"):
		await finish();return
	guest_network.lobby_action("ready", true)
	host_network.lobby_action("ready", true)
	if not await until(func(): return bool(host_network.players[1].ready) and bool(host_network.players[guest_id].ready), "both ready commands reach authoritative lobby"):
		await finish();return
	host_network.lobby_action("start")
	if not await until(func(): return host_network.sim != null and observed.guest_public > 0 and observed.host_public > 0, "start crosses real map ACK barrier and publishes round", 8.0):
		await finish();return
	check(host_network.sim.phase == "playing" and host_network.sim.actors.size() == 2, "authoritative two-player simulation started")
	check(host_network._pending_map.is_empty() and not host_network._prepared_map.is_empty() and host_network._prepared_map == guest_network._prepared_map, "both recipients loaded and acknowledged identical authored map")
	check(host_network._prepared_map.id == host_network.config.map_id and guest_network.latest.get("config", {}).get("map_fingerprint") == host_network._prepared_map.fingerprint, "round snapshot matches acknowledged map fingerprint")
	if not await until(func(): return observed.guest_private > 0 and observed.host_private > 0, "private snapshots delivered to each recipient"):
		await finish();return
	guest_network.send_input(2, Vector3.LEFT, 0.5, 0.1, false)
	host_network.send_input(2, Vector3.FORWARD, 0.0, 0.0, false)
	if not await until(func(): return host_network.sim.actors[guest_id]._input_seq == 2 and host_network.sim.actors[1]._input_seq == 2, "round inputs decoded through both local and RPC paths"):
		await finish();return
	var previous_tick: int = guest_network.last_received_tick
	if not await until(func(): return guest_network.last_received_tick > previous_tick, "live simulation sends subsequent public snapshots"):
		await finish();return
	check(host_network.online_transport._rates.has(guest_id) and guest_network.online_transport._rates.has(1), "both real transport receivers ingested authenticated frames")
	var first_fingerprint: String = host_network._prepared_map.fingerprint
	# Accelerate the authoritative timer, then let the real simulation finish.
	host_network.sim.elapsed = float(host_network.sim.config.round_seconds) - .02
	if not await until(func(): return guest_network.latest.get("phase") == "results", "survival result reaches guest"):
		await finish();return
	host_network.lobby_action("rematch")
	if not await until(func(): return host_network.sim == null and guest_network.latest.get("phase") == "waiting", "results return both players to independent lobby"):
		await finish();return
	check(not host_network.players[1].ready and not host_network.players[guest_id].ready, "rematch clears ready states")
	guest_network.lobby_action("ready", true)
	host_network.lobby_action("ready", true)
	if not await until(func(): return host_network.players[guest_id].ready, "guest ready for second round"):
		await finish();return
	host_network.lobby_action("start")
	if not await until(func(): return host_network.sim != null and guest_network.latest.get("phase") == "playing", "second round crosses map loading barrier"):
		await finish();return
	check(host_network._prepared_map.fingerprint == first_fingerprint, "second round preserves authored geometry")
	guest_network.close_client()
	if not await until(func(): return host_network.players.size() == 1 and not host_network.players.has(guest_id), "ENet disconnect removes remote game actor"):
		await finish();return
	check(host_network.players.has(1) and host_network.sim == null and host_network.waiting_actors.size() == 1, "host returns to waiting without disconnected actor")
	check(not guest_network._online and guest_network.latest.is_empty() and guest_session.closed, "guest teardown clears state and closes fixture session")
	await finish()

func finish() -> void:
	if not failures.is_empty():
		print("ONLINE_PAIR_DIAGNOSTICS " + JSON.stringify({"host_phase":host_network._connection_phase if is_instance_valid(host_network) else "absent", "guest_phase":guest_network._connection_phase if is_instance_valid(guest_network) else "absent", "notices":observed.notices}))
	if is_instance_valid(guest_network): guest_network.close_client()
	if is_instance_valid(host_network): host_network.close_client()
	if guest_peer != null: guest_peer.close()
	if host_peer != null: host_peer.close()
	if is_instance_valid(guest_side): guest_side.queue_free()
	if is_instance_valid(host_side): host_side.queue_free()
	await process_frame
	print("ONLINE_PAIR_RESULT checks=%d failures=%d transport=enet_loopback eos_sdk=false relay=false wan=false" % [checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
