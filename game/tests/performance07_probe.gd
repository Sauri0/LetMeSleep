extends RefCounted
## Test-only recorder used by work/cpu07_instrument.py; production has no hooks
## after the generator restores the verified original source bytes.
static var collecting := false
static var calls: Dictionary = {}
static var current: Dictionary = {}
static var frames: Array[Dictionary] = []
static var tick_counts: Array[int] = []
static var startup: Dictionary = {}

static func begin() -> void:
	collecting=false
	calls.clear()
	current.clear()
	frames.clear()
	tick_counts.clear()

static func add(label: String, usec: int) -> void:
	if label.begins_with("startup."):
		if not startup.has(label):startup[label]=[]
		startup[label].append(float(usec)/1000.0)
		return
	if not collecting:return
	if not calls.has(label):calls[label]=[]
	calls[label].append(float(usec)/1000.0)
	current[label]=float(current.get(label,0.0))+float(usec)/1000.0

static func frame(ticks: int, measured: bool) -> void:
	if collecting and measured:
		frames.append(current)
		tick_counts.append(ticks)
	current={}
	collecting=measured

static func stats(values: Array) -> Dictionary:
	if values.is_empty():return {}
	var sorted: Array=values.duplicate()
	sorted.sort()
	var total:=0.0
	for value: float in sorted:total+=value
	return {"count":sorted.size(),"mean":total/sorted.size(),"p50":sorted[int(sorted.size()*.5)],"p90":sorted[int(sorted.size()*.9)],"p99":sorted[mini(sorted.size()-1,int(sorted.size()*.99))],"max":sorted[-1]}

static func report() -> Dictionary:
	var result: Dictionary={"per_call_ms":{},"per_frame_ms":{},"physics_ticks_per_frame":{},"frames":frames.size(),"startup_ms":{}}
	for label: String in startup:result.startup_ms[label]=stats(startup[label])
	for label: String in calls:
		result.per_call_ms[label]=stats(calls[label])
		var values: Array=[]
		for frame_data: Dictionary in frames:values.append(float(frame_data.get(label,0.0)))
		result.per_frame_ms[label]=stats(values)
	for tick: int in tick_counts:
		result.physics_ticks_per_frame[str(tick)]=int(result.physics_ticks_per_frame.get(str(tick),0))+1
	return result
