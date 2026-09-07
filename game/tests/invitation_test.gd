extends SceneTree
const Invite = preload("res://scripts/invitation.gd")
var checks := 0
var failures := 0
func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("INVITATION FAIL " + label)
func envelope(data: Variant) -> String:
	return "DD3-" + Marshalls.raw_to_base64(JSON.stringify(data).to_utf8_buffer()).replace("+", "-").replace("/", "_").trim_suffix("=").trim_suffix("=")
func _initialize() -> void:
	for host: String in ["192.168.1.24", "203.0.113.10", "casa.example.org", "2001:db8::123"]:
		for port: int in [1024,27840,65535]:
			var code := Invite.encode(host,port,"ABC234")
			var decoded := Invite.decode(code)
			check(decoded.get("ok",false) and decoded.get("host") == host and decoded.get("port") == port and decoded.get("room") == "ABC234", "roundtrip " + host)
	for host: String in ["", "127.0.0.1", "127.23.1.2", "localhost", "::1", "0.0.0.0", "::", "https://example.org", "foo;quit", "$(echo hi)", "a@b", "a b", "a\\b", "a..b", "-bad.example", "999.999.1.1", "x".repeat(254)]:
		check(Invite.encode(host,27840,"ABC234").is_empty(), "reject host " + host)
	for value: String in ["", "ABC234", "DD2-abcd", "DD3-!", "DD3-A", "DD3-a".repeat(150), envelope([]), envelope({"v":2}), envelope({"v":1,"host":true,"port":27840,"room":"ABC234"})]:
		check(not Invite.decode(value).ok, "reject envelope")
	for port: Variant in [null, true, "27840", 0, 1023, 65536, 27840.5]:
		check(not Invite.decode(envelope({"v":1,"host":"192.168.1.24","port":port,"room":"ABC234"})).ok, "reject port")
	for room: String in ["", "ABC", "abcdef", "ABC234;quit", "ABC2345"]:
		check(not Invite.decode(envelope({"v":1,"host":"192.168.1.24","port":27840,"room":room})).ok, "reject room")
	check(Invite.decode("  " + Invite.encode("192.168.1.24",27840,"ABC234") + "\n").ok, "paste whitespace")
	print("INVITATION_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
