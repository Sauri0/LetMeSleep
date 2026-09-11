class_name HumanPresentation
extends RefCounted
## Cosmetic posture between snapshots. This never advances a simulation or
## predicts crouch intent. Contact-critical views opt out before posing.
const CROUCH_RATE := 6.0
const MOTION_RATE := 18.0
const MOTION_SCALARS := ["pitch", "motion_speed", "motion_blend", "motion_stride", "air_blend", "land_blend", "turn_blend", "pose_time"]
var initialized := false
var crouch := 0.0
var last_pose_time := 0.0
var last_tool := ""
var motion_sample: Dictionary = {}
var motion_time := 0.0
var motion_tool := ""
var motion_exact := true

func clear() -> void:
	initialized=false
	crouch=0.0
	last_pose_time=0.0
	last_tool=""
	motion_sample.clear()
	motion_time=0.0
	motion_tool=""
	motion_exact=true

static func needs_exact_pose(data: Dictionary) -> bool:
	return not bool(data.get("alive",true)) or str(data.get("state","human"))!="human" or bool(data.get("bitten",false)) or bool(data.get("threatened",false)) or float(data.get("swing",0.0))>0.0 or bool(Dictionary(data.get("strike",{})).get("active",false)) or str(Dictionary(data.get("throw_gesture",{})).get("state","idle")) not in ["","idle"] or not str(data.get("emote_id","")).is_empty()

func advance(data: Dictionary, dt: float, critical: bool=false, reset: bool=false) -> Dictionary:
	var target := clampf(float(data.get("crouch_amount",0.0)),0.0,1.0)
	var pose_time := float(data.get("pose_time",0.0))
	var tool := str(data.get("tool","hands"))
	# A pose without its public clock is a static/editor pose, not an observed
	# temporal transition. Keep direct pose fixtures and previews exact.
	var exact := reset or not initialized or critical or needs_exact_pose(data) or not data.has("pose_time") or not is_finite(dt) or dt<0.0 or not is_finite(pose_time) or pose_time<last_pose_time or tool!=last_tool
	if exact:crouch=target
	else:crouch=move_toward(crouch,target,maxf(0.0,dt)*CROUCH_RATE)
	initialized=true
	last_pose_time=pose_time
	last_tool=tool
	if crouch==target:return data
	var presented := data.duplicate()
	presented.crouch_amount=crouch
	return presented

## A single bounded render sample for turn, head and gait. Never extrapolate
## the public clock or run locomotion here. Authority dictionaries stay intact.
## Crouch keeps its existing contact policy in advance(), above.
func advance_motion(data: Dictionary, dt: float, critical: bool=false, reset: bool=false, preserve_view: bool=false) -> Dictionary:
	var time := float(data.get("pose_time",0.0))
	var tool := str(data.get("tool","hands"))
	motion_exact = reset or motion_sample.is_empty() or critical or needs_exact_pose(data) or not data.has("pose_time") or not is_finite(dt) or dt<0.0 or not is_finite(time) or time<motion_time or tool!=motion_tool
	var shown := data.duplicate()
	if not motion_exact:
		var weight := 1.0-exp(-MOTION_RATE*dt)
		for key: String in ["body_yaw","yaw","motion_phase"]:
			if preserve_view and key!="motion_phase":continue
			if data.has(key) and motion_sample.has(key):
				shown[key]=lerp_angle(float(motion_sample[key]),float(data[key]),weight)
		for key: String in MOTION_SCALARS:
			if preserve_view and key=="pitch":continue
			if data.has(key) and motion_sample.has(key):
				shown[key]=lerpf(float(motion_sample[key]),float(data[key]),weight)
		if data.has("motion_direction") and motion_sample.has("motion_direction"):
			shown.motion_direction=Vector3(motion_sample.motion_direction).lerp(Vector3(data.motion_direction),weight)
	motion_sample=shown
	motion_time=time
	motion_tool=tool
	return shown
