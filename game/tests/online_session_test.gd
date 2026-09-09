extends SceneTree
const Session = preload("res://scripts/online_session.gd")
const Invitation = preload("res://scripts/online_invitation.gd")
const Config := {"product_id":"product", "sandbox_id":"sandbox", "deployment_id":"deployment", "client_id":"client", "client_secret":"test-only-fake"}
const CAP := "0123456789abcdef0123456789abcdef"
var checks := 0
var failures := 0
var now := 100
class FakePeer extends RefCounted:
	signal incoming_connection_request(data: Dictionary)
	signal peer_connection_established(data: Dictionary)
	var accepted: Array = []
	var denied: Array = []
	var closed := false
	func accept_connection_request(id: String) -> void: accepted.append(id)
	func deny_connection_request(id: String) -> void: denied.append(id)
	func close() -> void: closed = true
class FakeLobby extends RefCounted:
	signal lobby_owner_changed
	signal kicked_from_lobby
	var lobby_id := "Lobby123"
	var owner_product_user_id := "host"
	var bucket_id := "LMS-P9"
	var members: Array = ["host", "guest"]
	func get_member_by_product_user_id(id: String) -> Variant: return self if id in members else null
class FakeBackend extends Node:
	signal completed(ticket: int, operation: String, result: Dictionary)
	signal auth_expiring
	var busy := false
	var installed := true
	var pending: Dictionary = {}
	var operations: Array = []
	var peer := FakePeer.new()
	var peer_args: Array = []
	var disposed: Array = []
	func dispose_search(lobby: Variant) -> void: disposed.append(lobby)
	func available() -> bool: return installed
	func request(ticket: int, operation: String, arguments: Dictionary) -> bool:
		if busy: return false
		busy = true
		pending = {"ticket":ticket, "operation":operation, "args":arguments}
		operations.append(operation)
		return true
	func finish(result: Dictionary) -> void:
		var item := pending
		pending = {}
		busy = false
		completed.emit(item.ticket, item.operation, result)
	func make_peer(host: bool, socket: String, owner: String, _force_relay := false) -> Dictionary:
		peer_args = [host, socket, owner]
		return {"ok":true, "peer":peer}
func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("ONLINE_SESSION_FAIL " + label)
func fixture() -> Array:
	var backend := FakeBackend.new()
	root.add_child(backend)
	var session := Session.new()
	session.set_backend(backend)
	session.clock = func(): return now
	root.add_child(session)
	return [session, backend]
func dispose(pair: Array) -> void:
	pair[0].close()
	if pair[1].busy and pair[1].pending.operation == "cleanup": pair[1].finish({"ok":true})
	pair[0].free()
	pair[1].free()
func login(backend: FakeBackend, id: String = "host") -> void:
	backend.finish({"ok":true})
	backend.finish({"ok":true})
	backend.finish({"ok":true,"local_user_id":id})
func _initialize() -> void:
	_run.call_deferred()
func _run() -> void:
	var p := fixture()
	var s: Node = p[0]
	var b: FakeBackend = p[1]
	check(not s.start_host({},"Name",9,CAP) and b.operations.is_empty(), "Missing config makes zero backend requests")
	var placeholders := Config.duplicate()
	placeholders.product_id = "YOUR_PRODUCT_ID"
	check(not Session.valid_configuration(placeholders), "Placeholder config rejected")
	b.installed = false
	check(not s.start_host(Config,"Name",9,CAP), "Missing addon reports unavailable")
	b.installed = true
	check(not s.start_host(Config,"Name",9,"bad"), "Bad capability rejected")
	var ready: Array = []
	s.transport_ready.connect(func(info): ready.append(info))
	check(s.start_host(Config,"Name",9,CAP), "Host attempt starts")
	check(not s.start_host(Config,"Other",9,CAP), "No overlapping SDK request")
	b.finish({"ok":true})
	check(b.pending.operation == "device", "Persistent DeviceID requested")
	b.finish({"ok":true})
	b.finish({"ok":false,"error":"invalid_user","continuance_token":"fake-token"})
	check(b.pending.operation == "create_user", "First account created from continuation")
	b.finish({"ok":true})
	check(b.pending.operation == "login", "CreateUser followed by second Login")
	b.finish({"ok":true,"local_user_id":"host"})
	check(b.pending.operation == "create" and ready.is_empty(), "Login is not game acceptance")
	var lobby := FakeLobby.new()
	b.finish({"ok":true,"lobby":lobby})
	check(ready.size() == 1 and s.state == "transport_ready", "Transport ready only after lobby and peer")
	check(b.peer_args == [true, Invitation.socket_id(lobby.lobby_id), "host"], "One listen-server peer uses discovered lobby socket")
	check(ready[0].capability == CAP and not ready[0].has("connected"), "Capability passed privately; never promises handshake")
	b.peer.incoming_connection_request.emit({"remote_user_id":"guest","socket":b.peer_args[1]})
	b.peer.incoming_connection_request.emit({"remote_user_id":"stranger","socket":b.peer_args[1]})
	b.peer.incoming_connection_request.emit({"remote_user_id":"guest","socket":"wrong"})
	check(b.peer.accepted == ["guest"] and b.peer.denied.size() == 2, "Admission checks socket and current membership")
	var routes: Array = []
	s.route_observed.connect(func(kind): routes.append(kind))
	b.peer.peer_connection_established.emit({"remote_user_id":"guest","socket":b.peer_args[1],"network_type":2})
	check(routes == [2], "Only real backend route callback forwarded (fake fixture)")
	b.auth_expiring.emit()
	check(b.pending.operation == "login" and s.state == "renewing", "Connect renewal uses same serialized DeviceID login")
	b.finish({"ok":true,"local_user_id":"host"})
	check(s.state == "transport_ready" and ready.size() == 1, "Renewal preserves peer and does not repeat acceptance")
	lobby.owner_product_user_id = "guest"
	lobby.lobby_owner_changed.emit()
	check(s.state == "error" and b.peer.closed and b.pending.operation == "cleanup", "Host migration rejected and resource cleanup requested")
	b.finish({"ok":true})
	dispose(p)
	# Cancellation cannot let a late create complete the next attempt.
	p = fixture(); s = p[0]; b = p[1]
	s.start_host(Config,"Name",9,CAP); login(b)
	s.cancel()
	check(not s.start_host(Config,"Name",9,CAP), "Cancelled outstanding create blocks new attempt")
	b.finish({"ok":true,"lobby":FakeLobby.new()})
	check(b.pending.operation == "cleanup" and b.pending.args.is_host, "Late created lobby destroyed")
	b.finish({"ok":true})
	check(s.start_host(Config,"Name",9,CAP), "Retry permitted after cleanup settles")
	now += Session.STEP_TIMEOUT_MS
	s.poll_timeout()
	check(s.state == "error", "Step deadline reports timeout")
	b.finish({"ok":true})
	check(b.operations.back() == "initialize", "Timed-out initialize cannot continue login chain")
	dispose(p)
	# Join owner comes from discovery, not an invitation-supplied identity.
	p = fixture(); s = p[0]; b = p[1]
	var invite := Invitation.decode(Invitation.encode("Lobby123", CAP))
	s.start_join(Config,"Guest",invite); login(b,"guest")
	check(b.pending.operation == "search", "Join searches lobby ID before joining")
	var remote := FakeLobby.new()
	b.finish({"ok":true,"lobby":remote})
	check(b.pending.operation == "join", "Search result provides actual owner")
	b.finish({"ok":true,"lobby":remote})
	check(b.peer_args == [false, Invitation.socket_id("Lobby123"), "host"], "Client targets authoritative discovered owner")
	s.close()
	check(b.pending.operation == "cleanup" and not b.pending.args.is_host, "Guest leaves instead of destroying")
	b.finish({"ok":true})
	dispose(p)
	# Protocol mismatch must never attempt join; changed owner after join fails.
	p = fixture(); s = p[0]; b = p[1]
	s.start_join(Config,"Guest",invite); login(b,"guest")
	remote = FakeLobby.new(); remote.bucket_id = "LMS-P8"
	b.finish({"ok":true,"lobby":remote})
	check(s.state == "error" and not "join" in b.operations, "Other protocol lobby rejected before join")
	check(b.disposed == [remote], "Rejected search wrapper disposed locally")
	dispose(p)
	p = fixture(); s = p[0]; b = p[1]
	s.start_join(Config,"Guest",invite); login(b,"guest")
	remote = FakeLobby.new(); b.finish({"ok":true,"lobby":remote})
	s.cancel(); b.finish({"ok":true,"lobby":remote})
	check(b.pending.operation == "cleanup" and not b.pending.args.is_host, "Late joined lobby left after cancellation")
	b.finish({"ok":false,"error":"cleanup_failed"})
	check(not s.start_join(Config,"Guest",invite), "Failed cleanup blocks false clean retry")
	dispose(p)
	# Every pre-lobby phase can fail/cancel without continuing its chain.
	for phase: String in ["initialize", "device", "login", "create"]:
		p = fixture(); s = p[0]; b = p[1]
		s.start_host(Config,"Name",9,CAP)
		while b.pending.operation != phase:
			b.finish({"ok":true,"local_user_id":"host"})
		b.finish({"ok":false,"error":"simulated_" + phase})
		check(s.state == "error" and not b.busy, "Failure stops " + phase)
		check(s.start_host(Config,"Name",9,CAP), "Retry after settled failure " + phase)
		s.cancel(); b.finish({"ok":true})
		check(s.state == "cancelled" and not b.busy, "Cancelled result stays cancelled " + phase)
		dispose(p)
	p = fixture(); s = p[0]; b = p[1]
	check(not s.start_join(Config,"Guest",{"protocol":{},"capability":CAP}), "Malformed protocol rejected without conversion")
	check(not s.start_host(Config,"Name",8,CAP), "Host protocol matches invitation version")
	s.start_join(Config,"Guest",invite); login(b,"guest")
	remote = FakeLobby.new(); b.finish({"ok":true,"lobby":remote})
	remote.owner_product_user_id = "new-host"
	b.finish({"ok":true,"lobby":remote})
	check(s.state == "error" and b.peer_args.is_empty(), "Owner changed between discovery and join cannot create peer")
	b.finish({"ok":true})
	dispose(p)
	p = fixture(); s = p[0]; b = p[1]
	s.start_host(Config,"Name",9,CAP); login(b)
	b.finish({"ok":true,"lobby":FakeLobby.new()})
	b.auth_expiring.emit()
	b.finish({"ok":true,"local_user_id":"unexpected-identity"})
	check(s.state == "error" and b.peer.closed, "Renewal identity change closes existing transport")
	b.finish({"ok":true})
	dispose(p)
	p = fixture(); s = p[0]; b = p[1]
	s.state_changed.connect(func(value):
		if value == "initialize": s.cancel()
	)
	s.start_host(Config,"Name",9,CAP)
	check(s.state == "cancelled" and b.operations.is_empty(), "Synchronous UI cancellation does not launch backend request")
	dispose(p)
	p = fixture(); s = p[0]; b = p[1]
	var cancelled_ready: Array = []
	s.transport_ready.connect(func(info): cancelled_ready.append(info))
	s.state_changed.connect(func(value):
		if value == "transport_ready": s.cancel()
	)
	s.start_host(Config,"Name",9,CAP); login(b)
	b.finish({"ok":true,"lobby":FakeLobby.new()})
	check(s.state == "cancelled" and cancelled_ready.is_empty() and b.peer.closed, "Synchronous cancellation at ready cannot expose closed peer")
	b.finish({"ok":true})
	dispose(p)
	# Production adapter itself stays parseable and inert without addon.
	var adapter := preload("res://scripts/eos_backend.gd").new()
	root.add_child(adapter)
	check(not adapter.available(), "No SDK means unavailable, never simulated online")
	check(adapter.shutdown().ok, "Uninitialized backend shutdown is inert")
	adapter.busy = true
	check(not adapter.shutdown().ok, "Shutdown refuses pending SDK callbacks")
	adapter.busy = false
	adapter.free()
	print("ONLINE_SESSION_RESULT checks=%d failures=%d" % [checks, failures])
	quit(1 if failures else 0)

