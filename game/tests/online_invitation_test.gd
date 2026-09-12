extends SceneTree
const Invite = preload("res://scripts/online_invitation.gd")
const CAP := "00112233445566778899aabbccddeeff"
var checks := 0
var failures := 0

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("ONLINE_INVITATION FAIL " + label)

func wrap_text(value: String) -> String:
	return wrap_bytes(value.to_utf8_buffer())

func wrap_bytes(value: PackedByteArray) -> String:
	return Invite.PREFIX + Marshalls.raw_to_base64(value).replace("+", "-").replace("/", "_").trim_suffix("=").trim_suffix("=")

func envelope(data: Variant) -> String:
	return wrap_text(JSON.stringify(data))

func reject(value: String, label: String) -> void:
	var result := Invite.decode(value)
	check(not result.get("ok", true) and not str(result.get("error", "")).is_empty() and result.size() == 2, label)

func _initialize() -> void:
	var canonical := Invite.encode("Lobby_A-9", CAP)
	var payload := {"transport":"eos", "v":1, "lobby_id":"Lobby_A-9", "protocol":Invite.PROTOCOL, "capability":CAP}
	var legacy := payload.duplicate(true)
	legacy.protocol = 9
	reject(envelope(legacy), "rc1 protocol rejected before connecting")
	check(Invite.PREFIX == "LMS1-" and Invite.PROTOCOL == 11, "format identity")
	for lobby: String in ["a", "0123456789abcdef0123456789abcdef", "Lobby_A-9", "a".repeat(64)]:
		var code := Invite.encode(lobby, CAP)
		var decoded := Invite.decode(code)
		check(not code.is_empty() and code.length() <= Invite.MAX_LENGTH and decoded.get("ok", false), "roundtrip bounded")
		check(decoded.get("lobby_id") == lobby and decoded.get("capability") == CAP and decoded.get("transport") == "eos" and decoded.get("protocol") == Invite.PROTOCOL and decoded.get("v") == 1, "roundtrip fields")
		check(code == Invite.encode(lobby, CAP), "encoding deterministic")
		var socket := Invite.socket_id(lobby)
		check(socket.length() == 31 and socket.begins_with("LMS") and socket == Invite.socket_id(lobby), "socket stable bounded")
		check(Invite._ascii_token(socket, false), "socket ASCII alphanumeric")
	check(Invite.socket_id("Lobby_A-9") != Invite.socket_id("lobby_A-9"), "opaque lobby case preserved")
	check(Invite.socket_id("a") != Invite.socket_id("b"), "distinct sample sockets")
	for lobby: String in ["", "a".repeat(65), " a", "a ", "a.b", "a/b", "a\\b", "a:b", "á", "a\n", "a\t", "a🙂", "https://example.org", "a@b"]:
		check(not Invite.valid_lobby_id(lobby) and Invite.encode(lobby, CAP).is_empty() and Invite.socket_id(lobby).is_empty(), "invalid lobby")
		var changed := payload.duplicate(true)
		changed.lobby_id = lobby
		reject(envelope(changed), "decode invalid lobby")
	for capability: String in ["", "0".repeat(31), "0".repeat(33), CAP.to_upper(), "g".repeat(32), "_".repeat(32), " " + CAP, CAP + "\n", "á".repeat(32)]:
		check(not Invite.valid_capability(capability) and Invite.encode("a", capability).is_empty(), "invalid capability")
		var changed := payload.duplicate(true)
		changed.capability = capability
		reject(envelope(changed), "decode invalid capability")
	for field: String in ["transport", "v", "lobby_id", "protocol", "capability"]:
		var missing := payload.duplicate(true)
		missing.erase(field)
		reject(envelope(missing), "missing required field")
		for malformed: Variant in [null, true, [], {}]:
			var changed := payload.duplicate(true)
			changed[field] = malformed
			reject(envelope(changed), "wrong field type")
	for field: String in ["v", "protocol"]:
		for malformed: Variant in ["1", str(Invite.PROTOCOL), -1, 0, 1.5, 9.5, 10, 12, 1.0e30]:
			var changed := payload.duplicate(true)
			changed[field] = malformed
			reject(envelope(changed), "wrong version or protocol")
	for field: String in ["host", "port", "puid", "owner_product_user_id", "client_secret", "extra"]:
		var changed := payload.duplicate(true)
		changed[field] = "unexpected"
		reject(envelope(changed), "unexpected field")
	var wrong_transport := payload.duplicate(true)
	wrong_transport.transport = "enet"
	reject(envelope(wrong_transport), "wrong transport")
	for value: String in ["", "LMS1-", "LMS1-A", "LMS1-!", "LMS1-+w", "LMS1-/w", "LMS1-_w=", "lms1-abcd", "DD5-abcd", "LMS2-abcd", " " + canonical, canonical + "\n", canonical + "=", Invite.PREFIX + "a".repeat(509), "LMS1-a b"]:
		reject(value, "bad envelope")
	for malformed: Variant in [null, true, [], "text", 123]:
		reject(envelope(malformed), "non-object JSON")
	for malformed: String in ["{", "{}garbage", JSON.stringify(payload) + " ", " " + JSON.stringify(payload), JSON.stringify(payload).replace("\"v\":1", "\"v\":1,\"v\":1"), JSON.stringify(payload).replace("\"protocol\":%d" % Invite.PROTOCOL, "\"protocol\":%d.0" % Invite.PROTOCOL), JSON.stringify(payload).replace("Lobby_A-9", "\\u004cobby_A-9")]:
		reject(wrap_text(malformed), "noncanonical or malformed JSON")
	for raw: PackedByteArray in [PackedByteArray([255]), PackedByteArray([192, 128]), PackedByteArray([0]), PackedByteArray([239, 187, 191]), PackedByteArray([10])]:
		reject(wrap_bytes(raw), "invalid bytes before JSON")
	# Change unused bits in a final base64 sextet while keeping decoded bytes equal.
	var alphabet := "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
	var noncanonical_tested := false
	for size: int in range(1, 4):
		var candidate := Invite.encode("a".repeat(size), CAP)
		if (candidate.length() - Invite.PREFIX.length()) % 4 in [2, 3]:
			var last := alphabet.find(candidate.right(1))
			reject(candidate.left(candidate.length() - 1) + alphabet.substr(last + 1, 1), "nonzero base64 padding bits")
			noncanonical_tested = true
	check(noncanonical_tested, "padding-bit rejection exercised")
	var generated: Dictionary = {}
	for index: int in 32:
		var capability := Invite.generate_capability()
		check(Invite.valid_capability(capability) and not generated.has(capability), "Crypto capability shape and independent samples")
		generated[capability] = true
		check(Invite.decode(Invite.encode("a", capability)).get("capability") == capability, "generated capability roundtrip")
	print("ONLINE_INVITATION_RESULT checks=%d failures=%d" % [checks, failures])
	quit(failures)
