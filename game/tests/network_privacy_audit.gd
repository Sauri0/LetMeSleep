extends RefCounted
## Test-only observer of accepted network packets. Channels can arrive in either
## order: a future private tick must never be classified using an older role.

const FORBIDDEN_PUBLIC_KEYS := ["assignment", "assignments", "zone", "target", "target_id", "rotation_at", "next_rotation", "blocked_zone", "focus", "task", "attack", "throw", "bite_feedback", "stun", "help", "surface", "support_id"]
var failures: Array[String] = []
var deferred_count := 0
var matched_count := 0
var _pending: Array[Dictionary] = []
var _public_roles: Array[Dictionary] = []

func record_public(data: Dictionary, local_id: int) -> void:
	_scan_public(data, "public")
	if not _valid_tick(data):
		_fail("public packet has no valid integer tick")
		return
	if str(data.get("phase", "")) not in ["playing", "results"]:
		return
	var actors: Variant = data.get("actors", {})
	if not actors is Dictionary:
		_fail("public actors must be a dictionary")
		return
	var actor: Variant = actors.get(local_id, {})
	if not actor is Dictionary:
		_fail("public local actor must be a dictionary")
		return
	var role: String = str(actor.get("role", ""))
	if role not in ["human", "mosquito"]:
		return # Missing role is not proof that this recipient was human.
	var tick: int = int(data.tick)
	var slot: int = _lower_bound(tick)
	if slot < _public_roles.size() and int(_public_roles[slot].tick) == tick:
		if str(_public_roles[slot].role) != role:
			_fail("contradictory public recipient roles at tick%d" % tick)
	else:
		_public_roles.insert(slot, {"tick": tick, "role": role})
	var remaining: Array[Dictionary] = []
	for packet: Dictionary in _pending:
		if not _match(packet):
			remaining.append(packet)
	_pending = remaining

func record_private(data: Dictionary) -> void:
	if not _valid_tick(data):
		_fail("private packet has no valid integer tick")
		return
	var assignment: Variant = data.get("assignment", {})
	if not assignment is Dictionary:
		_fail("private assignment must be a dictionary at tick%d" % int(data.tick))
		return
	# Store only immutable audit facts, not a reference to the received payload.
	var packet := {"tick": int(data.tick), "assignment_present": not assignment.is_empty(), "focus_present": data.has("focus") or data.has("stun") or data.has("help") or data.has("surface"), "human_feedback_present": data.has("attack") or data.has("throw") or data.has("bite_feedback")}
	if not _match(packet):
		_pending.append(packet)
		deferred_count += 1

func pending_count() -> int:
	return _pending.size()

func ok() -> bool:
	# The harness calls this at finish. Pending evidence is a failure to verify,
	# never a pass; it may continue receiving public packets before that barrier.
	return failures.is_empty() and _pending.is_empty()

func _match(packet: Dictionary) -> bool:
	var slot: int = _lower_bound(int(packet.tick))
	if slot == _public_roles.size():
		return false
	var evidence: Dictionary = _public_roles[slot]
	matched_count += 1
	if bool(packet.assignment_present) and str(evidence.role) != "mosquito":
		_fail("private assignment reached human at tick%d (public evidence tick%d)" % [int(packet.tick), int(evidence.tick)])
	if bool(packet.focus_present) and str(evidence.role) != "mosquito":
		_fail("private mosquito concentration/recovery/help reached human at tick%d" % int(packet.tick))
	if bool(packet.human_feedback_present) and str(evidence.role) != "human":
		_fail("private human contact feedback reached mosquito at tick%d" % int(packet.tick))
	return true

func _lower_bound(tick: int) -> int:
	var low := 0
	var high: int = _public_roles.size()
	while low < high:
		var middle: int = (low + high) / 2
		if int(_public_roles[middle].tick) < tick:
			low = middle + 1
		else:
			high = middle
	return low

func _valid_tick(data: Dictionary) -> bool:
	return data.get("tick") is int and int(data.tick) >= 0

func _scan_public(value: Variant, path: String) -> void:
	if value is Dictionary:
		for key: Variant in value:
			var label: String = str(key)
			var location: String = path + "." + label
			if label in FORBIDDEN_PUBLIC_KEYS or label.begins_with("_"):
				_fail("forbidden public field " + location)
			_scan_public(value[key], location)
	elif value is Array:
		for index: int in range(value.size()):
			_scan_public(value[index], "%s[%d]" % [path, index])

func _fail(message: String) -> void:
	if not failures.has(message):
		failures.append(message)
