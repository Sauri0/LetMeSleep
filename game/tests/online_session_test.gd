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
class FakeMember extends RefCounted:
	var product_user_id: String
	func _init(id: String) -> void: product_user_id = id
class FakeLobby extends RefCounted:
	signal lobby_owner_changed
	signal kicked_from_lobby
	signal lobby_updated
	var lobby_id := "Lobby123"
	var owner_product_user_id := "host"
	var bucket_id := "LMS-P9"
	var members: Array = [FakeMember.new("host"), FakeMember.new("guest")]
	func get_member_by_product_user_id(id: String) -> Variant:
		for member: FakeMember in members:
			if member.product_user_id == id: return member
		return null
class FakeBackend extends Node:
	signal completed(ticket: int, operation: String, result: Dictionary)
	signal auth_expiring
	signal member_departed(lobby_id: String, user_id: String)
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
	# Lobby membership consumes slots even without a P2P request. Admission is
	# bounded from first observation; repeated lobby updates cannot renew it.
	p = fixture(); s = p[0]; b = p[1]
	s.start_host(Config,"Name",9,CAP); login(b)
	lobby = FakeLobby.new(); b.finish({"ok":true,"lobby":lobby})
	check(s.mark_peer_admitted("host") and not s.reject_peer("host"), "Owner is admitted and cannot be kicked")
	check(not s.mark_peer_admitted("outsider"), "Nonmember cannot become admitted")
	now += Session.ADMISSION_TIMEOUT_MS - 1
	lobby.lobby_updated.emit(); s.poll_admission()
	check(not b.busy, "Repeated lobby update does not shorten admission grace")
	now += 1; s.poll_admission()
	check(b.pending.operation == "kick" and b.pending.args.user_id == "guest" and b.peer.accepted.is_empty(), "Silent member expires without ever connecting P2P")
	check(not s.mark_peer_admitted("guest"), "Handshake at/after deadline cannot bypass pending eviction")
	lobby.members = [FakeMember.new("host")]
	b.member_departed.emit("Lobby123", "guest")
	lobby.members.append(FakeMember.new("guest")); lobby.lobby_updated.emit()
	check(not s.mark_peer_admitted("guest"), "Rejoin cannot become admitted while its previous eviction is still in flight")
	lobby.members = [FakeMember.new("host")]
	b.finish({"ok":true}); s.poll_admission()
	check(not b.busy and b.operations.count("kick") == 1, "Departed member does not receive duplicate kicks")
	dispose(p)
	p = fixture(); s = p[0]; b = p[1]
	s.start_host(Config,"Name",9,CAP); login(b)
	lobby = FakeLobby.new(); b.finish({"ok":true,"lobby":lobby})
	check(s.mark_peer_admitted("guest"), "Valid handshake admits guest inside deadline")
	now += Session.ADMISSION_TIMEOUT_MS * 2; s.poll_admission()
	check(not b.busy, "Admitted guest is not evicted when grace expires")
	var departures: Array = []
	s.member_departed.connect(func(id): departures.append(id))
	b.member_departed.emit("other-room", "guest")
	check(departures.is_empty() and s.mark_peer_admitted("guest"), "Stale room notification cannot revoke current admission")
	lobby.members = [FakeMember.new("host")]
	b.member_departed.emit("Lobby123", "guest")
	check(departures == ["guest"], "Membership departure notifies Network without requiring P2P disconnect")
	lobby.members.append(FakeMember.new("guest")); lobby.lobby_updated.emit()
	now += Session.ADMISSION_TIMEOUT_MS; s.poll_admission()
	check(b.pending.operation == "kick", "Same identity rejoining needs a new handshake")
	s.cancel(); b.finish({"ok":true})
	check(s.state == "cancelled" and b.pending.operation == "cleanup", "Late kick completion after cancellation only drains room cleanup")
	b.finish({"ok":true}); dispose(p)
	# Multiple silent members are evicted serially without touching the owner.
	p = fixture(); s = p[0]; b = p[1]
	s.start_host(Config,"Name",9,CAP); login(b)
	lobby = FakeLobby.new()
	for index: int in 14: lobby.members.append(FakeMember.new("silent" + str(index)))
	b.finish({"ok":true,"lobby":lobby})
	now += Session.ADMISSION_TIMEOUT_MS; s.poll_admission()
	var evicted: Array = []
	while b.busy and b.pending.operation == "kick":
		var target: String = b.pending.args.user_id
		evicted.append(target)
		lobby.members = lobby.members.filter(func(member): return member.product_user_id != target)
		b.finish({"ok":true})
	check(evicted.size() == 15 and not "host" in evicted and not b.busy, "All fifteen unauthenticated slots released via serialized kicks")
	dispose(p)
	# Authentication and eviction share the one-callback-at-a-time backend.
	p = fixture(); s = p[0]; b = p[1]
	s.start_host(Config,"Name",9,CAP); login(b)
	lobby = FakeLobby.new(); b.finish({"ok":true,"lobby":lobby})
	now += Session.ADMISSION_TIMEOUT_MS - 1000
	b.auth_expiring.emit()
	now += 1000; s.poll_admission()
	check(b.pending.operation == "login", "Admission expiry does not overlap an outstanding login callback")
	b.finish({"ok":true,"local_user_id":"host"}); s.poll_admission()
	check(b.pending.operation == "kick", "Queued eviction resumes after authentication settles")
	b.finish({"ok":false,"error":"not_owner"})
	check(s.state == "error" and b.peer.closed and b.pending.operation == "cleanup", "Failed SDK eviction closes room instead of pretending capacity was freed")
	b.finish({"ok":true}); dispose(p)
	# Production adapter itself stays parseable and inert without addon.
	var adapter := preload("res://scripts/eos_backend.gd").new()
	root.add_child(adapter)
	check(not adapter.available(), "No SDK means unavailable, never simulated online")
	check(adapter.shutdown().ok, "Uninitialized backend shutdown is inert")
	adapter.busy = true
	check(not adapter.shutdown().ok, "Shutdown refuses pending SDK callbacks")
	adapter.busy = false
	adapter.initialized = true
	adapter.local_user_id = "host"
	var guarded: Dictionary = await adapter._perform("kick", {"lobby": FakeLobby.new(), "user_id": "host"})
	check(not guarded.ok and guarded.error == "invalid_kick_authority", "Real adapter refuses to kick its owner before reaching native API")
	var foreign_lobby := FakeLobby.new()
	foreign_lobby.owner_product_user_id = "someone-else"
	guarded = await adapter._perform("kick", {"lobby": foreign_lobby, "user_id": "guest"})
	check(not guarded.ok and guarded.error == "invalid_kick_authority", "Real adapter cannot kick from a lobby it does not own")
	adapter.initialized = false
	adapter.free()
	print("ONLINE_SESSION_RESULT checks=%d failures=%d" % [checks, failures])
	quit(1 if failures else 0)

