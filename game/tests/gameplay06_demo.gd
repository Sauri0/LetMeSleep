extends SceneTree
## Native recording fixture, intended for --fixed-fps 30 --write-movie PATH.
## Three explicitly prepared cuts use the actual Main/Client/Practice authority.
## No preferences are changed, and no audio or gameplay state is mocked.

const Prefs = preload("res://scripts/preferences.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Sim = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
var app: Node
var client: Node
var caption: Label
var overlay: CanvasLayer
var report_path := ""
var checks := 0
var failures := 0
var observations: Array[Dictionary] = []
var metrics: Dictionary = {}
var elapsed := 0.0
var finishing := false
var peak_focus := 0.0
var focus_audio_seen := false
var help_audio_seen := false
var menu_audio_seen := false
var game_audio_seen := false
var input_missing_frames := 0
var watching_hold := false

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--demo-report="):
			report_path = argument.trim_prefix("--demo-report=")
	_run.call_deferred()

func _check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	observations.append({"ok": ok, "label": message, "movie_seconds": snappedf(elapsed, 0.001)})
	print("GAMEPLAY06_DEMO %s %s" % ["PASS" if ok else "FAIL", message])

func _title(text: String) -> void:
	caption.text = "Prueba de controles · rival quieto\n" + text

func _key(action: String, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = pressed
	# Global held state drives Client._physics_process. Direct unhandled delivery
	# is used exactly once, with automatic delivery disabled on this Client only.
	Input.parse_input_event(event)
	client._unhandled_input(event)

func _look(yaw: float, pitch: float) -> void:
	var sensitivity: float = Prefs.human_sensitivity if client.role == "human" else Prefs.mosquito_sensitivity
	var motion := InputEventMouseMotion.new()
	motion.relative = Vector2(-wrapf(yaw - client.yaw, -PI, PI) / sensitivity, -(pitch - client.pitch) / sensitivity * (-1.0 if Prefs.invert_y else 1.0))
	client._unhandled_input(motion)

func _aim(point: Vector3) -> void:
	if client.role == "human":
		var angles: Vector2 = Pose.aim_angles(client.practice.sim.actors[1], point)
		_look(angles.x, angles.y)
	else:
		var direction: Vector3 = (point - Vector3(client.practice.sim.actors[1].p)).normalized()
		_look(atan2(-direction.x, -direction.z), asin(clampf(direction.y, -1.0, 1.0)))

func _aim_mark() -> void:
	var assignment: Dictionary = client.practice.sim.private_for(1).assignment
	if not assignment.is_empty():
		_aim(assignment.p)

func _aim_insect() -> void:
	_aim(client.practice.sim.actors[101].p)

func _aim_ally() -> void:
	_aim(client.practice.sim.actors[102].p)

func _sample() -> void:
	menu_audio_seen = menu_audio_seen or (client.music.menu_player.playing and client.music.menu_player.volume_db > -60.0)
	game_audio_seen = game_audio_seen or (client.music.game_player.playing and client.music.game_player.volume_db > -60.0)
	if not client.practice_active:
		return
	var personal: Dictionary = client.practice.sim.private_for(1)
	peak_focus = maxf(peak_focus, float(personal.get("focus", {}).get("progress", 0.0)))
	focus_audio_seen = focus_audio_seen or client.world.audio_fx.focus_player.playing
	help_audio_seen = help_audio_seen or client.world.audio_fx.help_player.playing
	if watching_hold and not Input.is_action_pressed("bite"):
		input_missing_frames += 1

func _wait(seconds: float, follow: Callable = Callable()) -> void:
	var spent := 0.0
	while spent < seconds and not finishing:
		if follow.is_valid():
			follow.call()
		await process_frame
		# MovieWriter's fixed delta determines screen time. Cap a single loading
		# stall so it cannot skip an entire control demonstration in a live run.
		var delta: float = clampf(client.get_process_delta_time(), 0.0001, 0.1)
		spent += delta
		elapsed += delta
		_sample()

func _start(role: String, via_menu: bool = false) -> void:
	_key("bite", false)
	_key("move_forward", false)
	if via_menu:
		client.ui._open_practice()
		client.ui._practice_role_buttons[role].pressed.emit()
		client.ui._practice_mode_buttons["blood"].pressed.emit()
		client.ui._practice_start_button.pressed.emit()
	else:
		client._start_practice(role, "blood")
	client.practice.brains.clear()
	# These are staged control cuts, not footage of an autonomous encounter.
	client.ui._practice_banner.text = "PRÁCTICA · ESCENA PREPARADA"
	_check(client.practice_active and client.playing and client.role == role, "real practice starts " + role + " POV")

func _run() -> void:
	root.size = Vector2i(1280, 720)
	app = load("res://scripts/main.gd").new()
	root.add_child(app)
	client = app.get_node("Client")
	client.set_process_unhandled_input(false)
	overlay = CanvasLayer.new()
	overlay.layer = 100
	root.add_child(overlay)
	caption = Label.new()
	caption.position = Vector2(300, 16)
	caption.size = Vector2(680, 60)
	caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	caption.mouse_filter = Control.MOUSE_FILTER_IGNORE
	caption.add_theme_font_size_override("font_size", 18)
	caption.add_theme_color_override("font_color", Color("fff1ca"))
	caption.add_theme_color_override("font_outline_color", Color("17121d"))
	caption.add_theme_constant_override("outline_size", 5)
	overlay.add_child(caption)
	create_timer(45.0).timeout.connect(func() -> void:
		if not finishing:
			_check(false, "watchdog: demonstration did not finish within45seconds")
			_write_report()
			quit(1))
	_title("Let me sleep · sonido y controles")
	metrics.fixture_setups = ["quiet opponents and prepared positions", "human cut starts with an insect already attached", "rescue cut starts with a stunned ally; timer is not shortened"]
	metrics.audio_volumes = {"master": Prefs.master_volume, "music": Prefs.music_volume, "effects": Prefs.effects_volume, "ambience": Prefs.ambience_volume, "ui": Prefs.ui_volume}
	await _wait(1.7)
	_check(not client.practice_active and client.world.current_map == "lobby", "real menu and distinct lobby visible")
	await _flight_cut()
	await _human_cut()
	await _rescue_cut()
	_title("Carga, palmada y rescate · acciones verificadas")
	await _wait(1.0)
	_check(menu_audio_seen and game_audio_seen, "native menu and synchronized gameplay music play")
	_check(focus_audio_seen and help_audio_seen, "native concentration and rescue loops play")
	_check(input_missing_frames == 0, "held E remains pressed throughout measured actions")
	metrics.focus_peak = peak_focus
	metrics.input_missing_frames = input_missing_frames
	metrics.audio_observed = {"menu": menu_audio_seen, "gameplay": game_audio_seen, "focus": focus_audio_seen, "help": help_audio_seen}
	await _finish()

func _flight_cut() -> void:
	_title("1/3 · W sigue la mirada; soltar frena")
	_start("mosquito", true)
	var sim: RefCounted = client.practice.sim
	var me: Dictionary = sim.actors[1]
	var human: Dictionary = sim.actors[101]
	human.p = Vector3(-7, 0, 8)
	me._assignment = {"human": 101, "zone": 4, "revision": 1}
	me.p = human.p + Vector3(0, 1.0, -1.8)
	me.velocity = Vector3.ZERO
	client.practice._publish()
	await _wait(0.35)
	_look(PI, 0.35)
	await _wait(0.30)
	var start: Vector3 = me.p
	_key("move_forward", true)
	await _wait(0.35)
	_key("move_forward", false)
	await _wait(0.35)
	_check(float(me.p.y) > start.y + 0.1, "W climbs along camera aim without ascend input")
	_check(Vector3(me.velocity).length() < 0.1, "releasing W brakes")
	_title("1/3 · Mantener E: concentración y picadura")
	await _wait(0.25, _aim_mark)
	_key("bite", true)
	watching_hold = true
	await _wait(0.55, _aim_mark)
	var focus: Dictionary = sim.private_for(1).focus
	_check(float(focus.progress) > 0.15 and float(focus.progress) < 1.0, "real held E produces observable partial charge")
	metrics.partial_focus = focus.duplicate(true)
	await _wait(1.30, _aim_mark)
	_check(me.state == "biting", "authority attaches only after held concentration")
	_key("bite", false)
	watching_hold = false
	await _wait(1.35)
	_check(me.state == "biting" and float(sim.blood) > 0.0, "release preserves actual bite and blood extracts after preparation")
	metrics.blood_after_bite = sim.blood
	_title("1/3 · Nueva pulsación de E: desprenderse")
	_key("bite", true)
	await _wait(0.10)
	_key("bite", false)
	await _wait(0.35)
	_check(me.state == "flying", "new E press explicitly detaches")

func _human_cut() -> void:
	_title("2/3 · Corte preparado: mosquito en el antebrazo")
	_start("human")
	var sim: RefCounted = client.practice.sim
	var human: Dictionary = sim.actors[1]
	var insect: Dictionary = sim.actors[101]
	human.p = Vector3(-7, 0, 8)
	insect._assignment = {"human": 1, "zone": 4, "revision": 1}
	insect.state = "biting"
	insect._bite_started = sim.elapsed
	sim._update_attached()
	client.practice._publish()
	await _wait(0.90, _aim_insect)
	_title("2/3 · Mirar el cuerpo, apuntar y pulsar clic izquierdo")
	await _wait(0.60, _aim_insect)
	_check(client.camera.current and client.pitch < -0.7, "real first-person camera looks down at body")
	var clicked_at: float = sim.elapsed
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	client._unhandled_input(click)
	click.pressed = false
	client._unhandled_input(click)
	await _wait(0.40)
	_check(bool(sim.private_for(1).attack.hit), "LMB manual ray and physical palm contact register hit")
	_check(insect.state == "stunned" and bool(insect.alive), "confirmed hit stuns rather than kills in Sangre")
	metrics.manual_hit_at = clicked_at
	metrics.stun_after_hit = sim.private_for(101).stun.duplicate(true)
	_title("2/3 · Aturdido: cae y puede recibir ayuda")
	await _wait(1.0)
	_check(bool(insect.grounded), "actual stunned body lands on floor")
	metrics.impact_cues = client.world.audio_fx.effects_started.duplicate(true)

func _rescue_cut() -> void:
	_title("3/3 · Corte preparado: compañero aturdido, 35 s")
	_start("mosquito")
	var sim: RefCounted = client.practice.sim
	# Enrich the actual practice roster with one ally. Practice still submits
	# local ID1 input, steps this simulation and publishes it through Client.
	sim.start({
		1: {"name": "Vos", "role": "mosquito", "cosmetics": Prefs.cosmetics},
		101: {"name": "Rival quieto", "role": "human", "cosmetics": {"human": {"color": 2, "accessory": 1}}},
		102: {"name": "Compañero", "role": "mosquito", "cosmetics": {"mosquito": {"color": 4, "accessory": 1}}},
	}, {"mode": "blood", "round_seconds": 120.0, "rotation_seconds": 40.0})
	var me: Dictionary = sim.actors[1]
	var ally: Dictionary = sim.actors[102]
	sim.actors[101].p = Vector3(-4, 0, 8)
	ally.p = Vector3(-7, ArenaData.MOSQUITO_RADIUS, 8)
	me.p = Vector3(-7, 0.30, 7.50)
	me.velocity = Vector3.ZERO
	sim._kill(102) # Explicit staged initial condition; keep its full35second timer.
	client.practice._publish()
	_check(is_equal_approx(float(sim.private_for(102).stun.remaining), Sim.STUN_SECONDS), "rescue fixture begins at full35seconds, no shortened timer")
	await _wait(0.55, _aim_ally)
	_check(sim.private_for(1).help.state == "ready", "real range, aim and LOS admit nearby ally help")
	_title("3/3 · Mantener E para ayudar: recuperación ×4")
	var start: float = sim.elapsed
	var remaining: float = sim.private_for(102).stun.remaining
	_key("bite", true)
	watching_hold = true
	await _wait(1.0, _aim_ally)
	var rate: float = (remaining - float(sim.private_for(102).stun.remaining)) / (float(sim.elapsed) - start)
	_check(me.help_target == 102 and sim.private_for(1).help.state == "helping", "E selects actual ally help ahead of own bite concentration")
	_check(absf(rate - Sim.HELP_RATE) < 0.15, "measured rescue timer advances at four times normal rate")
	metrics.help_rate = rate
	while ally.state == "stunned" and float(sim.elapsed) - start < 9.5:
		await _wait(0.05, _aim_ally)
	var duration: float = float(sim.elapsed) - start
	_key("bite", false)
	watching_hold = false
	_check(ally.state == "flying" and bool(ally.alive), "sustained real E recovers ally in place")
	_check(absf(duration - remaining / Sim.HELP_RATE) < 0.20, "recovery duration matches authority remaining divided by4")
	_check(Vector3(ally.p).distance_to(Vector3(-7, ArenaData.MOSQUITO_RADIUS, 8)) < 0.02, "recovery never teleports ally")
	metrics.help_seconds = duration
	metrics.help_initial_remaining = remaining
	_title("3/3 · Compañero recuperado")
	await _wait(1.2, _aim_ally)
	_check(int(me.help_target) == 0 and sim.private_for(1).help.state == "idle", "completed rescue stops helping without an automatic bite")
	metrics.recovery_cues = client.world.audio_fx.effects_started.duplicate(true)

func _write_report() -> void:
	var report := {"fixture": "gameplay06_demo", "checks": checks, "failures": failures, "screen_seconds": elapsed, "observations": observations, "metrics": metrics, "staged_control_demo": true, "preferences_modified": false}
	if not report_path.is_empty():
		var folder: String = report_path.get_base_dir()
		if not folder.is_empty():
			DirAccess.make_dir_recursive_absolute(folder)
		var file := FileAccess.open(report_path, FileAccess.WRITE)
		if file != null:
			file.store_string(JSON.stringify(report, "\t"))
		else:
			failures += 1
			printerr("GAMEPLAY06_DEMO could not write report: " + report_path)
	print("GAMEPLAY06_DEMO_RESULT checks=%d failures=%d seconds=%.3f" % [checks, failures, elapsed])

func _finish() -> void:
	finishing = true
	for action: String in ["bite", "move_forward", "move_back", "sprint", "crouch", "jump"]:
		_key(action, false)
	_write_report()
	client._leave()
	await process_frame
	app.queue_free()
	overlay.queue_free()
	await create_timer(0.2).timeout
	quit(0 if failures == 0 else 1)
