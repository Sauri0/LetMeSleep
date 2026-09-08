extends SceneTree
const View=preload("res://scripts/actor_view.gd")
const Pose=preload("res://scripts/human_pose.gd")
var checks:=0
var failures:=0
func check(value:bool,label:String)->void:
	checks+=1
	if not value:
		failures+=1
		print("CHARACTER_CACHE07 FAIL ",label)
func _initialize()->void:
	_run.call_deferred()
func _run()->void:
	var avatar:=View.new()
	root.add_child(avatar)
	avatar.build("human","Cache",0)
	var data:Dictionary={"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"pitch":0.0,"state":"human","alive":true,"tool":"hands","grounded":true,"crouch_amount":0.0}
	avatar.update_state(data,1.0/60.0)
	var skin:Node3D=avatar.imported_skin
	var before:Dictionary=avatar.body_pose.duplicate(true)
	var skin_before:Dictionary=skin.human_pose_values.duplicate(true)
	var time_before:float=skin.time
	avatar.update_state(data,1.0/60.0)
	check(avatar.body_pose==before and skin.human_pose_values==skin_before,"equal snapshots preserve exact geometry")
	check(float(skin.time)>time_before,"blink and grip time continue between snapshots")
	# Mutate the caller's original dictionary and force the fast hash to match.
	# The independent value signature must still detect the new geometry.
	data.crouch_amount=1.0
	var expected:Dictionary=Pose.sample(data)
	avatar.human_snapshot_hash=hash(data)
	skin.human_pose_hash=hash(expected)
	avatar.update_state(data,1.0/60.0)
	check(avatar.body_pose==expected,"snapshot hash collision cannot retain stale pose")
	check(skin.human_pose_values==expected,"skin hash collision cannot retain stale skeleton")
	check(avatar.human_snapshot_values.crouch_amount==1.0,"snapshot signature updated")
	check(float(before.head.y)>float(avatar.body_pose.head.y)+.4,"crouch actually changes rendered pose")
	data.strike={"active":true,"progress":.45,"point":Vector3(-.2,.85,-.4),"normal":Vector3.FORWARD,"hand":"right","tool":"hands"}
	avatar.update_state(data,1.0/60.0)
	var old_progress:float=avatar.human_snapshot_values.strike.progress
	data.strike.progress=.8
	check(float(avatar.human_snapshot_values.strike.progress)==old_progress,"nested signature has no caller alias")
	avatar.human_snapshot_hash=hash(data)
	skin.human_pose_hash=hash(Pose.sample(data))
	avatar.update_state(data,1.0/60.0)
	check(avatar.body_pose==Pose.sample(data) and skin.human_pose_values==avatar.body_pose,"nested strike mutation refreshes both layers")
	avatar.queue_free()
	await process_frame
	await process_frame
	print("CHARACTER_CACHE07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
