extends SceneTree
## Headless UI contract: isolated controls only. No Main, EOS, viewport scenes,
## native clipboard calls, preference writes or game/renderer startup.
const OnlineCode = preload("res://scripts/online_invitation.gd")
const LegacyCode = preload("res://scripts/invitation.gd")

class UIHarness extends "res://scripts/ui.gd":
	var focused: Control
	var copied: Array[String] = []
	var remembered: Array = []
	func _ready() -> void:
		pass
	func _build() -> void:
		pass
	func _queue_focus(control: Control) -> void:
		focused = control
	func _set_screen(value: String) -> void:
		_screen = value
	func _input_release() -> void:
		pass
	func _write_invitation_clipboard(value: String) -> void:
		copied.append(value)
	func _remember_online_connection(username: String, invite: String, create: bool) -> void:
		remembered = [username, invite, create]
	func prepare() -> void:
		_root = Control.new()
		add_child(_root)
		_name_edit = LineEdit.new()
		_invitation_edit = LineEdit.new()
		_invitation_paste = Button.new()
		_connect_submit = Button.new()
		_connection_cancel = Button.new()
		_connection_retry = Button.new()
		_connection_feedback = Label.new()
		_lobby_copy_button = Button.new()
		_connection_form = VBoxContainer.new()
		_home_menu = VBoxContainer.new()
		_home_default_focus = Button.new()
		for control: Control in [_name_edit, _invitation_edit, _invitation_paste, _connect_submit, _connection_cancel, _connection_retry, _connection_feedback, _lobby_copy_button, _connection_form, _home_menu, _home_default_focus]:
			_root.add_child(control)
		# Exercise the real sharing panel without attaching it to a scene tree.
		_build_invite_settings()
		_connection_open = true

var failures: Array[String] = []
var checks := 0
var host_requests: Array = []
var join_requests: Array = []
var legacy_requests := 0
var cancellations := 0

func _initialize() -> void:
	run.call_deferred()

func check(value: bool, label: String) -> void:
	checks += 1
	print("ONLINE093_UI %s %s" % ["PASS" if value else "FAIL", label])
	if not value:
		failures.append(label)

func run() -> void:
	var ui := UIHarness.new()
	ui.prepare()
	ui.online_host_requested.connect(func(username: String) -> void: host_requests.append(username))
	ui.online_join_requested.connect(func(username: String, code: String) -> void: join_requests.append([username, code]))
	ui.connect_requested.connect(func(_a: String, _p: int, _n: String, _c: String, _h: bool) -> void: legacy_requests += 1)
	ui.host_requested.connect(func(_n: String, _p: int) -> void: legacy_requests += 1)
	ui.local_server_requested.connect(func() -> void: legacy_requests += 1)
	ui.cancel_connection_requested.connect(func() -> void: cancellations += 1)
	var code := OnlineCode.encode("online093-room-a", "0123456789abcdef0123456789abcdef")
	var second := OnlineCode.encode("online093-room-b", "fedcba9876543210fedcba9876543210")
	check(ui._lobby_copy_button.disabled and ui._invite_copy_button.disabled, "copy disabled while code is preparing")
	check(not ui._invite_code_edit.editable and ui._invite_code_edit.selecting_enabled and ui._invite_code_edit.focus_mode == Control.FOCUS_ALL, "real share panel code is readonly, selectable and keyboard focusable")
	ui._name_edit.text = "  "
	ui._request_connection(true)
	check(host_requests.is_empty() and not ui._connection_feedback.text.is_empty() and ui.focused == ui._name_edit, "missing name rejected in connection feedback")
	ui._name_edit.text = "  Online player  "
	ui._request_connection(true)
	check(host_requests == ["Online player"] and legacy_requests == 0, "create emits only online host with trimmed name")
	check(ui._connection_busy and ui._connect_submit.disabled and ui._invitation_paste.disabled and ui.focused == ui._connection_cancel, "pending connection locks fields and focuses cancel")
	ui._request_connection(true)
	check(host_requests.size() == 1, "duplicate create blocked while pending")
	ui._close_connection()
	check(cancellations == 1 and not ui._connection_busy and not ui._connection_open, "back cancels pending connection and returns to menu")
	ui._connection_open = true
	for invalid: String in ["", "ABC123", "192.168.1.2", LegacyCode.encode("192.168.1.2", 27840, "ABC123", "lan"), "LMS1-broken", code + "!"]:
		ui._invitation_edit.text = invalid
		ui._request_connection(false)
		check(join_requests.is_empty() and legacy_requests == 0 and not ui._connection_feedback.text.is_empty(), "invalid or legacy code rejected without fallback")
	ui._apply_pasted_invitation("  " + code + "\n")
	check(ui._invitation_edit.text == code and ui.focused == ui._invitation_edit, "paste trims exterior whitespace and focuses code")
	ui._apply_pasted_invitation(code + "x".repeat(OnlineCode.MAX_LENGTH))
	check(ui._invitation_edit.text == code and ui._connection_feedback.text.contains("largo"), "oversized paste is rejected without truncating or replacing code")
	ui._request_connection(false)
	check(join_requests == [["Online player", code]] and legacy_requests == 0, "valid code emits online join only")
	ui.show_connection_state({"phase":"failed", "message":"No pudimos entrar.", "can_retry":true})
	check(not ui._connection_busy and ui._invitation_edit.editable and ui._connection_retry.visible and ui.focused == ui._connection_retry, "failure restores editable fields and retry focus")
	ui._apply_pasted_invitation(second)
	ui._request_connection(false)
	check(join_requests.size() == 2 and join_requests[1][1] == second, "resubmission uses newly pasted code")
	ui.show_connection_state({"phase":"cancelled"})
	ui._screen = "lobby"
	ui.set_online_invitation(code)
	check(ui._invite_code_edit.text == code and not ui._invite_copy_button.disabled and not ui._lobby_copy_button.disabled, "valid invitation populates sharing panel and enables copy")
	ui._copy_invitation()
	check(ui.copied == [code] and ui._invite_status.text.contains("copiado"), "copy writes exact online token and displays confirmation")
	ui.set_online_invitation("ABC123")
	ui._copy_invitation()
	check(ui.copied == [code] and ui._invite_code_edit.text.is_empty() and ui._invite_copy_button.disabled, "invalid invitation clears displayed code and cannot touch clipboard")
	ui.set_online_invitation(second)
	ui.show_home()
	ui._copy_invitation()
	check(ui._online_invitation.is_empty() and ui._invite_code_edit.text.is_empty() and ui._lobby_copy_button.disabled and ui.copied == [code], "leaving room clears invitation and blocks stale copy")
	# Raw source is intentionally absent from compiled exports.
	if "--source-contracts" in OS.get_cmdline_user_args():
		var source := FileAccess.get_file_as_string("res://scripts/ui.gd")
		check(not source.contains("host_requested.emit(username,") and not source.contains("connect_requested.emit("), "menu source contains no legacy network dispatch")
		check(not source.contains("Conexión directa") and not source.contains("Dirección para amigos") and not source.contains("Puerto UDP") and not source.contains("DIRECCIÓN DEL ANFITRIÓN"), "menu source contains no legacy network controls")
		check(source.contains('_code_label.text = "SALA ONLINE"') and not source.contains('_code_label.set_meta("code"'), "lobby title cannot display legacy six-character room code")
		check(source.contains('_invitation_edit.text_submitted.connect') and source.contains('_small_button("Reintentar", func() -> void: _request_connection(_connection_mode_create))'), "Enter and retry are wired to current online form")
		check(source.contains('_home_default_focus = _button("CREAR SALA ONLINE"') and source.contains('_small_button("Entrenamiento", _open_practice)'), "online create is primary and training is secondary")
	ui.free()
	print("ONLINE093_UI_RESULT checks=%d failures=%d" % [checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
