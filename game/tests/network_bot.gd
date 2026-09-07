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
const Invite = preload("res://scripts/invitation.gd")
const Brain = preload("res://scripts/bot_brain.gd")
const PrivacyAudit = preload("res://tests/network_privacy_audit.gd")
var pilot: RefCounted
var privacy_audit := PrivacyAudit.new()
var finishing := false
var was_stunned := false

func _ready() -> void:
	network.local_cosmetics = Cosmetics.sanitize({"human":{"color": 3, "accessory": 1}, "mosquito":{"color": 4, "accessory": 2}})
	report["roles_by_round"] = []
	report["results"] = []
	report["lobby_movement_seen"] = false
	report["cosmetics_synced"] = false
	report["lobby_jump_seen"] = false
	report["lobby_crouch_seen"] = false
	report["lobby_sprint_seen"] = false
	network.waiting_updated.connect(func(data: Dictionary) -> void:
		waiting_state = data
		var me: Dictionary = data.get("actors", {}).get(local_id, {})
		if not me.is_empty():
			var stunned: bool = str(me.get("state", "")) == "stunned"
			if stunned:
				report["stun_seen"] = true
				if not bool(me.get("alive",false)):
					report.errors.append("stunned actor incorrectly marked dead")
			elif was_stunned:
				report["stun_recovery_seen"] = true
			was_stunned = stunned
			if int(me.get("help_target",0)) != 0:
				report["help_seen"] = true
			if float(me.p.y) > 0.15:
				report.lobby_jump_seen = true
			if float(me.get("crouch_amount",0.0)) > 0.5:
				report.lobby_crouch_seen = true
			if bool(me.get("sprinting",false)):
				report.lobby_sprint_seen = true
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
			network._request_join.rpc_id(1, str(options.get("join-version", "0.0.invalid")), int(options.get("join-protocol", "-99")), "OldClient", "", true, ""))
	var address := str(options.get("address", "127.0.0.1"))
	var port := int(options.get("port", "27840"))
	var room := str(options.get("code", ""))
	if options.has("invitation"):
		var decoded: Dictionary = Invite.decode(str(options.invitation))
		if not bool(decoded.ok):
			report.errors.append(decoded.error)
			_finish()
			return
		address = decoded.host
		port = decoded.port
		room = decoded.room
		report["invitation_decoded"] = true
	network.connect_room(address,port,str(options.get("name", "Bot")),room,create)

func _lobby(data: Dictionary) -> void:
	if roster.is_empty():
		lobby_age = 0.0
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
		config.round_seconds = 60
		config.blood_goal = 3
		config.task_goal = 1
		config.task_interval = 25
		config.task_deadline = 23
		config.task_work = 1
		config.task_floor = 4
		config.mosquito_lives = 3
		network.lobby_action("config", config)
		var code_file := str(options.get("code-file", ""))
		if not code_file.is_empty():
			var file := FileAccess.open(code_file, FileAccess.WRITE)
			file.store_string(Invite.encode(str(options["shared-host"]),int(options.get("port","27840")),data.code) if options.has("shared-host") else str(data.code))
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
	if lobby_age < 3.5 or not (report.lobby_movement_seen and report.lobby_jump_seen and report.lobby_crouch_seen and report.lobby_sprint_seen):
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
	privacy_audit.record_public(data,local_id)
	if str(data.get("phase", "")) == "playing":
		if not started:
			pilot = Brain.new()
			pilot.setup(local_id)
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
	if finishing:
		return
	privacy_audit.record_private(data)
	personal = data
	if data.get("focus",{}).get("state","") == "charging":
		report["focus_seen"] = true
	if data.get("focus",{}).get("state","") == "waiting":
		report["assignment_wait_seen"] = true
	if str(data.get("attack",{}).get("state","idle")) in ["windup", "hit", "miss"]:
		report["attack_feedback_seen"] = true
	if bool(data.get("bite_feedback",{}).get("active",false)):
		report["bite_feedback_seen"] = true
	report.private_packets += 1
	if not data.get("assignment", {}).is_empty():
		report.private_assignment_seen = true

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
			# Advance after authoritative acknowledgement, not a narrow wall-clock
			# pulse that can disappear while sixteen processes start together.
			var moving: bool = not (report.lobby_movement_seen and report.lobby_sprint_seen)
			var jumping: bool = not moving and not bool(report.lobby_jump_seen)
			var crouching: bool = not moving and not jumping and not bool(report.lobby_crouch_seen)
			# The first observation can arrive after earlier input already reached a
			# wall. Walk toward the opposite half from that fixed observed origin.
			var toward_center: float = -0.5 if lobby_origin != Vector3.INF and lobby_origin.z >= 0.0 else 0.5
			network.send_input(input_seq, Vector3(0.0,0,toward_center) if moving else Vector3.ZERO, 0.0,0.0,false,moving,crouching,jumping and fmod(lobby_age,1.5)<1.0)
		lobby_retry += dt
		if not started and not roster.is_empty() and lobby_retry > 0.75:
			lobby_retry = 0.0
			start_sent = false
			_lobby(roster)
		return
	if options.has("disconnect-after") and elapsed > float(options["disconnect-after"]):
		report["intentional_disconnect"] = true
		_finish()
		return
	control_accumulator += dt
	if control_accumulator < 0.0333:
		return
	control_accumulator = 0.0
	var me: Dictionary = public_state.get("actors", {}).get(local_id, {})
	if me.is_empty() or not bool(me.get("alive", false)):
		return
	var mode := str(public_state.get("config", {}).get("mode", "blood"))
	var intent := {"move":Vector3.ZERO,"yaw":float(me.yaw),"pitch":float(me.pitch),"interact":false,"sprint":false,"crouch":false,"jump":false,"action":""}
	if str(me.role) == "mosquito" or (mode == "sleep" and not options.has("idle")):
		intent = pilot.decide(public_state,personal,0.0333)
	input_seq += 1
	network.send_input(input_seq,intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
	if not str(intent.action).is_empty():
		act(intent.action, float(intent.yaw), float(intent.pitch))
func act(verb: String, aim_yaw: float = NAN, aim_pitch: float = NAN) -> void:
	action_seq += 1
	network.send_action(action_seq, verb, aim_yaw, aim_pitch)
	if verb in ["attack", "self_swat"]:
		report["manual_action_packets"] = int(report.get("manual_action_packets",0)) + 1
	action_delay = 0.5

func _finish() -> void:
	if finishing:
		return
	finishing = true
	set_process(false)
	# Freeze private observations, then wait for the corresponding public ticks.
	# An unresolved packet makes this test fail instead of silently passing it.
	for attempt: int in range(20):
		if privacy_audit.pending_count() == 0:
			break
		await get_tree().create_timer(0.1).timeout
	report.privacy_ok = privacy_audit.ok()
	report["privacy_failures"] = privacy_audit.failures.duplicate()
	report["privacy_pending"] = privacy_audit.pending_count()
	report["privacy_deferred"] = privacy_audit.deferred_count
	report["privacy_matched"] = privacy_audit.matched_count
	report["peer_id"] = local_id
	report["elapsed"] = elapsed
	report["lobby_origin"] = lobby_origin
	report["lobby_final_position"] = waiting_state.get("actors",{}).get(local_id,{}).get("p",Vector3.INF)
	var report_path := str(options.get("report", ""))
	if not report_path.is_empty():
		var file := FileAccess.open(report_path, FileAccess.WRITE)
		file.store_string(JSON.stringify(report, "\t"))
	print("BOT_REPORT " + JSON.stringify(report))
	get_tree().quit(0 if report.errors.is_empty() and bool(report.privacy_ok) else 1)
