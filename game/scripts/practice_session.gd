class_name PracticeSession
extends Node
## In-process authority for solo practice. Uses the multiplayer rules unchanged.
signal snapshot_updated(data: Dictionary)
signal private_updated(data: Dictionary)
const Simulation = preload("res://scripts/simulation.gd")
const Brain = preload("res://scripts/bot_brain.gd")
const Maps = preload("res://scripts/map_catalog.gd")
## Decisions are spread over three physics ticks. Held input still reaches the
## authority every tick; one-shot actions are submitted only on a new decision.
const BOT_DECISION_SECONDS := 1.0/20.0
var bot_schedule: Dictionary = {}
var sim: RefCounted
var brains: Dictionary = {}
var input_sequences: Dictionary = {}
var action_sequences: Dictionary = {}
var selected_role := "human"
var selected_mode := "blood"
var local_cosmetics: Dictionary = {}
var player_name := "Vos"
var publication_age := 0.0
var active := false
var selected_config: Dictionary={}

func start(role: String, mode: String, cosmetics: Dictionary, display_name: String = "Vos", config_override: Dictionary = {}) -> void:
	stop()
	selected_role = role if role in ["human","mosquito"] else "human"
	selected_mode = mode if mode in ["blood","survival","sleep"] else "blood"
	local_cosmetics = cosmetics.duplicate(true)
	player_name = display_name if not display_name.is_empty() else "Vos"
	var config: Dictionary = Simulation.DEFAULT_CONFIG.duplicate(true)
	config.merge(config_override,true)
	config.mode = selected_mode
	config.human_count = 1
	config.map_id = str(config_override.get("map_id",Maps.default_map_id()))
	if not Maps.is_playable(str(config.map_id)):
		config.map_id = Maps.default_map_id()
	selected_config=config.duplicate(true)
	Brain.prepare_navigation(str(config.map_id))
	# Two insects provide targets for the human. One human provides the enemy
	# for the insect POV. Social rooms never pass through this roster builder.
	var roster := {1:{"name":player_name,"role":selected_role,"ready":true,"cosmetics":local_cosmetics}}
	if selected_role == "human":
		roster[101] = {"name":"BOT · Zeta","role":"mosquito","ready":true,"cosmetics":{"mosquito":{"color":4,"accessory":1}}}
		roster[102] = {"name":"BOT · Mora","role":"mosquito","ready":true,"cosmetics":{"mosquito":{"color":1,"accessory":2}}}
	else:
		roster[101] = {"name":"BOT · Luna","role":"human","ready":true,"cosmetics":{"human":{"color":2,"accessory":1}}}
	for id: int in roster:
		if id == 1:
			continue
		var brain := Brain.new()
		brain.setup(id)
		brains[id] = brain
		input_sequences[id] = 0
		action_sequences[id] = 0
	sim = Simulation.new()
	sim.start(roster,config)
	active = sim.phase=="playing"
	_publish()

func restart() -> void:
	start(selected_role,selected_mode,local_cosmetics,player_name,selected_config.duplicate(true))

func stop() -> void:
	active = false
	sim = null
	brains.clear()
	input_sequences.clear()
	action_sequences.clear()
	bot_schedule.clear()
	publication_age = 0.0

func send_input(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
	if active and sim != null:
		sim.submit_input(1,seq,move,yaw,pitch,interact,sprint,crouch,jump)

func send_action(seq: int, verb: String, aim_yaw: float = NAN, aim_pitch: float = NAN) -> void:
	if active and sim != null:
		sim.action(1,seq,verb,aim_yaw,aim_pitch)

func send_emote(seq: int, emote_id: String) -> void:
	if active and sim!=null: sim.submit_emote(1,seq,emote_id)

func send_view_ack(seq: int, revision: int, first_input_seq: int, yaw: float, pitch: float) -> void:
	if active and sim!=null: sim.submit_view_ack(1,seq,revision,first_input_seq,yaw,pitch)

func advance(dt: float) -> void:
	if not active or sim == null or sim.phase != "playing":
		return
	var observed: Dictionary = {}
	for id: int in brains:
		if not bot_schedule.has(id) or bot_schedule[id].brain != brains[id]:
			bot_schedule[id] = {"brain":brains[id],"age":0.0,"wait":float(posmod(id,3))/60.0,"intent":{}}
		var schedule: Dictionary = bot_schedule[id]
		schedule.age += dt
		schedule.wait -= dt
		var decided: bool = float(schedule.wait) <= 0.000001
		if decided:
			if observed.is_empty():
				observed = sim.public_snapshot()
			schedule.intent = brains[id].decide(observed,sim.private_for(id),float(schedule.age))
			schedule.age = 0.0
			# Retain the phase after a long tick, without a burst of stale decisions.
			schedule.wait = BOT_DECISION_SECONDS+fmod(float(schedule.wait),BOT_DECISION_SECONDS)
		var intent: Dictionary = schedule.intent
		if intent.is_empty():
			continue
		input_sequences[id] += 1
		sim.submit_input(id,input_sequences[id],intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
		if decided and not str(intent.action).is_empty():
			action_sequences[id] += 1
			sim.action(id,action_sequences[id],str(intent.action),intent.yaw,intent.pitch)
	sim.step(dt)
	publication_age += dt
	if publication_age >= 1.0/30.0 or sim.phase == "results":
		publication_age = 0.0
		_publish()

func _physics_process(dt: float) -> void:
	advance(dt)

func _publish() -> void:
	if sim == null:
		return
	var data: Dictionary = sim.public_snapshot()
	data["practice"] = true
	data["bot_ids"] = brains.keys()
	snapshot_updated.emit(data)
	private_updated.emit(sim.private_for(1))
