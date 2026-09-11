extends SceneTree

const Audit = preload("res://tests/network_privacy_audit.gd")
var checks := 0
var failures := 0

func check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("NETWORK_PRIVACY_AUDIT_FAIL " + label)

func public_packet(tick: int, role: String, phase: String = "playing") -> Dictionary:
	return {"tick": tick, "phase": phase, "actors": {42: {"role": role, "alive": true, "threatened": false, "bitten": false}, 77: {"role": "human"}}}

func _initialize() -> void:
	var visible_contact := public_packet(1, "mosquito")
	visible_contact.actors[42].state = "biting"
	visible_contact.actors[42].attached_to = 77
	visible_contact.actors[77].alive = true
	var contact_audit = Audit.new()
	contact_audit.record_public(visible_contact, 42)
	check(contact_audit.ok(), "actual visible bite may identify its human without revealing zone")
	for invalid_state: String in ["flying", "perched", "stunned", "dead"]:
		var invalid_contact := visible_contact.duplicate(true)
		invalid_contact.actors[42].state = invalid_state
		var leak_audit = Audit.new()
		leak_audit.record_public(invalid_contact, 42)
		check(not leak_audit.ok(), "reserved human cannot leak while " + invalid_state)
	for invalid_parent: Variant in [-1, 77.0, 999]:
		var invalid_contact := visible_contact.duplicate(true)
		invalid_contact.actors[42].attached_to = invalid_parent
		var invalid_audit = Audit.new()
		invalid_audit.record_public(invalid_contact, 42)
		check(not invalid_audit.ok(), "invalid attachment parent rejected " + str(invalid_parent))
	var first = Audit.new()
	first.record_private({"tick": 12, "assignment": {"human": 77, "zone": 3}})
	check(first.pending_count() == 1 and first.failures.is_empty() and not first.ok(), "private before first public is pending, not a human leak or a premature pass")
	first.record_public(public_packet(11, "mosquito"), 42)
	check(first.pending_count() == 1 and first.matched_count == 0, "older public evidence cannot release a future private tick")
	first.record_public(public_packet(12, "mosquito"), 42)
	check(first.ok() and first.pending_count() == 0 and first.deferred_count == 1 and first.matched_count == 1, "matching first mosquito snapshot validates deferred assignment once")

	var next_round = Audit.new()
	next_round.record_public(public_packet(100, "human"), 42)
	next_round.record_private({"tick": 201, "assignment": {"zone": 4}})
	check(next_round.pending_count() == 1 and next_round.failures.is_empty(), "old human role cannot condemn a private packet from a future round")
	next_round.record_public(public_packet(180, "human", "results"), 42)
	next_round.record_public(public_packet(200, "waiting", "waiting"), 42)
	check(next_round.pending_count() == 1 and not next_round.ok(), "older results and lobby do not validate next-round assignment")
	next_round.record_public(public_packet(202, "mosquito"), 42)
	check(next_round.ok() and next_round.matched_count == 1, "new-round mosquito evidence resolves packet despite skipped exact public tick")

	var leak = Audit.new()
	leak.record_public(public_packet(50, "human"), 42)
	leak.record_private({"tick": 50, "assignment": {"zone": 7}})
	check(not leak.ok() and leak.failures.size() == 1 and leak.pending_count() == 0, "assignment actually delivered to a human is a proven failure")
	leak.record_public(public_packet(70, "mosquito"), 42)
	check(not leak.ok(), "later role change cannot erase a detected leak")

	for field: String in ["focus", "assignment", "zone", "target", "target_id", "rotation_at", "next_rotation", "blocked_zone", "_focus_progress", "task", "attack", "bite_feedback", "stun", "help", "throw"]:
		var public_leak = Audit.new()
		var packet: Dictionary = public_packet(10, "human")
		packet.actors[77][field] = {}
		public_leak.record_public(packet, 42)
		check(not public_leak.ok() and public_leak.failures.size() == 1, "public forbidden field detected even on another actor: " + field)

	var unresolved = Audit.new()
	unresolved.record_private({"tick": 900, "assignment": {}})
	unresolved.record_public(public_packet(899, "human"), 42)
	check(not unresolved.ok() and unresolved.pending_count() == 1 and unresolved.matched_count == 0, "even empty private packets remain unverified at finish without fresh role evidence")

	var result_packet = Audit.new()
	result_packet.record_public(public_packet(50, "mosquito"), 42)
	result_packet.record_private({"tick": 60, "assignment": {"zone": 2}})
	result_packet.record_public(public_packet(60, "mosquito", "results"), 42)
	check(result_packet.ok() and result_packet.pending_count() == 0, "final result snapshot verifies final private packet using authoritative retained role")

	var ordered = Audit.new()
	ordered.record_private({"tick": 20, "assignment": {"zone": 2}})
	ordered.record_private({"tick": 10, "assignment": {}})
	ordered.record_public(public_packet(10, "human"), 42)
	check(ordered.pending_count() == 1 and ordered.matched_count == 1, "one public snapshot only resolves private ticks it actually covers")
	ordered.record_public(public_packet(20, "mosquito"), 42)
	check(ordered.ok() and ordered.matched_count == 2, "reordered private queue resolves against corresponding public role evidence")
	ordered.record_private({"tick": 10, "assignment": {}})
	check(ordered.ok() and ordered.matched_count == 3, "delayed private packet uses earliest sufficient historical public snapshot")

	var copied = Audit.new()
	var original := {"tick": 4, "assignment": {"zone": 2}}
	copied.record_private(original)
	original.assignment.clear()
	copied.record_public(public_packet(4, "human"), 42)
	check(not copied.ok(), "payload mutation cannot erase deferred evidence")

	var malformed = Audit.new()
	malformed.record_private({"tick": NAN, "assignment": {}})
	malformed.record_public({"phase": "playing", "actors": {}}, 42)
	check(not malformed.ok() and malformed.failures.size() == 2, "missing or malformed tick is reported rather than silently discarded")
	var human_feedback = Audit.new()
	human_feedback.record_private({"tick": 91, "attack": {"id": 1, "state": "hit"}, "bite_feedback": {"id": 2, "active": true}})
	check(human_feedback.pending_count() == 1 and human_feedback.failures.is_empty(), "private human feedback waits for its matching public role")
	human_feedback.record_public(public_packet(91, "human"),42)
	check(human_feedback.ok(), "human recipient may receive its own manual hit and actual bite feedback")
	var wrong_feedback = Audit.new()
	wrong_feedback.record_public(public_packet(3, "mosquito"),42)
	wrong_feedback.record_private({"tick":3,"bite_feedback":{"active":true}})
	check(not wrong_feedback.ok(), "human contact feedback must not be sent to another role")
	var wrong_focus = Audit.new()
	wrong_focus.record_public(public_packet(3, "human"),42)
	wrong_focus.record_private({"tick":3,"focus":{"state":"waiting"},"assignment":{}})
	check(not wrong_focus.ok(), "even waiting concentration is mosquito-private")
	var physical = Audit.new()
	var gesture: Dictionary = public_packet(4,"human")
	gesture.actors[42].strike = {"id":1,"origin":Vector3.ZERO,"point":Vector3.FORWARD,"direction":Vector3.FORWARD,"hand":"left","tool":"hands","kind":"body","progress":0.5,"active":true}
	physical.record_public(gesture,42)
	check(physical.ok(), "public physical gesture is allowed without target or reservation")
	var throwing = Audit.new()
	var throw_packet: Dictionary = public_packet(5,"human")
	throw_packet.actors[42].throw_gesture = {"id":2,"state":"charging","progress":.4,"tool":"slipper","direction":Vector3.FORWARD}
	throwing.record_public(throw_packet,42)
	throwing.record_private({"tick":5,"throw":{"can_throw":true,"power":.4}})
	check(throwing.ok(), "public throw pose and private owner authorization coexist")
	var wrong_throw = Audit.new()
	wrong_throw.record_public(public_packet(6,"mosquito"),42)
	wrong_throw.record_private({"tick":6,"throw":{"can_throw":true}})
	check(not wrong_throw.ok(), "throw authorization cannot reach mosquito role")
	var stunned = Audit.new()
	stunned.record_private({"tick":8,"stun":{"active":true,"remaining":35},"help":{"state":"idle"},"assignment":{}})
	var visible_stun: Dictionary = public_packet(8,"mosquito")
	visible_stun.actors[42].state = "stunned"
	visible_stun.actors[42].alive = true
	visible_stun.actors[77].help_target = 42
	stunned.record_public(visible_stun,42)
	check(stunned.ok(), "public stun state and physical helper target coexist with private recovery countdown")
	for field: String in ["stun", "help"]:
		var leaked = Audit.new()
		leaked.record_public(public_packet(9,"human"),42)
		leaked.record_private({"tick":9,field:{"remaining":12}})
		check(not leaked.ok(), "private mosquito " + field + " cannot reach human")
	print("NETWORK_PRIVACY_AUDIT_RESULT checks=%d failures=%d" % [checks, failures])
	quit(failures)
