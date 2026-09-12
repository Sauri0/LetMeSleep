extends SceneTree
const Session=preload("res://scripts/online_session.gd")
const Network=preload("res://scripts/network.gd")
const Invitation=preload("res://scripts/online_invitation.gd")
const Fakes=preload("res://tests/online_session_test.gd")
var checks:=0
var failures:Array[String]=[]
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);printerr("ONLINE093_FAIL "+label)
func _initialize()->void:run.call_deferred()
func run()->void:
	var backend=Fakes.FakeBackend.new();root.add_child(backend)
	var session=Session.new();session.set_backend(backend);root.add_child(session)
	var errors:Array[String]=[]
	session.failed.connect(func(code:String)->void:errors.append(code))
	var invite=Invitation.decode(Invitation.encode("Lobby123",Fakes.CAP))
	check(session.start_join(Fakes.Config,"Owner",invite),"own invite reaches authenticated lookup")
	backend.finish({"ok":true});backend.finish({"ok":true})
	backend.finish({"ok":true,"local_user_id":"host"})
	var own=Fakes.FakeLobby.new();backend.finish({"ok":true,"lobby":own})
	check(errors==["own_room"] and session.state=="error","own room is not reported as a version mismatch")
	check(backend.disposed==[own] and not backend.operations.has("join"),"self join releases search without acquiring a lobby")
	check(session.start_join(Fakes.Config,"Guest",invite),"failed self join can retry")
	backend.finish({"ok":true});backend.finish({"ok":true})
	backend.finish({"ok":true,"local_user_id":"guest"})
	var other=Fakes.FakeLobby.new();backend.finish({"ok":true,"lobby":other})
	check(backend.pending.operation=="join","different player proceeds to the real join operation")
	backend.finish({"ok":true,"lobby":other})
	check(session.state=="transport_ready","peer opens only after lobby acceptance")
	session.close()
	if backend.busy:backend.finish({"ok":true})
	session.free();backend.free()
	var network=Network.new();root.add_child(network);network.set_physics_process(false)
	network._online_request={"host":false,"name":"Guest","invite":"test"}
	network._last_request={"address":"EOS","port":0,"name":"Guest"}
	for code:String in ["room_not_found","own_room","online_timeout_login","online_timeout_join","transport_timeout","configuration_required","room_owner_changed"]:
		network._online=true;network.invitation_text="stale code";network.connecting=true
		network._online_failed(code)
		check(network.connection_state.phase=="failed" and network.connection_state.code==code,"failed attempt retains diagnostic category "+code)
		check(network.invitation_text.is_empty() and not network.connecting,"failed attempt clears invitation and busy state "+code)
		var message:String=network.connection_state.message
		check(not message.is_empty() and not message.contains("UDP") and not message.contains("127.0.0.1"),"online error explains next action without local networking "+code)
		check(network.connection_state.can_retry,"failure can be retried "+code)
	network.queue_free();await process_frame
	print("ONLINE093_CONNECTION checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
