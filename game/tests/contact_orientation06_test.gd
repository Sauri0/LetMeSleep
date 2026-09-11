extends SceneTree

## Public contact orientation must describe a visible attachment, never a free
## reservation. Fixtures use real held concentration and public action dispatch.
const Sim = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const PrivacyAudit = preload("res://tests/network_privacy_audit.gd")
const Codec = preload("res://scripts/online_packet_codec.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_free_reservations()
	_test_attached_pose()
	_test_perching()
	_test_stun_and_elimination()
	print("CONTACT_ORIENTATION06_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + message)

func make_sim(humans: int = 1, mosquitoes: int = 1, mode: String = "blood") -> RefCounted:
	var sim = Sim.new()
	var roster: Dictionary = {}
	for id: int in range(1, humans + mosquitoes + 1):
		roster[id] = {"role": "human" if id <= humans else "mosquito"}
	sim.start(roster, {"human_count": humans, "mode": mode, "blood_goal": 1000.0, "rotation_seconds": 40.0, "round_seconds": 180.0})
	return sim

func _audit(sim: RefCounted, message: String) -> void:
	var snapshot: Dictionary = sim.public_snapshot()
	snapshot.tick = int(sim._frame)
	var encoded: Dictionary = Codec.encode(snapshot, Codec.Kind.PUBLIC, 1, 1, snapshot.tick)
	check(encoded.ok, message + " contact snapshot fits online codec")
	if encoded.ok:
		var codec := Codec.new()
		codec.reset(1)
		var received: Dictionary = {}
		for frame: PackedByteArray in encoded.frames:
			received = codec.ingest(1, frame, 0)
		check(received.get("complete", false) and received.get("payload", {}) == snapshot, message + " contact survives actual codec round trip")
	for actor_id: int in snapshot.actors:
		var shown: Dictionary = snapshot.actors[actor_id]
		var expected := 0
		if shown.role == "mosquito" and shown.alive and shown.state == "biting":
			expected = int(sim.actors[actor_id]._assignment.human)
		check(int(shown.attached_to) == expected, message + " only actual contact exposes human ID")
	for id: int in [1, sim.actors.size()]:
		var audit = PrivacyAudit.new()
		var personal: Dictionary = sim.private_for(id)
		personal.tick = snapshot.tick
		audit.record_private(personal)
		audit.record_public(snapshot, id)
		check(audit.ok() and audit.pending_count() == 0 and audit.matched_count == 1, message + " privacy recipient%d: %s" % [id, str(audit.failures)])

func _normal(sim: RefCounted, id: int) -> Vector3:
	return sim.public_snapshot().actors[id].surface_normal

func _test_free_reservations() -> void:
	var sim = make_sim(1, 12)
	var assigned := 0
	var waiting := 0
	for id: int in range(2, 14):
		if Dictionary(sim.private_for(id).assignment).is_empty():
			waiting += 1
		else:
			assigned += 1
		# A historical wall normal must not escape through a new flying state.
		sim.actors[id]._surface_normal = Vector3.RIGHT
		check(_normal(sim, id) == Vector3.ZERO, "free or waiting insect%d exposes ZERO" % id)
	check(assigned == 8 and waiting == 4, "fixture covers eight reserved and four privately waiting insects")
	for id: int in range(2, 14):
		sim._assign(id)
		check(_normal(sim, id) == Vector3.ZERO, "reservation rotation cannot reveal a future normal%d" % id)
	sim.actors[1].body_yaw = 1.3
	sim.actors[1].crouch_amount = 1.0
	for id: int in range(2, 14):
		check(_normal(sim, id) == Vector3.ZERO, "hidden target pose cannot affect free normal%d" % id)
	check(_normal(sim, 1) == Vector3.ZERO, "human has no insect contact normal")
	_audit(sim, "free reservations")

func _aim_held(sim: RefCounted, id: int) -> void:
	var point: Vector3 = sim.private_for(id).assignment.p
	var direction: Vector3 = (point - Vector3(sim.actors[id].p)).normalized()
	sim.submit_input(id, int(sim.actors[id]._input_seq) + 1, Vector3.ZERO, atan2(-direction.x, -direction.z), asin(clampf(direction.y, -1.0, 1.0)), true)

func _attach_real(sim: RefCounted, id: int, zone: int) -> void:
	# Assignment injection isolates each authored surface; attachment itself must
	# pass authority range, facing, LOS, body occlusion and 1.2s held charge.
	sim.actors[id]._assignment = {"human": 1, "zone": zone, "revision": 1}
	var posed: Dictionary = Pose.zone_pose(sim.actors[1], Sim.BODY_ZONES[zone])
	sim.actors[id].p = Vector3(posed.p) + Vector3(posed.normal) * 0.5
	sim.actors[id].velocity = Vector3.ZERO
	for tick: int in range(80):
		_aim_held(sim, id)
		sim.step(0.025)
		if sim.actors[id].state == "biting":
			break
	check(sim.actors[id].state == "biting", "real concentration attaches zone%d" % zone)

func _test_attached_pose() -> void:
	for zone: int in range(Sim.BODY_ZONES.size()):
		var humans: int = 2 if bool(Sim.BODY_ZONES[zone].rear) else 1
		var sim = make_sim(humans)
		var id: int = humans + 1
		_attach_real(sim, id, zone)
		var human: Dictionary = sim.actors[1]
		var initial: Vector3 = _normal(sim, id)
		check(initial.is_normalized(), "biting orientation is unit length zone%d" % zone)
		check(int(sim.public_snapshot().actors[id].attached_to) == 1, "active bite identifies its real human zone%d" % zone)
		# Sample latest replicated poses without advancing the independent mark
		# schedule: the serializer must not cache the attachment-time normal.
		for pose_case: Dictionary in [
			{"body_yaw": 0.8, "yaw": 1.2, "crouch_amount": 0.0, "grounded": true, "motion_speed": 0.0},
			{"body_yaw": -0.6, "yaw": 0.1, "crouch_amount": 1.0, "grounded": true, "motion_speed": 0.0},
			{"body_yaw": 1.4, "yaw": 1.4, "crouch_amount": 0.0, "grounded": false, "motion_speed": 3.1},
			{"body_yaw": -1.2, "yaw": -1.2, "crouch_amount": 0.0, "grounded": true, "motion_speed": 5.0, "sprinting": true, "motion_phase": 1.5 * PI},
		]:
			human.merge(pose_case, true)
			sim._update_attached()
			var posed: Dictionary = Pose.zone_pose(human, Sim.BODY_ZONES[zone])
			check(_normal(sim, id).distance_to(posed.normal) < 0.00001, "orientation follows current body pose zone%d" % zone)
			check(Vector3(sim.actors[id].p).distance_to(Vector3(posed.p) + Vector3(posed.normal) * Sim.ATTACH_OFFSET) < 0.00001, "visible normal and attached position use same pose zone%d" % zone)
		check(initial.distance_to(_normal(sim, id)) > 0.1, "turned body updates normal instead of retaining contact-time value zone%d" % zone)
		_audit(sim, "biting zone%d" % zone)
		var next_rotation: float = sim.actors[id]._next_rotation
		sim.action(id, 1, "bite")
		sim.step(0.025)
		check(sim.actors[id].state == "flying" and _normal(sim, id) == Vector3.ZERO, "explicit detach immediately clears public normal zone%d" % zone)
		check(int(sim.public_snapshot().actors[id].attached_to) == 0, "detach hides the newly reserved human zone%d" % zone)
		check(sim.actors[id]._next_rotation == next_rotation, "orientation update does not reset private calendar zone%d" % zone)
		_audit(sim, "detached zone%d" % zone)

func _test_perching() -> void:
	var cases: Array[Dictionary] = [
		{"label": "floor", "p": Vector3(-7, 0.18, 8), "normal": Vector3.UP},
		{"label": "ceiling", "p": Vector3(-7, 6.22, 8), "normal": Vector3.DOWN},
		{"label": "west wall", "p": Vector3(-13.55, 1.8, 8), "normal": Vector3.RIGHT},
		{"label": "east wall", "p": Vector3(13.55, 1.8, 8), "normal": Vector3.LEFT},
		{"label": "north wall", "p": Vector3(0, 1.8, -10.55), "normal": Vector3.BACK},
		{"label": "south wall", "p": Vector3(0, 1.8, 10.55), "normal": Vector3.FORWARD},
	]
	for structure: Dictionary in Maps.get_map("house").structures:
		if str(structure.get("style", "")) == "table":
			var box: AABB = structure.box
			cases.append({"label": "authored table", "p": Vector3(box.get_center().x, box.end.y + 0.18, box.get_center().z), "normal": Vector3.UP})
			break
	check(cases.size() == 7, "table fixture comes from an actual catalog surface")
	for fixture: Dictionary in cases:
		var sim = make_sim()
		var start: Vector3 = fixture.p
		sim.actors[2].p = start
		sim.actors[2].velocity = Vector3.ZERO
		sim.action(2, 1, "perch")
		sim.step(0.4) # Acquisition travels to the support instead of snapping.
		check(sim.actors[2].state == "perched", fixture.label + " accepts actual perch action")
		var normal: Vector3 = _normal(sim, 2)
		check(normal.distance_to(fixture.normal) < 0.00001, fixture.label + " normal faces open space")
		var contact: Vector3 = sim.actors[2].p
		check((start - contact).dot(normal) > 0.0, fixture.label + " approach is on the outward side")
		check(ArenaData.clear_segment(contact, contact + normal * 0.1), fixture.label + " outward normal leads into clear space")
		check(not Dictionary(sim.private_for(2).assignment).is_empty(), fixture.label + " keeps its reservation private while perched")
		_audit(sim, fixture.label)
		sim.action(2, 2, "perch")
		sim.step(0.025)
		check(sim.actors[2].state == "flying" and _normal(sim, 2) == Vector3.ZERO, fixture.label + " takeoff clears cached surface normal")
		# Re-perch and leave using a fresh Space edge, not tangent walking.
		sim.action(2, 3, "perch")
		sim.step(0.4)
		sim.submit_input(2, 1, Vector3.UP, 0.0, 0.0, false)
		sim.step(0.025)
		check(sim.actors[2].state == "flying" and _normal(sim, 2) == Vector3.ZERO, fixture.label + " movement takeoff also clears normal")

func _test_stun_and_elimination() -> void:
	for mode: String in ["blood", "sleep", "survival"]:
		var sim = make_sim(1, 1, mode)
		_attach_real(sim, 2, 0)
		check(_normal(sim, 2) != Vector3.ZERO, mode + " begins with visible physical contact")
		var aim: Vector2 = Pose.aim_angles(sim.actors[1], sim.actors[2].p)
		sim.action(1, 1, "attack", aim.x, aim.y)
		sim.step(0.3)
		check(bool(sim.private_for(1).attack.hit), mode + " fixture lands a real manual hit")
		check(sim.actors[2].state == ("dead" if mode == "survival" else "stunned"), mode + " enters correct post-hit state")
		check(_normal(sim, 2) == Vector3.ZERO, mode + " hit removes prior bite orientation")
		check(Dictionary(sim.private_for(2).assignment).is_empty(), mode + " no active mark survives hit")
		_audit(sim, mode + " after hit")
		if mode != "survival":
			sim.step(1.0)
			check(_normal(sim, 2) == Vector3.ZERO, mode + " falling does not restore old orientation")
			sim._recover_stun(2)
			check(sim.actors[2].state == "flying" and _normal(sim, 2) == Vector3.ZERO, mode + " recovered reservation remains private")
	var wall = make_sim()
	wall.actors[2].p = Vector3(-13.55, 1.8, 8)
	wall.action(2, 1, "perch")
	wall.step(0.4)
	check(_normal(wall, 2) == Vector3.RIGHT, "stun fixture stores a wall contact")
	wall._kill(2)
	check(_normal(wall, 2) == Vector3.ZERO, "stun suppresses a historical perched normal too")
	_audit(wall, "wall stun")
