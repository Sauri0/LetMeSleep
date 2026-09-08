extends "C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/perf09-view-instrumentation/1588/client-base.gd"
const Recorder = preload("C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/perf09-view-instrumentation/1588/recorder.gd")

func _process(dt: float) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Client._process"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._process(dt)
	if trace!=null:trace.leave(label,tracked)

func _physics_process(dt: float) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Client._physics_process"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._physics_process(dt)
	if trace!=null:trace.leave(label,tracked)

func _flush_game_hud() -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Client._flush_game_hud"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._flush_game_hud()
	if trace!=null:trace.leave(label,tracked)

func _snapshot(data: Dictionary) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Client._snapshot"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._snapshot(data)
	if trace!=null:trace.leave(label,tracked)

func _private(data: Dictionary) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Client._private"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._private(data)
	if trace!=null:trace.leave(label,tracked)
