extends "C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/perf09-view-instrumentation/1588/world-base.gd"
const Recorder = preload("C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/perf09-view-instrumentation/1588/recorder.gd")

func _process(dt: float) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="World._process"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super._process(dt)
	if trace!=null:trace.leave(label,tracked)

func sync_actors(data: Dictionary, local_id: int, dt: float, critical_human_id: int=0) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="World.sync_actors"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super.sync_actors(data,local_id,dt,critical_human_id)
	if trace!=null:trace.leave(label,tracked)

func sync_doors(states: Dictionary, dt: float) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="World.sync_doors"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super.sync_doors(states,dt)
	if trace!=null:trace.leave(label,tracked)

func sync_pickups(pickups: Dictionary, dt: float=1.0/60.0) -> void:
	var trace: RefCounted=Recorder.current
	var label: String="World.sync_pickups"
	var tracked: bool=trace.enter(label) if trace!=null else false
	super.sync_pickups(pickups,dt)
	if trace!=null:trace.leave(label,tracked)
