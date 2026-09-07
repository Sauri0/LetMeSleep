extends Node

# Automated clients exercise exactly the same ENet RPCs as the native UI.
var network: Node
var options: Dictionary
var local_id := 0
var roster: Dictionary = {}
var public_state: Dictionary = {}
var personal: Dictionary = {}
var elapsed := 0.0
var input_seq := 0
var action_seq := 0
var control_accumulator := 0.0
var action_delay := 0.0
var configured := false
var ready_sent := false
var started := false
var start_sent := false
var rounds := 0
var report: Dictionary = {"snapshots":0, "private_packets":0, "private_assignment_seen":false, "privacy_ok":true, "movement_seen":false, "biting_seen":false, "result":{}, "errors":[]}
var initial_position := Vector3.INF
var result_seen := false
var rematch_requested := false
var lobby_retry := 0.0
var lobby_age := 0.0
var lobby_origin := Vector3.INF
var waiting_state: Dictionary = {}
var authority_probe_sent := false
const Cosmetics = preload("res://scripts/cosmetics.gd")

func _ready() -> void:
	network.local_cosmetics = Cosmetics.sanitize({"human":{"color": 3, "accessory": 1}, "mosquito":{"color": 4, "accessory": 2}})
	report["roles_by_round"] = []
	report["results"] = []
	report["lobby_movement_seen"] = false
	report["cosmetics_synced"] = false
	network.waiting_updated.connect(func(data: Dictionary) -> void:
		waiting_state = data
		var me: Dictionary = data.get("actors", {}).get(local_id, {})
		if not me.is_empty():
			if lobby_origin == Vector3.INF:
				lobby_origin = me.p
			elif lobby_origin.distance_to(me.p) > 0.3:
				report.lobby_movement_seen = true)
	network.accepted.connect(func(id: int) -> void: local_id = id)
	network.lobby_updated.connect(_lobby)
	network.snapshot_updated.connect(_snapshot)
	network.private_updated.connect(_private)
	network.notice.connect(func(message: String) -> void:
		print("BOT_NOTICE %s" % message)
		if message.contains("incompatible"):
			report["incompatible_rejected"] = true
			_finish())
	network.disconnected.connect(func() -> void:
		report["disconnected"] = true
		if started and not result_seen:
			report.errors.append("transport disconnected"))
	var create := options.has("create")
	if options.has("incompatible"):
		network.multiplayer.connected_to_server.disconnect(network._connected)
		network.multiplayer.connected_to_server.connect(func() -> void:
			network._request_join.rpc_id(1, "0.0.invalid", -99, "OldClient", "", true, ""))
	network.connect_room(str(options.get("address", "127.0.0.1")), int(options.get("port", "27840")), str(options.get("name", "Bot")), str(options.get("code", "")), create)

func _lobby(data: Dictionary) -> void:
	roster = data
	if started:
		report["returned_to_lobby"] = true
		if options.has("disconnect-test") or rounds >= int(options.get("rounds", "1")):
			_finish()
			return
		started = false
		result_seen = false
		start_sent = false
		rematch_requested = false
		input_seq = 0
		action_seq = 0
		lobby_age = 0.0
		personal.clear()
		initial_position = Vector3.INF
	if options.has("create") and not configured:
		configured = true
		var config: Dictionary = data.config.duplicate(true)
		config.mode = str(options.get("mode", "blood"))
		config.human_count = int(options.get("humans", "1"))
		config.round_seconds = 30
		config.blood_goal = 5
		config.task_goal = 1
		config.task_interval = 12
		config.task_deadline = 8
		config.task_work = 1
		config.task_floor = 4
		config.mosquito_lives = 3
		network.lobby_action("config", config)
		var code_file := str(options.get("code-file", ""))
		if not code_file.is_empty():
			var file := FileAccess.open(code_file, FileAccess.WRITE)
			file.store_string(data.code)
		return
	var me: Dictionary = data.players.get(local_id, {})
	if not authority_probe_sent and lobby_age > 0.4:
		authority_probe_sent = true
		network.lobby_action("role", "human")
		if not options.has("create"):
			var forbidden: Dictionary = data.config.duplicate(true)
			forbidden.human_count = 5
			network.lobby_action("config", forbidden)
			network.lobby_action("start")
	if str(me.get("role", "")) != "waiting":
		report.errors.append("role assigned before round")
	report["cosmetics_synced"] = me.get("cosmetics", {}) == network.local_cosmetics
	if lobby_age < 2.0:
		return
	if not bool(me.get("ready", false)):
		network.lobby_action("ready", true)
		return
	if options.has("create") and data.players.size() == int(options.get("players", "2")) and bool(data.can_start) and not start_sent:
		start_sent = true
		network.lobby_action("start")

func _snapshot(data: Dictionary) -> void:
	public_state = data
	report.snapshots += 1
	for id: int in data.get("actors", {}):
		for key: String in ["assignment", "zone", "target", "rotation_at", "next_rotation", "blocked_zone"]:
			if data.actors[id].has(key):
				report.privacy_ok = false
	if str(data.get("phase", "")) == "playing":
		if not started:
			rounds += 1
			input_seq = 0
			action_seq = 0
			var mine: Dictionary = data.get("actors", {}).get(local_id, {})
			report.roles_by_round.append(mine.get("role", ""))
			var humans := 0
			for actor: Dictionary in data.get("actors", {}).values():
				if actor.get("role", "") == "human":
					humans += 1
			if humans != int(data.config.human_count):
				report.errors.append("incorrect human count")
			if mine.get("appearance", {}) != Cosmetics.appearance_for(network.local_cosmetics, str(mine.get("role", ""))):
				report.errors.append("round appearance mismatch")
			if options.has("create"):
				var forbidden: Dictionary = data.config.duplicate(true)
				forbidden.human_count = 5
				network.lobby_action("config", forbidden)
				network.lobby_action("ready", false)
				network.lobby_action("cosmetics", {})
		started = true
		var me: Dictionary = data.get("actors", {}).get(local_id, {})
		if not me.is_empty():
			if initial_position == Vector3.INF:
				initial_position = me.p
			elif initial_position.distance_to(me.p) > 0.4:
				report.movement_seen = true
			if str(me.get("state", "")) == "biting":
				report.biting_seen = true
	elif str(data.get("phase", "")) == "results" and not result_seen:
		result_seen = true
		report.result = {"winner": data.get("winner", ""), "reason": data.get("reason", ""), "blood": data.get("blood", 0), "tasks_done": data.get("tasks_done", 0)}
		report.results.append(report.result.duplicate(true))
		if options.has("rematch-test") or rounds < int(options.get("rounds", "1")):
			if options.has("create"):
				rematch_requested = true
				get_tree().create_timer(0.8).timeout.connect(func() -> void: network.lobby_action("rematch"))
		else:
			get_tree().create_timer(0.7).timeout.connect(_finish)

func _private(data: Dictionary) -> void:
	personal = data
	report.private_packets += 1
	var role := str(public_state.get("actors", {}).get(local_id, {}).get("role", ""))
	if not data.get("assignment", {}).is_empty():
		report.private_assignment_seen = true
		if role != "mosquito":
			report.privacy_ok = false

func _process(dt: float) -> void:
	elapsed += dt
	action_delay -= dt
	if elapsed > float(options.get("timeout", "50")):
		report.errors.append("timeout")
		_finish()
		return
	if not started or result_seen:
		lobby_age += dt
		if not started and not roster.is_empty():
			input_seq += 1
			network.send_input(input_seq, Vector3(0.0, 0, 0.5) if lobby_age < 1.5 else Vector3.ZERO, 0.0, 0.0, false)
		lobby_retry += dt
		if not started and not roster.is_empty() and lobby_retry > 0.75:
			lobby_retry = 0.0
			start_sent = false
			_lobby(roster)
		return
	if options.has("disconnect-after") and elapsed > float(options["disconnect-after"]):
		report["intentional_disconnect"] = true
		network.close_client()
		_finish()
		return
	control_accumulator += dt
	if control_accumulator < 0.0333:
		return
	control_accumulator = 0.0
	var me: Dictionary = public_state.get("actors", {}).get(local_id, {})
	if me.is_empty() or not bool(me.get("alive", false)):
		return
	var move := Vector3.ZERO
	var interact := false
	var yaw := 0.0
	var pitch := 0.0
	var mode := str(public_state.get("config", {}).get("mode", "blood"))
	if str(me.role) == "mosquito":
		var assignment: Dictionary = personal.get("assignment", {})
		if not assignment.is_empty() and str(me.state) != "biting" and mode == "blood":
			var destination: Vector3 = assignment.p + assignment.normal * 0.20
			if (Vector3(me.p) - Vector3(assignment.p)).dot(assignment.normal) < 0.35:
				# Approach the exposed face above head height instead of crossing the human.
				destination = assignment.p + assignment.normal * 0.80
				destination.y = 2.30
			var delta: Vector3 = destination - me.p
			move = delta.normalized() if delta.length() > 0.15 else Vector3.ZERO
			if delta.length() < 0.48 and action_delay <= 0.0:
				act("bite")
		elif mode == "survival":
			move = Vector3(sin(elapsed), 0, cos(elapsed)) * 0.6
	elif mode == "sleep" and not options.has("idle"):
		var task: Dictionary = personal.get("task", {})
		if not task.is_empty():
			var delta: Vector3 = task.get("p", Vector3.ZERO) - me.p
			delta.y = 0.0
			move = delta.normalized() if delta.length() > 0.70 else Vector3.ZERO
			interact = delta.length() < 1.35
			if delta.length() > 0.05:
				yaw = atan2(-delta.x, -delta.z)
				move = move.rotated(Vector3.UP, -yaw)
	input_seq += 1
	network.send_input(input_seq, move, yaw, pitch, interact)

func act(verb: String) -> void:
	action_seq += 1
	network.send_action(action_seq, verb)
	action_delay = 0.5

func _finish() -> void:
	set_process(false)
	report["peer_id"] = local_id
	report["elapsed"] = elapsed
	var report_path := str(options.get("report", ""))
	if not report_path.is_empty():
		var file := FileAccess.open(report_path, FileAccess.WRITE)
		file.store_string(JSON.stringify(report, "\t"))
	print("BOT_REPORT " + JSON.stringify(report))
	get_tree().quit(0 if report.errors.is_empty() and bool(report.privacy_ok) else 1)
