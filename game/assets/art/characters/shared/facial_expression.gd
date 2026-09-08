extends RefCounted
## Cosmetic reactions use only already-public, observed actor state. No target,
## assignment, opportunity or future action enters this facial controller.
const CHANNELS: Array[String] = ["BlinkL","BlinkR","GazeX","GazeY","BrowUp","BrowDown","MouthOpen","MouthSmile","MouthPress","CheekLift"]
var clock := 0.0
var seed := 0.0
var impact_timer := 0.0
var was_bitten := false
var was_stunned := false
var sleepy := 0.0
var alert := 0.0
var effort := 0.0
var impact := 0.0
var gaze := Vector2.ZERO
var context := "rest"

func _init(phase: float = 0.0) -> void:
	seed = fposmod(phase,1.0)

func _pulse(t: float, start: float, duration: float) -> float:
	var progress := (t-start)/duration
	if progress<0.0 or progress>1.0: return 0.0
	return smoothstep(0.0,.36,progress) if progress<.36 else 1.0-smoothstep(.36,1.0,progress)

func advance(data: Dictionary, species: String, dt: float, face: int = 0) -> Dictionary:
	dt = clampf(dt,0.0,.10)
	clock += dt
	var human := species=="human"
	var state := str(data.get("state","human" if human else "flying"))
	var bitten := bool(data.get("bitten",false))
	var stunned := state=="stunned"
	if (bitten and not was_bitten) or (stunned and not was_stunned): impact_timer = .50
	was_bitten = bitten
	was_stunned = stunned
	impact_timer = maxf(0.0,impact_timer-dt)
	var velocity: Vector3 = data.get("velocity",Vector3.ZERO)
	var speed := float(data.get("motion_speed",velocity.length())) if human else velocity.length()
	var strike: Dictionary = data.get("strike",{})
	var striking := bool(strike.get("active",false))
	var target_effort := clampf(speed/(5.0 if human else 3.8),0.0,1.0)*(.65 if bool(data.get("sprinting",false)) or not human else .18)
	if striking: target_effort = maxf(target_effort,sin(clampf(float(strike.get("progress",0.0)),0.0,1.0)*PI)*.9)
	if state=="biting": target_effort = .35
	var target_alert := .8 if bitten else .55 if int(data.get("help_target",0))!=0 else .35 if state=="biting" else 0.0
	var target_sleepy := (.82 if human else .35) if speed<.15 and not striking and not bitten and state not in ["biting","stunned"] else 0.0
	var target_impact := 1.0 if impact_timer>.28 else impact_timer/.28
	# The isolated editor/fixture may show the very same channels explicitly.
	if bool(data.get("preview_only",false)):
		match str(data.get("facial_preview","")):
			"sleepy": target_sleepy=1.0; target_alert=0.0; target_effort=0.0
			"alert": target_sleepy=0.0; target_alert=1.0; target_effort=0.0
			"effort": target_sleepy=0.0; target_alert=0.0; target_effort=.95
			"impact": target_sleepy=0.0; target_alert=.4; target_impact=.9
	var blend := 1.0-exp(-7.0*dt)
	sleepy = lerpf(sleepy,target_sleepy,blend)
	alert = lerpf(alert,target_alert,blend)
	effort = lerpf(effort,target_effort,blend)
	impact = lerpf(impact,target_impact,1.0-exp(-14.0*dt))
	context = "impact" if impact>.25 else "effort" if effort>.3 else "alert" if alert>.2 else "sleepy" if sleepy>.3 else "rest"
	var target_gaze := Vector2.ZERO
	if human:
		target_gaze.x = clampf(wrapf(float(data.get("yaw",0.0))-float(data.get("body_yaw",data.get("yaw",0.0))),-PI,PI)/1.31,-1.0,1.0)*.72
		target_gaze.y = clampf(float(data.get("pitch",0.0))*.45,-.6,.55)
	else:
		var local_velocity := velocity.rotated(Vector3.UP,-float(data.get("yaw",0.0)))
		target_gaze = Vector2(clampf(local_velocity.x*.14,-.65,.65),clampf(local_velocity.y*.13,-.45,.45))
	# Rare, tiny idle saccades give life without pretending to detect a target.
	if target_gaze.length()<.08:
		target_gaze += Vector2(sin(floor((clock+seed*7.0)/2.8)*2.399+seed)*.18,sin(clock*.31+seed*6.0)*.07)
	gaze = gaze.lerp(target_gaze,1.0-exp(-10.0*dt))
	var period := 4.2+seed*2.0
	var phase := fposmod(clock+seed*period,period)
	var blink_start := .8+seed*.6
	var blink_l := _pulse(phase,blink_start,.20)
	var blink_r := _pulse(phase,blink_start+.012,.20)
	if int((clock+seed*period)/period)%4==2:
		blink_l=maxf(blink_l,_pulse(phase,blink_start+.31,.17)*.8)
		blink_r=maxf(blink_r,_pulse(phase,blink_start+.32,.17)*.8)
	if bool(data.get("preview_only",false)) and bool(data.get("facial_no_blink",false)):
		blink_l=0.0
		blink_r=0.0
	var resting_lid := sleepy*(.19 if human else .09)+(.05 if face==1 and human else 0.0)
	var lid := maxf(resting_lid,impact*.45 if not stunned else .62)
	var breath := maxf(0.0,sin(clock*2.0+seed*4.0))
	var gaze_visibility := 1.0-maxf(lid,maxf(blink_l,blink_r))
	var result: Dictionary = {
		"BlinkL":clampf(maxf(blink_l,lid),0.0,1.0),"BlinkR":clampf(maxf(blink_r,lid),0.0,1.0),
		"GazeX":gaze.x*(1.0-impact*.6)*gaze_visibility,"GazeY":gaze.y*gaze_visibility,
		"BrowUp":clampf(alert*.75+impact*.6+sleepy*.04,0.0,1.0),"BrowDown":effort*.85,
		"MouthOpen":clampf(impact*.72+alert*.18+effort*breath*.18+sleepy*breath*.035,0.0,1.0),
		"MouthSmile":(.10 if human else .27)*(1.0-effort)*(1.0-impact),
		"MouthPress":effort*.62*(1.0-impact),"CheekLift":alert*.15+effort*.2,
	}
	if bool(data.get("preview_only",false)) and str(data.get("facial_preview",""))=="neutral":
		for channel: String in CHANNELS: result[channel] = 0.0
		context = "neutral"
	return result
