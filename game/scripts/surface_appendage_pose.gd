class_name SurfaceAppendagePose
extends RefCounted
## Cosmetic tarsi follow actual distance, never an idle wall clock. The body
## orientation and support solver remain in MosquitoPose/SurfaceLocomotion.
const SCALE := .35
const SUPPORT_OFFSET := .045
const FOOT_RADIUS := .0015
const DUTY := .50
const STEP_DISTANCE := .05

static func leg_target(rest_tip: Vector3, phase: float, side: int, leg: int, lift_blend: float) -> Dictionary:
	# Three foot cycles per public .15m motion cycle. Equal stance/return halves
	# retain an alternating tripod and give the returning foot enough time at
	# 60Hz; the former 38% return squeezed its travel into fewer than two frames.
	var alternating := (leg+(1 if side>0 else 0))%2
	var cycle := fposmod(phase*3.0+float(alternating)*PI,TAU)/TAU
	var stance := cycle<DUTY
	var half_travel := STEP_DISTANCE/SCALE*DUTY*.5
	var travel: float
	var lift := 0.0
	if stance:
		travel=lerpf(-half_travel,half_travel,cycle/DUTY)
	else:
		var swing := (cycle-DUTY)/(1.0-DUTY)
		travel=lerpf(half_travel,-half_travel,smoothstep(0.0,1.0,swing))
		lift=sin(swing*PI)*.011/SCALE*lift_blend
	var point := rest_tip+Vector3.BACK*travel
	point.y=(-SUPPORT_OFFSET+FOOT_RADIUS)/SCALE+lift
	return {"tip":point,"stance":stance or lift_blend<.01,"lift":lift,"cycle":cycle}

static func knee(root: Vector3, rest_knee: Vector3, rest_tip: Vector3, tip: Vector3) -> Vector3:
	var first := root.distance_to(rest_knee)
	var second := rest_knee.distance_to(rest_tip)
	var direction := (tip-root).normalized()
	var distance := maxf(root.distance_to(tip),.0001)
	var along := clampf((first*first-second*second+distance*distance)/(2.0*distance),0.0,first)
	# Transport the authored bend with the leg axis. Projecting a fixed pole
	# onto a changing axis can flip the knee below a lifted foot; its cuff then
	# forces the sole solver to raise/lower the entire tarsus in one frame.
	var rest_direction := (rest_tip-root).normalized()
	var rest_bend := (rest_knee-root).slide(rest_direction).normalized()
	var bend := Basis(Quaternion(rest_direction,direction))*rest_bend
	return root+direction*along+bend*sqrt(maxf(0.0,first*first-along*along))
