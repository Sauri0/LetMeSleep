extends SceneTree
## Real ENet transports and unmodified production RPC handlers. Manual polling
## delays one client's delivery, never substitutes an ACK or starts a simulation.
## Catalog eviction before each production receiver forces local regeneration
## despite all isolated MultiplayerAPIs sharing this test process.
const Net = preload("res://scripts/network.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Inv = preload("res://scripts/invitation.gd")
const Emotes = preload("res://scripts/emote_catalog.gd")
var surfaces: Array[Node] = []
var peers: Array[Node] = []
var clients: Array[Node] = []
var authority: Node
var other_authority: Node
var owner: Node
var guest: Node
var other: Node
var stranger: Node
var checks := 0
var failures: Array[String] = []
var results: Array[Dictionary] = []
var offers: Dictionary = {}
var notices: Dictionary = {}
var packets: Dictionary = {}
var input_sequence := 0
var inject_bad_hash := false
var inject_stale_ack := false
var stale_ack: Dictionary = {}
var bad_hash_sent := false
var stale_ack_sent := false
var regenerate_count := 0
var round_maps: Array[Dictionary] = []
var diagnostics: Array[Dictionary] = []
var finished := false
var output := ""

func _initialize() -> void:
	multiplayer_poll = false
	_run.call_deferred()

func check(value: bool, label: String) -> bool:
	checks += 1
	results.append({"ok":value,"label":label})
	if not value: failures.append(label)
	print("NET_MAP_SOCIAL09 %s %s" % ["PASS" if value else "FAIL",label])
	return value

func peer(label: String) -> Node:
	var surface := Node.new()
	surface.name = label
	root.add_child(surface)
	set_multiplayer(SceneMultiplayer.new(), surface.get_path())
	var node := Net.new()
	node.name = "Network"
	surface.add_child(node)
	surfaces.append(surface)
	peers.append(node)
	offers[node] = []
	notices[node] = []
	packets[node] = []
	node.lobby_updated.connect(func(data: Dictionary) -> void: _observe_lobby(node,data))
	node.notice.connect(func(message: String) -> void: notices[node].append(message))
	node.snapshot_updated.connect(func(data: Dictionary) -> void: packets[node].append(data.duplicate(true)))
	return node

func _observe_lobby(node: Node, data: Dictionary) -> void:
	if not data.get("map_prepare") is Dictionary: return
	var prepare: Dictionary = data.map_prepare.duplicate(true)
	offers[node].append(prepare)
	# This signal runs immediately before Network's real get_map()/fingerprint
	# verification. Evict only this generated key; no geometry is modified.
	Maps._generated.erase(str(prepare.id))
	regenerate_count += 1
	if node == owner and inject_bad_hash:
		inject_bad_hash = false
		bad_hash_sent = true
		node._request_lobby.rpc_id(1,"map_ready",{"id":prepare.id,"fingerprint":"incorrect-fixture-hash"})
	if node == owner and inject_stale_ack:
		inject_stale_ack = false
		stale_ack_sent = true
		# Deliberately wrong hash, but OLD id. It must not abort the NEW offer.
		node._request_lobby.rpc_id(1,"map_ready",stale_ack)

func settle(frames: int = 6, excluded: Array[Node] = []) -> void:
	for frame: int in range(frames):
		input_sequence += 1
		for node: Node in clients:
			if node._had_session:
				node.send_input(input_sequence,Vector3.ZERO,0.0,0.0,false)
		for node: Node in peers:
			if excluded.has(node): continue
			if node.multiplayer.multiplayer_peer is ENetMultiplayerPeer:
				node.multiplayer.poll()
		await physics_frame
	await process_frame

func until(test: Callable, label: String, frames: int = 240, excluded: Array[Node] = []) -> bool:
	for frame: int in range(frames):
		if test.call(): return check(true,label)
		await settle(1,excluded)
	return check(false,label)

func _ready_room() -> void:
	owner.lobby_action("ready",true)
	guest.lobby_action("ready",true)
	await until(func() -> bool: return authority._start_reason().is_empty(),"both clients ready through lobby RPC")

func _run() -> void:
	output = ProjectSettings.globalize_path("res://../outputs/0.9-network-map-social")
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output = argument.trim_prefix("--output=")
	DirAccess.make_dir_recursive_absolute(output)
	create_timer(75).timeout.connect(func() -> void:
		if not finished:
			check(false,"watchdog completed within 75 seconds")
			_finish.call_deferred())
	authority = peer("AuthorityA")
	other_authority = peer("AuthorityB")
	var port := 42000 + int(Time.get_ticks_msec() % 7000)
	if not check(authority.host(port) == OK and other_authority.host(port+1) == OK,"two independent ENet authorities bind UDP"):
		await _finish(); return
	check(Net.PROTOCOL == Inv.PROTOCOL,"production protocol derived from Invitation")
	owner = peer("OwnerA")
	guest = peer("GuestA")
	other = peer("OwnerB")
	clients.assign([owner,guest,other])
	owner.connect_room("127.0.0.1",port,"Social owner","",true)
	other.connect_room("127.0.0.1",port+1,"Separate room","",true)
	if not await until(func() -> bool: return authority.players.size() == 1 and other_authority.players.size() == 1 and owner._had_session and other._had_session,"owners authenticate in separate rooms"):
		await _finish(); return
	guest.connect_room("127.0.0.1",port,"Social guest",authority.room_code,false)
	if not await until(func() -> bool: return authority.players.size() == 2 and guest._had_session,"friend joins actual room code"):
		await _finish(); return
	var owner_id: int = owner.multiplayer.get_unique_id()
	var guest_id: int = guest.multiplayer.get_unique_id()
	var other_id: int = other.multiplayer.get_unique_id()
	check(authority.room_owner == owner_id and other_authority.room_owner == other_id,"ownership is transport-local")
	check(not authority.players.has(other_id) and not other_authority.players.has(owner_id) and not other_authority.players.has(guest_id),"authenticated rosters remain isolated")
	owner.lobby_action("config",{"mode":"blood","human_count":1,"round_seconds":180,"blood_goal":1000})
	await settle()
	await _ready_room()
	inject_bad_hash = true
	owner.lobby_action("start")
	if not await until(func() -> bool: return bad_hash_sent and authority._pending_map.is_empty(),"incorrect fingerprint clears preparation without starting"):
		await _finish(); return
	await settle()
	check(authority.sim == null and owner.latest.get("phase","") != "playing" and guest.latest.get("phase","") != "playing","incorrect ACK cannot produce a playable round")
	check(str(notices[owner]).contains("no coincide") and str(notices[guest]).contains("no coincide"),"both clients receive specific map mismatch notice")
	check(owner._had_session and guest._had_session,"hash failure preserves accepted lobby connections")
	var failed_offer: Dictionary = offers[owner].back()
	# Queue legitimate preparation while GuestA is deliberately not polled.
	owner.lobby_action("start")
	if not await until(func() -> bool: return not authority._pending_map.is_empty() and authority._pending_map.ready.has(owner_id),"production receiver regenerates map and acknowledges fingerprint",240,[guest]):
		await _finish(); return
	var first: Dictionary = authority._pending_map.duplicate(true)
	check(authority.sim == null and not first.ready.has(guest_id),"round is blocked until every authenticated participant acknowledges")
	check(owner._prepared_map.get("id") == first.id and owner._prepared_map.get("fingerprint") == first.fingerprint,"client stores exact verified map identity")
	check(first.id != failed_offer.id,"retry prepares a fresh offer after bad hash")
	check(first.seed == Maps.Generator.parse_seed(str(first.id)),"map id encodes the server seed")
	# An ENet-connected peer that never sent _request_join has no room authority.
	stranger = peer("Unauthenticated")
	var transport := ENetMultiplayerPeer.new()
	check(transport.create_client("127.0.0.1",port,3) == OK,"unauthenticated transport opens for adversarial RPC checks")
	stranger.multiplayer.multiplayer_peer = transport
	await until(func() -> bool: return transport.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED and authority.multiplayer.get_peers().has(stranger.multiplayer.get_unique_id()),"raw ENet peer connects without room authentication",120,[guest])
	var stranger_id: int = stranger.multiplayer.get_unique_id()
	check(not authority.players.has(stranger_id),"raw transport is not a participant")
	stranger._request_lobby.rpc_id(1,"map_ready",{"id":first.id,"fingerprint":first.fingerprint})
	other._request_lobby.rpc_id(1,"map_ready",{"id":first.id,"fingerprint":first.fingerprint})
	await settle(6,[guest])
	check(authority.sim == null and authority._pending_map.ready.size() == 1 and not authority._pending_map.ready.has(stranger_id),"unauthenticated and other-room ACKs cannot satisfy the barrier")
	check(other_authority._pending_map.is_empty() and offers[other].is_empty(),"room B receives no room A preparation")
	if not await until(func() -> bool: return authority.sim != null and owner.latest.get("phase","") == "playing" and guest.latest.get("phase","") == "playing","all production ACKs start and publish the same generated house"):
		await _finish(); return
	round_maps.append({"id":first.id,"seed":first.seed,"fingerprint":first.fingerprint})
	for node: Node in [owner,guest]:
		check(node.latest.config.map_id == first.id and node.latest.config.map_fingerprint == first.fingerprint,"remote snapshot uses prepared identity for " + str(node.get_parent().name))
		check(node._prepared_map.fingerprint == Maps.get_map(str(first.id)).fingerprint,"actual regenerated catalog matches verified fingerprint for " + str(node.get_parent().name))
	check(authority.sim.actors.size() == 2 and other_authority.sim == null,"only room A starts its own roster")
	await _social(stranger_id)
	stranger.close_client()
	# Only advancing the round clock to its final tick is staged. The ordinary
	# win/result path, owner rematch and second handshake remain production code.
	# abort() means interrupted lobby, deliberately NOT normal round results.
	authority.sim.elapsed = float(authority.sim.config.round_seconds) - .01
	await until(func() -> bool: return owner.latest.get("phase","") == "results" and guest.latest.get("phase","") == "results","both peers receive authoritative results")
	owner.lobby_action("rematch")
	if not await until(func() -> bool: return authority.sim == null and owner.latest.get("phase","") != "results" and guest.latest.get("phase","") != "results","owner returns to lobby through production rematch RPC"):
		await _finish(); return
	await _ready_room()
	stale_ack = {"id":first.id,"fingerprint":"stale-and-invalid-fingerprint"}
	inject_stale_ack = true
	owner.lobby_action("start")
	if not await until(func() -> bool: return stale_ack_sent and not authority._pending_map.is_empty() and authority._pending_map.ready.has(owner_id),"stale previous-round ACK is ignored before fresh ACK",240,[guest]):
		await _finish(); return
	var second: Dictionary = authority._pending_map.duplicate(true)
	check(authority.sim == null and second.ready.size() == 1,"stale ACK never counts for the delayed participant")
	check(second.id != first.id and second.seed != first.seed and second.fingerprint != first.fingerprint,"new round has a distinct seed, id and fingerprint")
	if not await until(func() -> bool: return authority.sim != null and owner.latest.get("config",{}).get("map_id") == second.id and guest.latest.get("config",{}).get("map_id") == second.id,"second handshake delivers new map to both peers"):
		await _finish(); return
	round_maps.append({"id":second.id,"seed":second.seed,"fingerprint":second.fingerprint})
	check(offers[other].is_empty() and packets[other].is_empty() and other_authority.players.size() == 1,"room B never receives maps, gestures or results from room A")
	check(other._had_session and other.latest.get("phase","") == "waiting","unrelated room remains connected and usable")
	check(regenerate_count >= 6,"each accepted/rejected offer forces real receiver regeneration, not shared cache success")
	await _finish()

func _social(stranger_id: int) -> void:
	var human: Node
	var insect: Node
	for node: Node in [owner,guest]:
		if authority.sim.actors[node.multiplayer.get_unique_id()].role == "human": human = node
		else: insect = node
	if not check(human != null and insect != null,"roles come from authoritative random draw"): return
	var hid: int = human.multiplayer.get_unique_id()
	var mid: int = insect.multiplayer.get_unique_id()
	var initial_sequence: int = authority.sim.actors[hid]._input_seq
	await until(func() -> bool: return int(authority.sim.actors[hid]._input_seq) > initial_sequence and authority.sim.elapsed-float(authority.sim.actors[hid]._last_input) < .1,"fresh stationary movement RPC reaches the authoritative human before emote")
	var initial_actor: Dictionary = authority.sim.actors[hid]
	diagnostics.append({"stage":"emote_precondition","grounded":initial_actor.grounded,"position":str(initial_actor.p),"velocity":str(initial_actor.velocity),"last_input":initial_actor._last_input,"elapsed":authority.sim.elapsed,"bitten":initial_actor.bitten,"threatened":initial_actor.threatened,"move":str(initial_actor._move)})
	check(not authority.sim._emote_blocked(authority.sim.actors[hid]),"actual generated spawn is grounded, idle and ready for emote")
	var root_position: Vector3 = authority.sim.actors[hid].p
	var actor_ids: Array = authority.sim.actors.keys()
	stranger._action.rpc_id(1,90,JSON.stringify({"verb":"emote","id":"wave"}))
	insect.send_emote(90,"wave")
	insect._action.rpc_id(1,91,JSON.stringify({"verb":"emote","id":"wave","player":hid}))
	other.send_emote(90,"wave")
	await settle()
	check(authority.sim.actors.keys() == actor_ids and not authority.sim.actors.has(stranger_id),"unauthenticated action cannot create or impersonate an actor")
	check(authority.sim.actors[hid].emote_id == "" and authority.sim.actors[mid].emote_id == "","mosquito and forged target JSON cannot animate a human")
	check(other_authority.sim == null,"gesture RPC cannot cross room transport")
	human.send_emote(100,"not-a-real-emote")
	human._action.rpc_id(1,101,JSON.stringify({"verb":"emote","id":"wave","duration":99}))
	await settle()
	check(authority.sim.actors[hid].emote_id == "","unknown id and client-controlled duration are rejected")
	human.send_emote(110,"wave")
	if not await until(func() -> bool: return insect.latest.get("actors",{}).get(hid,{}).get("emote_id","") == "wave","authenticated human gesture is received by remote mosquito"): return
	check(authority.sim.actors[hid].emote_id == "wave" and float(authority.sim.actors[hid].emote_time) > 0,"server advances the catalog gesture clock")
	check(authority.sim.actors[hid].p.distance_to(root_position) < .001,"gesture cannot translate the authoritative root")
	check(human.latest.actors[hid].tool == "hands" and insect.latest.actors[hid].tool == "hands","remote gesture preserves equipped tool identity")
	var before: float = authority.sim.actors[hid].emote_time
	var until_time: float = authority.sim.actors[hid]._emote_until
	human.send_emote(110,"wave")
	human.send_emote(109,"")
	await settle()
	check(authority.sim.actors[hid].emote_id == "wave" and float(authority.sim.actors[hid].emote_time) > before and is_equal_approx(float(authority.sim.actors[hid]._emote_until),until_time),"replayed start and stale cancellation cannot restart or cancel gesture")
	human.send_emote(111,"")
	await until(func() -> bool: return authority.sim.actors[hid].emote_id == "" and insect.latest.get("actors",{}).get(hid,{}).get("emote_id","missing") == "","new sequence cancels and replicates empty gesture")
	human.send_emote(110,"wave")
	human.send_emote(112,"celebrate")
	await settle()
	check(authority.sim.actors[hid].emote_id == "","replay and premature different gesture cannot bypass recovery")
	await until(func() -> bool: return authority.sim.elapsed >= float(authority.sim.actors[hid]._emote_cooldown),"catalog recovery expires with real server time",240)
	human.send_emote(113,"shrug")
	await until(func() -> bool: return insect.latest.get("actors",{}).get(hid,{}).get("emote_id","") == "shrug","subsequent valid catalog gesture replicates after recovery")
	human.send_emote(114,"")
	await settle()
	check(authority.sim.actors[hid].emote_id == "" and authority.sim.actors[hid].p.distance_to(root_position) < .001,"final cancellation leaves root stationary")
	check(not Emotes.is_valid("not-a-real-emote"),"negative payload is outside shared catalog")

func _finish() -> void:
	if finished: return
	finished = true
	for node: Node in clients:
		if is_instance_valid(node): node.close_client()
	if is_instance_valid(stranger): stranger.close_client()
	for node: Node in [authority,other_authority]:
		if is_instance_valid(node) and node.multiplayer.multiplayer_peer != null:
			node.multiplayer.multiplayer_peer.close()
	var report := {"checks":checks,"failures":failures,"protocol":Inv.PROTOCOL,"invitation_prefix":Inv.PREFIX,"transport":"real ENet, two authority contexts, loopback, one process","production_rpc_handlers":true,"forced_client_regenerations":regenerate_count,"round_maps":round_maps,"diagnostics":diagnostics,"preferences_modified":false,"microphone_used":false,"results":results}
	var file := FileAccess.open(output.path_join("checks.json"),FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(report,"\t")); file.close()
	else: printerr("Could not write network map/social evidence"); failures.append("evidence write failed")
	for surface: Node in surfaces: surface.queue_free()
	await process_frame
	print("NET_MAP_SOCIAL09_RESULT checks=%d failures=%d protocol=%d" % [checks,failures.size(),Inv.PROTOCOL])
	quit(0 if failures.is_empty() else 1)
