extends SceneTree
const Pose = preload("res://scripts/human_pose.gd")
const Emotes = preload("res://scripts/emote_pose.gd")
const Catalog = preload("res://scripts/emote_catalog.gd")
const Tools = preload("res://scripts/tool_catalog.gd")
const Sim = preload("res://scripts/simulation.gd")
var checks := 0
var failures := 0

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		print("EMOTE09 FAIL "+label)

func _initialize() -> void:
	for id: String in Catalog.IDS:
		var duration := float(Catalog.get_emote(id).duration)
		for crouch: float in [0.0,.5,1.0]:
			for tool: String in Tools.DATA:
				var base := {"p":Vector3(2,.4,-1),"yaw":.6,"body_yaw":.2,"pitch":-.9,"crouch_amount":crouch,"tool":tool,"motion_phase":1.1}
				var rest := Pose.sample(base)
				for index: int in range(13):
					var actor := base.duplicate(true)
					actor.emote_id=id
					actor.emote_time=duration*float(index)/12.0
					var posed := Pose.sample(actor)
					check(actor.p==base.p,"root position unchanged")
					for field: String in ["eye","head","pelvis","hip_l","hip_r","knee_l","knee_r","ankle_l","ankle_r","foot_direction_l","foot_direction_r"]:
						check(posed[field]==rest[field],"camera and grounded lower body unchanged "+field)
					if index in [0,12]: check(posed==rest,"smooth entry/exit returns exact base pose")
					for side: String in ["l","r"]:
						check(Vector3(posed["hand_"+side]).distance_to(posed["shoulder_"+side])<Pose.ARM_REACH,"gesture inside real reach")
						check(Vector3(posed["hand_"+side]).distance_to(posed["elbow_"+side])>.05,"no collapsed forearm")
					if tool!="hands":
						for field: String in ["shoulder_r","elbow_r","hand_r","tool_grip","tool_direction","tool_normal","hand_width_r","hand_direction_r"]:
							check(posed[field]==rest[field],"equipped right arm and grasp unchanged "+field)
					for zone: Dictionary in Sim.BODY_ZONES:
						var contact := Pose.zone_pose(actor,zone,posed)
						check(Vector3(contact.p).is_finite() and is_equal_approx(Vector3(contact.normal).length(),1.0),"all original zone IDs follow shared pose")
				var actor := base.duplicate(true)
				actor.emote_id=id
				actor.emote_time=.7
				check(Pose.cache_key(actor)!=Pose.cache_key(base),"emote changes owning simulation cache signature")
				var previous_key := Pose.cache_key(actor)
				actor.emote_time+=.01
				check(Pose.cache_key(actor)!=previous_key,"elapsed time invalidates cached joints")
				actor.strike={"active":true,"hand":"left","tool":"hands","point":Vector3(1.6,1.0,-1.6),"normal":Vector3.FORWARD,"progress":.5}
				base.strike=actor.strike
				check(Pose.sample(actor)==Pose.sample(base),"manual strike has priority over gesture")
				actor.erase("strike")
				base.erase("strike")
				for state: String in ["charging","release","recovering"]:
					actor.throw_gesture={"state":state,"progress":.5,"power":.7,"direction":Vector3.FORWARD,"tool":"newspaper"}
					base.throw_gesture=actor.throw_gesture
					check(Pose.sample(actor)==Pose.sample(base),"throw keeps exact origin/hand pose "+state)
	print("EMOTE09 %d/%d PASS"%[checks-failures,checks])
	quit(1 if failures else 0)
