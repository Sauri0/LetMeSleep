extends SceneTree

const Network = preload("res://scripts/network.gd")
var checks := 0
var failures := 0
var nodes: Array[Node] = []

class ActionSpy extends RefCounted:
	var received: Array = []
	func action(id: int, seq: int, verb: String, yaw: float = NAN, pitch: float = NAN) -> void:
		received = [id,seq,verb,yaw,pitch]

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("CONNECTION_FAIL " + label)
	else:
		print("CONNECTION_OK " + label)

func endpoint(label: String) -> Node:
	var branch := Node.new()
	branch.name = label
	root.add_child(branch)
	set_multiplayer(SceneMultiplayer.new(), branch.get_path())
	var network := Network.new()
	network.name = "Network"
	branch.add_child(network)
	nodes.append(branch)
	return network

func until(predicate: Callable, seconds: float = 4.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(seconds * 1000)
	while not predicate.call() and Time.get_ticks_msec() < deadline:
		await process_frame
	return bool(predicate.call())

func run() -> void:
	var server := endpoint("Server")
	var owner := endpoint("Owner")
	var guest := endpoint("Guest")
	var invalid := endpoint("Invalid")
	check(server.host(29385) == OK, "Real UDP server binds on a separate test port")
	var phases: Array[String] = []
	owner.connection_state_changed.connect(func(value: Dictionary) -> void: phases.append(str(value.phase)))
	owner.connect_room("localhost",29385,"Owner","",true)
	check(await until(func() -> bool: return owner._had_session and owner.room_owner != 0), "DNS, UDP transport and room handshake accept the owner")
	check(phases == ["resolving","connecting_transport","joining_room","joined"], "Connection phases are ordered and distinct")
	check(owner.room_owner == owner.multiplayer.get_unique_id() and server.players.size() == 1, "Accepted owner identity reaches the client")
	guest.connect_room("127.0.0.1",29385,"Guest",server.room_code,false)
	check(await until(func() -> bool: return guest._had_session and guest.room_owner != 0), "Guest joins the real server using the room code")
	guest.request_close_room()
	await create_timer(0.1).timeout
	check(server.players.size() == 2 and owner._had_session and guest._had_session, "A guest close request is a no-op")
	invalid.connect_room("127.0.0.1",29385,"Wrong code","ABC123",false)
	check(await until(func() -> bool: return invalid.connection_state.get("phase") == "failed"), "Wrong room receives a server rejection")
	check(invalid.connection_state.code == "wrong_room", "Specific wrong-room reason survives immediate transport close")
	var rejected: Dictionary = invalid.connection_state.duplicate(true)
	invalid._server_lost()
	check(invalid.connection_state == rejected, "A delayed disconnect cannot replace the rejection")
	invalid.request_close_room()
	check(server.players.size() == 2, "Disconnected client close request is a no-op")
	# A spy at the authoritative simulation boundary verifies actual RPC arguments.
	server.set_physics_process(false)
	var spy := ActionSpy.new()
	server.sim = spy
	owner.send_action(41,"attack",1.125,-0.925)
	check(await until(func() -> bool: return not spy.received.is_empty()), "Captured attack reaches the authoritative action boundary")
	check(spy.received == [owner.multiplayer.get_unique_id(),41,"attack",1.125,-0.925], "Action sequence and click-time yaw/pitch survive reliable transport")
	server.sim = null
	server.set_physics_process(true)
	owner.request_close_room()
	check(await until(func() -> bool: return guest.connection_state.get("code") == "host_closed"), "Owner explicitly closes the guest's room")
	check(guest.connection_state.phase == "closed" and "anfitrión" in guest.connection_state.message and not guest._had_session, "Reliable host-close message is distinct from unexplained connection loss")
	check(server.room_owner == 0 and server.players.is_empty() and server.room_code.is_empty(), "Closing the owner clears the room instead of migrating ownership")
	await create_timer(0.25).timeout
	owner.connect_room("127.0.0.1",29385,"New owner","",true)
	check(await until(func() -> bool: return owner._had_session and owner.room_owner != 0), "External server can accept a fresh room after its previous owner closes")
	guest.connect_room("127.0.0.1",29385,"New guest",server.room_code,false)
	check(await until(func() -> bool: return guest._had_session), "Fresh guest joins the new room")
	owner.close_client()
	check(await until(func() -> bool: return guest.connection_state.get("code") == "host_closed"), "Owner disconnect also ends the room without host migration")
	invalid.connect_room("",27840,"Invalid","ABC123",false)
	check(invalid.connection_state.code == "invalid_endpoint" and not invalid.connecting, "Invalid endpoint fails before opening UDP")
	invalid.connect_room("127.0.0.1",29386,"Timeout","ABC123",false)
	var first_attempt: int = invalid.connection_state.attempt
	invalid.connect_age = Network.TRANSPORT_TIMEOUT
	invalid._physics_process(0.01)
	check(invalid.connection_state.code == "transport_timeout" and invalid.connection_state.can_retry, "No-response timeout is classified as transport, with retry")
	invalid.retry_connect()
	check(invalid.connecting and invalid.connection_state.attempt > first_attempt and invalid.connection_state.phase == "connecting_transport", "Retry creates a fresh attempt using the same endpoint")
	invalid.cancel_connect()
	var cancelled: Dictionary = invalid.connection_state.duplicate(true)
	invalid._connected()
	invalid._failed()
	invalid._server_lost()
	check(invalid.connection_state == cancelled and not invalid.connecting, "Cancel suppresses late connection, failure and disconnection callbacks")
	# Timeouts are separate even when UDP answered but the room handshake did not.
	invalid.connect_room("127.0.0.1",29386,"Handshake","ABC123",false)
	invalid._set_connection_state("joining_room")
	invalid.connect_age = Network.HANDSHAKE_TIMEOUT
	invalid._physics_process(0.01)
	check(invalid.connection_state.code == "handshake_timeout", "Handshake timeout identifies the later stage")
	for network: Node in [owner,guest,invalid]:
		network.close_client()
	server.multiplayer.multiplayer_peer.close()
	for branch: Node in nodes:
		branch.queue_free()
	await process_frame
	await process_frame
	print("NETWORK_CONNECTION_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
