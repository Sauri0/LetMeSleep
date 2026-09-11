class_name EmotePose
extends RefCounted
## Public, deterministic upper-body poses. No root motion, camera motion, target
## selection or per-renderer smoothing: HumanPose remains the contact authority.
const Catalog = preload("res://scripts/emote_catalog.gd")

static func sample(actor: Dictionary) -> Dictionary:
	var id := str(actor.get("emote_id",""))
	if not Catalog.is_valid(id): return {}
	if bool(Dictionary(actor.get("strike",{})).get("active",false)): return {}
	if str(Dictionary(actor.get("throw_gesture",{})).get("state","idle")) in ["charging","release","recovering"]: return {}
	var seconds := float(actor.get("emote_time",0.0))
	var duration := float(Catalog.get_emote(id).duration)
	if not is_finite(seconds) or seconds<=0.0 or seconds>=duration: return {}
	var weight := smoothstep(0.0,.28,seconds)*(1.0-smoothstep(duration-.38,duration,seconds))
	if weight<.000001: return {}
	return {"id":id,"time":seconds,"phase":seconds/duration,"weight":weight}

static func apply_human(pose: Dictionary, actor: Dictionary) -> void:
	var state := sample(actor)
	if state.is_empty(): return
	var weight := float(state.weight)
	var seconds := float(state.time)
	var equipped := str(actor.get("tool","hands"))!="hands"
	var head: Vector3 = pose.head
	for side: float in [-1.0,1.0]:
		# A held object keeps its exact authored grip and its whole arm. The free
		# hand carries the gesture, including the hand covering a yawn.
		if side>0.0 and (equipped or state.id in ["wave","yawn"]): continue
		var suffix := "_l" if side<0.0 else "_r"
		var shoulder: Vector3 = pose["shoulder"+suffix]
		var wrist: Vector3
		var elbow: Vector3
		var fingers := Vector3.UP
		var normal := Vector3.FORWARD
		match str(state.id):
			"wave":
				var wag := sin((seconds-.28)*TAU*1.65)
				wrist=shoulder+Vector3(side*(.18+wag*.035),.26,-.45)
				elbow=shoulder+Vector3(side*.19,-.10,-.25)
				fingers=Vector3(side*wag*.26,1,0).normalized()
			"celebrate":
				var pump := sin((seconds-.28)*TAU*1.3)*.035
				wrist=shoulder+Vector3(side*.16,.42+pump,-.20)
				elbow=shoulder+Vector3(side*.23,.12+pump*.35,-.09)
				fingers=Vector3(side*.15,1,0).normalized()
			"shrug":
				pose["shoulder"+suffix]+=Vector3.UP*.025*weight
				wrist=shoulder+Vector3(side*.21,-.11,-.31)
				elbow=shoulder+Vector3(side*.19,-.24,-.10)
				fingers=Vector3(side*.75,0,-.65).normalized()
				normal=Vector3.UP
			"yawn":
				wrist=head+Vector3(-.13,-.18,-.30)
				elbow=shoulder+Vector3(-.13,-.20,-.18)
				normal=Vector3.BACK
		pose["elbow"+suffix]=Vector3(pose["elbow"+suffix]).lerp(elbow,weight)
		pose["hand"+suffix]=Vector3(pose["hand"+suffix]).lerp(wrist,weight)
		var base_direction := (Vector3(pose["hand"+suffix])-Vector3(pose["elbow"+suffix])).normalized()
		var base_width := (Vector3.RIGHT-base_direction*base_direction.x).normalized()
		# The imported hand's anatomical palm is negative frame Z.
		var width := normal.cross(fingers).normalized()
		var basis := Basis(base_width,base_direction,base_width.cross(base_direction))
		var desired := Basis(width,fingers,width.cross(fingers))
		var blended := Basis(basis.get_rotation_quaternion().slerp(desired.get_rotation_quaternion(),weight))
		pose["hand_direction"+suffix]=blended.y
		pose["hand_width"+suffix]=blended.x
	# Small nod/tilt; the head's centre, camera and lower-body support never move.
	var tilt := -.09 if state.id=="shrug" else .065*sin(seconds*TAU*.65) if state.id=="celebrate" else 0.0
	var nod := -.07 if state.id=="yawn" else 0.025*sin(seconds*TAU*.7) if state.id=="wave" else 0.0
	pose.head_basis=Basis(Vector3.BACK,tilt*weight)*Basis(Vector3.RIGHT,nod*weight)*Basis(pose.head_basis)
	pose.torso_basis=Basis(Vector3.UP,sin(seconds*TAU*.65)*.018*weight)*Basis(pose.torso_basis)

static func apply_face(values: Dictionary, actor: Dictionary) -> void:
	var state := sample(actor)
	if state.is_empty(): return
	var targets: Dictionary = {}
	match str(state.id):
		"wave": targets={"BrowUp":.25,"MouthSmile":.35}
		"celebrate": targets={"BrowUp":.40,"MouthSmile":.65,"MouthOpen":.18}
		"shrug": targets={"BrowUp":.50,"MouthPress":.32}
		"yawn": targets={"BlinkL":.65,"BlinkR":.65,"MouthOpen":.62,"MouthSmile":0.0,"BrowUp":.16}
	for channel: String in targets:
		var target := float(targets[channel])
		# A spontaneous complete blink still closes the eyes during a gesture.
		if channel.begins_with("Blink"): target=maxf(target,float(values.get(channel,0.0)))
		values[channel]=lerpf(float(values.get(channel,0.0)),target,float(state.weight))
