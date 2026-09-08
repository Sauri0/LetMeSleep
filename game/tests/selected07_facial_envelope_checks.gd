extends SceneTree
## Blend shapes must be baked separately: skeleton baking ignores morphs in 4.5.
## https://docs.godotengine.org/en/4.5/classes/class_meshinstance3d.html
const Character=preload("res://assets/art/characters/shared/character_skin.gd")
const Mosquito=preload("res://scripts/mosquito_pose.gd")
const Pose=preload("res://scripts/human_pose.gd")
var checks:=0
var failures:=0
var maximum_gap:=0.0
func _initialize()->void:_run.call_deferred()
func _run()->void:
	for role:String in ["human","mosquito"]:
		var skin:=Character.new();root.add_child(skin);skin.setup(role)
		skin.scale=Vector3.ONE*(.35 if role=="mosquito" else 1.0)
		for face:int in range(3):
			skin.set_appearance({"face":face})
			for expression:String in ["sleepy","alert","effort","impact"]:
				var data:Dictionary={"state":"human" if role=="human" else "flying","preview_only":true,"facial_preview":expression,"facial_no_blink":true}
				for frame:int in range(60):
					if role=="human":skin.apply_human(Pose.sample(data),data,1.0/30.0)
					else:skin.apply_mosquito(data,skin.time+1.0/30.0,0.0)
				await process_frame
				for mesh:MeshInstance3D in skin.face_channels:
					if not mesh.visible:continue
					var baked:ArrayMesh=mesh.bake_mesh_from_current_blend_shape_mix()
					for surface:int in range(baked.get_surface_count()):
						var points:PackedVector3Array=baked.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
						for vertex:Vector3 in points:
							var point:Vector3=mesh.global_transform*vertex
							var gap:=INF
							if role=="human":gap=point.distance_to(Vector3(0,1.55,0))-.235
							else:
								for capsule:Dictionary in Mosquito.local_segments():gap=minf(gap,point.distance_to(Mosquito.closest_axis(point,capsule.from,capsule.to))-float(capsule.radius))
							checks+=1;maximum_gap=maxf(maximum_gap,gap)
							if gap>.002:
								failures+=1
								if failures<=20:print("FACIAL_ENVELOPE_OUTSIDE ",role," face=",face," state=",expression," p=",point," gap=",gap)
		skin.queue_free();await process_frame
	print("SELECTED07_FACIAL_ENVELOPE checks=%d failures=%d max_gap=%.6f"%[checks,failures,maximum_gap]);quit(1 if failures>0 else 0)
