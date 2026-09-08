extends "res://scripts/actor_view.gd"
const Recorder = preload("C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/perf09-view-instrumentation/1588/recorder.gd")

func update_state(data: Dictionary, dt: float) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Actor."+actor_role+".update_state"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super.update_state(data,dt)
	if trace!=null:trace.leave(label,tracked)

func _apply_human_pose(data: Dictionary, dt: float, authoritative_data: Dictionary={}) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Actor.human.pose"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._apply_human_pose(data,dt,authoritative_data)
	if trace!=null:trace.leave(label,tracked)

func _apply_human_colliders(data: Dictionary) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Actor.human.colliders"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._apply_human_colliders(data)
	if trace!=null:trace.leave(label,tracked)

func _process(dt: float) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="Actor."+actor_role+".voice_process"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._process(dt)
	if trace!=null:trace.leave(label,tracked)
