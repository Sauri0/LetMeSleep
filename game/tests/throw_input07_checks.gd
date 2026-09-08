extends SceneTree
const Client = preload("res://scripts/client.gd")
const Prefs = preload("res://scripts/preferences.gd")
class Menu extends CanvasLayer:
	var blocked := false
	func is_menu_open() -> bool: return blocked
class Transport extends Node:
	var actions: Array[String] = []
	func send_action(_seq: int,verb: String,_yaw: float,_pitch: float) -> void: actions.append(verb)
var checks := 0
var failures := 0
func _initialize() -> void: _run.call_deferred()
func check(ok: bool,label: String) -> void:
	checks += 1
	if not ok: failures += 1
	print("THROW_INPUT07 %s %s"%["PASS" if ok else "FAIL",label])
func event(action: String,pressed: bool) -> InputEventAction:
	var value := InputEventAction.new();value.action=action;value.pressed=pressed;return value
func _run() -> void:
	Prefs.setup_inputs()
	var bindings := InputMap.action_get_events("throw")
	check(bindings.size()==1 and bindings[0] is InputEventMouseButton and bindings[0].button_index==MOUSE_BUTTON_RIGHT,"new binding is right mouse, independent of left strike")
	var c := Client.new();var menu := Menu.new();var net := Transport.new()
	c.ui=menu;c.practice=net;c.practice_active=true;c.playing=true;c.role="human";c.local_id=1
	c.state={"actors":{1:{"role":"human","alive":true,"state":"human","tool":"newspaper"}}}
	c.personal={"throw":{"can_throw":true}}
	c._input(event("throw",false))
	check(net.actions.is_empty(),"release without local press never launches")
	c._unhandled_input(event("throw",true));c._input(event("throw",false))
	check(net.actions==["throw_start","throw_release"],"fresh press/release sends one authoritative pair")
	for cancel_action: String in ["pause","toggle_help"]:
		net.actions.clear();c._unhandled_input(event("throw",true));c._input(event(cancel_action,true));c._input(event("throw",false))
		check(net.actions==["throw_start","throw_cancel"],cancel_action+" cancels without release shot")
	for action: String in ["attack","self_swat","pickup","drop"]:
		net.actions.clear();c._unhandled_input(event("throw",true));c._unhandled_input(event(action,true));c._input(event("throw",false))
		check(net.actions==["throw_start","throw_cancel",action],action+" cancels preparation and keeps original action")
	net.actions.clear();menu.blocked=true;c._unhandled_input(event("throw",true));menu.blocked=false;c._input(event("throw",false))
	check(net.actions.is_empty(),"press in menu cannot arm a shot after closing")
	net.actions.clear();c._unhandled_input(event("throw",true));menu.blocked=true;c._input(event("throw",false));menu.blocked=false
	check(net.actions==["throw_start","throw_cancel"],"release while settings are open cancels")
	net.actions.clear();c._unhandled_input(event("throw",true));c.state.actors[1].tool="hands";c._input(event("throw",false));c.state.actors[1].tool="newspaper"
	check(net.actions==["throw_start","throw_cancel"],"losing held object cannot launch replacement")
	net.actions.clear();c._unhandled_input(event("throw",true));c._cancel_throw();c._input(event("throw",false))
	check(net.actions==["throw_start","throw_cancel"],"focus loss cancellation disarms the later release")
	net.actions.clear();c.personal.throw.can_throw=false;c._unhandled_input(event("throw",true));c._input(event("throw",false))
	check(net.actions.is_empty(),"nonthrowable or cooling down object does not arm locally")
	var key := InputEventKey.new();key.physical_keycode=KEY_T;Prefs.bind_action("throw",key,false)
	check(InputMap.action_get_events("throw")[0] is InputEventKey and Prefs.binding_text("throw")=="T","throw is reconfigurable without writing user profile")
	InputMap.action_erase_events("throw")
	for original: InputEvent in bindings: InputMap.action_add_event("throw",original)
	c.free();menu.free();net.free()
	print("THROW_INPUT07_RESULT checks=%d failures=%d"%[checks,failures]);quit(0 if failures==0 else 1)
