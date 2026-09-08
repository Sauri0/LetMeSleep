class_name SurfacePresentation
extends RefCounted
## Local presentation only. Adjacent public support planes reconstruct the
## same expanded-box edge traversed by SurfaceLocomotion; no support ID travels.
const MAX_POINTS := 12
const MAX_SAMPLE_DISTANCE := .35
const PUBLIC_STRIDE := .15
const EPS := .000001
var active := false
var points: Array[Dictionary] = []
var position := Vector3.ZERO
var normal := Vector3.ZERO
var forward := Vector3.FORWARD
var phase := 0.0
var previous: Dictionary = {}

func clear() -> void:
	active=false
	points.clear()
	previous.clear()

func _reset(sample: Dictionary) -> void:
	points.clear()
	position=sample.p
	normal=sample.n
	forward=sample.f
	phase=sample.phase
	previous=sample
	active=true

func _append(sample: Dictionary, corner: bool=false) -> void:
	# Coalesce only the last straight segment; never remove its preceding edge.
	if not points.is_empty() and not bool(points.back().get("corner",false)) and Vector3(points.back().n).dot(sample.n)>.9999:
		points.pop_back()
	var point := sample.duplicate()
	point.corner=corner
	points.append(point)

func advance(data: Dictionary, dt: float, force_reset: bool=false) -> Dictionary:
	var n: Vector3=data.get("surface_normal",Vector3.ZERO)
	var f: Vector3=data.get("surface_forward",Vector3.ZERO)
	if str(data.get("state",""))!="perched" or n.length_squared()<.9 or f.slide(n).length_squared()<.1:
		clear()
		return {}
	n=n.normalized();f=f.slide(n).normalized()
	var raw_phase := float(data.get("motion_phase",0.0))
	var sample := {"p":Vector3(data.get("p",Vector3.ZERO)),"n":n,"f":f,"phase":raw_phase,"raw_phase":raw_phase}
	var reset := force_reset or not active
	var changed: bool = not reset and (sample.p!=previous.p or sample.n!=previous.n or sample.f!=previous.f or raw_phase!=float(previous.raw_phase))
	if changed:
		var distance := Vector3(previous.p).distance_to(sample.p)
		var route_distance := distance
		var edge_sample: Dictionary = {}
		var edge_fraction := 0.0
		var dot: float=Vector3(previous.n).dot(n)
		reset=distance>MAX_SAMPLE_DISTANCE or (dot<.9999 and absf(dot)>.0001)
		if not reset and dot>.9999:
			# Parallel but different planes represent a new support, not a route.
			reset=absf((Vector3(sample.p)-Vector3(previous.p)).dot(n))>.001
		elif not reset:
			var old_normal: Vector3=previous.n
			var old: Vector3=previous.p
			var target: Vector3=sample.p
			var edge := old+n*(target.dot(n)-old.dot(n))
			var axis := old_normal.cross(n).normalized()
			var old_distance := old.distance_to(edge)
			var next_distance := absf((target-old).dot(old_normal))
			var fraction := old_distance/maxf(EPS,old_distance+next_distance)
			edge+=axis*(target-old).dot(axis)*fraction
			route_distance=old.distance_to(edge)+edge.distance_to(target)
			reset=route_distance>MAX_SAMPLE_DISTANCE*1.5
			if not reset:
				edge_sample={"p":edge,"n":old_normal,"f":previous.f}
				edge_fraction=fraction
		if not reset:
			# Authority wraps phase in TAU. Lift it to positive travelled distance
			# before interpolation, including whole strides lost between packets.
			var advance_phase := fposmod(raw_phase-float(previous.raw_phase),TAU)
			if is_equal_approx(raw_phase,float(previous.raw_phase)): advance_phase=0.0
			var expected := route_distance/PUBLIC_STRIDE*TAU
			advance_phase+=maxf(0.0,roundf((expected-advance_phase)/TAU))*TAU
			sample.phase=float(previous.phase)+advance_phase
			if not edge_sample.is_empty():
				edge_sample.phase=lerpf(float(previous.phase),float(sample.phase),edge_fraction)
				_append(edge_sample,true)
			_append(sample)
			previous=sample
			reset=points.size()>MAX_POINTS
	if reset:
		_reset(sample)
	else:
		var length := 0.0
		var start := position
		for point: Dictionary in points:
			length+=start.distance_to(point.p)
			start=point.p
		var budget := length*(1.0-exp(-18.0*maxf(dt,0.0)))
		while not points.is_empty():
			var point: Dictionary=points.front()
			var distance := position.distance_to(point.p)
			var weight := 1.0 if distance<EPS else minf(1.0,budget/distance)
			normal=point.n
			forward=Vector3(forward).lerp(Vector3(point.f),weight).slide(normal).normalized()
			if forward.length_squared()<.5: forward=point.f
			phase=lerpf(phase,float(point.phase),weight)
			position=position.lerp(point.p,weight)
			if weight<1.0: break
			budget=maxf(0.0,budget-distance)
			points.pop_front()
	var result := data.duplicate()
	result.p=position
	result.surface_normal=normal
	result.surface_forward=forward
	result.motion_phase=phase
	result.presentation_reset=reset
	return result
