class_name HumanPresentation
extends RefCounted
## Cosmetic posture between snapshots. This never advances a simulation or
## predicts crouch intent. Contact-critical views opt out before posing.
const CROUCH_RATE := 6.0
var initialized := false
var crouch := 0.0
var last_pose_time := 0.0
var last_tool := ""

func clear() -> void:
	initialized=false
	crouch=0.0
	last_pose_time=0.0
	last_tool=""

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
