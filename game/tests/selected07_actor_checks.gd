extends SceneTree
const Actor=preload("res://scripts/actor_view.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Mosquito=preload("res://scripts/mosquito_pose.gd")
const Facial=preload("res://assets/art/characters/shared/facial_expression.gd")
var checks:=0
var failures:=0
func check(value:bool,message:String)->void:
	checks+=1
	if not value:failures+=1;print("SELECTED07_ACTOR_FAIL ",message)
func _initialize()->void:_run.call_deferred()
func _run()->void:
	for role:String in ["human","mosquito"]:
		var actor:=Actor.new();root.add_child(actor);actor.build(role,"",0)
		for face:int in range(3):
			actor.apply_appearance({"color":1,"accent":4,"face":face,"outfit":0,"accessory":3 if role=="human" else 0,"footwear":0})
			var selected_parts:=0
			for mesh:MeshInstance3D in actor.imported_skin.face_channels:
				if not mesh.visible:continue
				selected_parts+=1
				for channel:String in Facial.CHANNELS:check(actor.imported_skin.face_channels[mesh].has(channel),role+str(face)+" "+channel)
			check(selected_parts==3,role+str(face)+" three independent facial parts selected")
		if role=="human":
			for crouch:float in [0.0,1.0,.5,0.0]:
				var data:Dictionary={"state":"human","p":Vector3.ZERO,"body_yaw":.7,"yaw":.9,"crouch_amount":crouch,"motion_phase":1.8,"motion_speed":3.1,"appearance":{"color":1,"accent":4,"face":1,"outfit":0,"accessory":3}}
				actor.update_state(data,1.0)
				for piece:Dictionary in Pose.collision_segments(data):
					var key:String=str(piece.key).replace("upperarm_","upper_arm_")
					var collider:StaticBody3D=actor.pose_colliders[key]
					var shape:Shape3D=collider.get_child(0).shape
					check(collider.position.distance_to((Vector3(piece.from)+Vector3(piece.to))*.5)<.0001,"ray collider position "+key)
					check(absf(shape.radius-float(piece.radius))<.0001,"ray collider radius "+key)
					if shape is CapsuleShape3D:check(absf(shape.height-(Vector3(piece.from).distance_to(piece.to)+float(piece.radius)*2.0))<.0001,"ray collider length "+key)
			var material:StandardMaterial3D=actor.imported_skin.material_cache["skin"]
			check(material.albedo_color.is_equal_approx(Color("e3ac83")),"skin imports intended sRGB colour")
			var cloth:ShaderMaterial=actor.imported_skin.material_cache["primary"]
			check(Color(cloth.get_shader_parameter("primary_color")).is_equal_approx(Color("78bbc2")),"custom primary retains chosen colour")
		else:
			for scale_factor:float in [1.0,3.0]:
				actor.scale=Vector3.ONE*scale_factor
				for state:String in ["flying","biting","perched","stunned","flying"]:
					for normal:Vector3 in [Vector3.UP,Vector3.DOWN,Vector3.LEFT,Vector3.RIGHT,Vector3.FORWARD,Vector3.BACK]:
						var data:Dictionary={"state":state,"p":Vector3(2,1,3),"yaw":.8,"pitch":.25,"velocity":Vector3(.7,.2,-1.0),"surface_normal":normal}
						actor.update_state(data,1.0)
						var actual:Basis=actor.model.global_basis.orthonormalized()
						check(actual.x.distance_to(Mosquito.orientation(data).x)<.0001,"shared insect orientation "+state)
						check(absf(actor.model.global_basis.x.length()-.35*scale_factor)<.0001,"editor scale preserved "+state)
			check(actor.body_shapes.size()==3,"three anatomical impact ray shapes")
		actor.queue_free();await process_frame
	print("SELECTED07_ACTOR_RESULT checks=%d failures=%d"%[checks,failures]);quit(failures)
