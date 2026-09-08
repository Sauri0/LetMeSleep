extends SceneTree
const Invite = preload("res://scripts/invitation.gd")
var checks := 0
var failures := 0
func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("INVITATION FAIL " + label)
func envelope(data: Variant, protocol: Variant=Invite.PROTOCOL) -> String:
	if data is Dictionary:
		data=data.duplicate(true)
		data.protocol=protocol
	return Invite.PREFIX + Marshalls.raw_to_base64(JSON.stringify(data).to_utf8_buffer()).replace("+", "-").replace("/", "_").trim_suffix("=").trim_suffix("=")
func _initialize() -> void:
	for host: String in ["192.168.1.24", "203.0.113.10", "casa.example.org", "2001:db8::123"]:
		for port: int in [1024,27840,65535]:
			var code := Invite.encode(host,port,"ABC234")
			var decoded := Invite.decode(code)
			check(decoded.get("ok",false) and decoded.get("host") == host and decoded.get("port") == port and decoded.get("room") == "ABC234", "roundtrip " + host)
	for host: String in ["", "127.0.0.1", "127.23.1.2", "localhost", "::1", "0.0.0.0", "::", "https://example.org", "foo;quit", "$(echo hi)", "a@b", "a b", "a\\b", "a..b", "-bad.example", "999.999.1.1", "x".repeat(254)]:
		check(Invite.encode(host,27840,"ABC234").is_empty(), "reject host " + host)
	for value: String in ["", "ABC234", "DD2-abcd", "DD3-abcd", Invite.PREFIX+"!", Invite.PREFIX+"A", (Invite.PREFIX+"a").repeat(150), envelope([]), envelope({"v":2}), envelope({"v":1,"host":true,"port":27840,"room":"ABC234"})]:
		check(not Invite.decode(value).ok, "reject envelope")
	for port: Variant in [null, true, "27840", 0, 1023, 65536, 27840.5]:
		check(not Invite.decode(envelope({"v":1,"host":"192.168.1.24","port":port,"room":"ABC234"})).ok, "reject port")
	for room: String in ["", "ABC", "abcdef", "ABC234;quit", "ABC2345"]:
		check(not Invite.decode(envelope({"v":1,"host":"192.168.1.24","port":27840,"room":room})).ok, "reject room")
	check(Invite.decode("  " + Invite.encode("192.168.1.24",27840,"ABC234") + "\n").ok, "paste whitespace")
	for protocol: Variant in [null,true,"8",7,9,8.5]:
		check(not Invite.decode(envelope({"v":1,"host":"192.168.1.24","port":27840,"room":"ABC234"},protocol)).ok,"reject mismatched or malformed protocol")
	var old: String=Invite.encode("192.168.1.24",27840,"ABC234").replace(Invite.PREFIX,"DD3-")
	check(not Invite.decode(old).ok and "anterior" in str(Invite.decode(old).error),"old checkpoint invitation gives a clear incompatibility reason")
	for sample: Array in [["10.0.0.7","private"],["172.16.3.2","private"],["172.31.255.254","private"],["192.168.1.25","private"],["100.64.0.1","shared"],["100.127.255.254","shared"],["127.0.0.1","loopback"],["169.254.2.7","link_local"],["224.0.0.1","multicast"],["203.0.113.7","reserved"],["fd00::7","private"],["fe80::7","link_local"],["2001:db8::7","reserved"],["casa.local","local_name"],["casa.example.org","hostname"],["8.8.8.8","public_ipv4"],["2606:4700:4700::1111","public_ipv6"]]:
		check(Invite.classify_host(sample[0]) == sample[1], "classify endpoint " + sample[0])
	for host: String in ["192.168.1.25","10.0.0.7","172.20.1.8","100.70.4.3","fd00::7","casa.local"]:
		check(Invite.encode(host,27840,"ABC234","internet").is_empty(), "other-house scope rejects non-public endpoint " + host)
		check(not Invite.decode(envelope({"v":1,"host":host,"port":27840,"room":"ABC234","scope":"internet"})).ok, "decode enforces the scope/address match")
	for scope: String in ["lan","virtual"]:
		var decoded := Invite.decode(Invite.encode("192.168.1.25",27840,"ABC234",scope))
		check(decoded.ok and decoded.scope == scope, "non-public scoped roundtrip " + scope)
	check(Invite.decode(Invite.encode("casa.example.org",27840,"ABC234","internet")).scope == "internet", "public hostname carries an explicit other-house scope without claiming reachability")
	for scope: Variant in [true,5,[],{},"unrecognized"]:
		check(not Invite.decode(envelope({"v":1,"host":"casa.example.org","port":27840,"room":"ABC234","scope":scope})).ok, "malformed scope cannot bypass validation")
	print("INVITATION_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
