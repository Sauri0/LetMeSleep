extends SceneTree

var checks := 0
var failures := 0
var lobbies := 0
var snapshots := 0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("NETWORK_ORDER FAIL " + label)

func _run() -> void:
	var net: Node = load("res://scripts/network.gd").new()
	root.add_child(net)
	net.lobby_updated.connect(func(_data: Dictionary) -> void: lobbies += 1)
	net.snapshot_updated.connect(func(_data: Dictionary) -> void: snapshots += 1)
	net._receive_lobby({"barrier_tick":10})
	net._accept_snapshot({"tick":12,"phase":"playing"})
	net._receive_private({"tick":12,"assignment":{"zone":"forehead"}})
	net._receive_lobby({"barrier_tick":11})
	check(lobbies == 1, "delayed pre-round lobby cannot cancel a newer round")
	check(net.latest.get("phase") == "playing", "round state retained")
	check(net.last_received_tick == 12, "clock never rolls back")
	check(not net.private_latest.is_empty(), "current private assignment retained")
	net._accept_snapshot({"tick":30,"phase":"results"})
	net._receive_lobby({"barrier_tick":11})
	check(lobbies == 1 and net.latest.get("phase") == "results", "old lobby cannot hide results")
	net._receive_lobby({"barrier_tick":30})
	check(lobbies == 2 and net.latest.is_empty(), "actual rematch lobby accepted at barrier")
	check(net.private_latest.is_empty(), "return clears private state")
	net._accept_snapshot({"tick":30,"phase":"results"})
	net._receive_private({"tick":30,"assignment":{"zone":"forehead"}})
	check(snapshots == 2 and net.private_latest.is_empty(), "late previous-round packets rejected")
	net._accept_snapshot({"tick":31,"phase":"waiting"})
	check(net.latest.get("phase") == "waiting", "new lobby movement accepted")
	net._accept_snapshot({"tick":32,"phase":"playing"})
	check(snapshots == 3, "next round accepted")
	net._receive_lobby({"barrier_tick":33})
	check(lobbies == 3, "disconnect return with later barrier accepted")
	net.queue_free()
	await process_frame
	print("NETWORK_ORDER_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
