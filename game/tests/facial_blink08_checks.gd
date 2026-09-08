extends SceneTree
## Imported vertices, not node visibility: Blink preserves the eye and sweeps lids.
const Character = preload("res://assets/art/characters/shared/character_skin.gd")
var checks := 0
var failures := 0
var maximum_ocular_delta := 0.0
var minimum_lid_motion := INF
var rows: Array = []
const RAY_NUMERIC_SCALE := 100.0

func _initialize() -> void: _run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		if failures <= 20: print("FACIAL_BLINK08_FAIL ",label)

func vertices_by_material(mesh: MeshInstance3D) -> Dictionary:
	var baked := mesh.bake_mesh_from_current_blend_shape_mix()
	var result: Dictionary = {}
	for surface: int in range(baked.get_surface_count()):
		var material := mesh.mesh.surface_get_material(surface).resource_name
		var points: PackedVector3Array = baked.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
		if not result.has(material): result[material] = PackedVector3Array()
		result[material].append_array(points)
	return result

func closure_coverage(mesh: MeshInstance3D) -> Dictionary:
	var baked := mesh.bake_mesh_from_current_blend_shape_mix()
	var debug_surfaces: Array=[]
	var lid_faces := PackedVector3Array()
	var samples: Array = []
	for surface: int in range(baked.get_surface_count()):
		var material := mesh.mesh.surface_get_material(surface).resource_name
		var arrays := baked.surface_get_arrays(surface)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		debug_surfaces.append({"material":material,"verts":vertices.size(),"indices":indices.size(),"first":str(vertices[0]),"last":str(vertices[-1]),"primitive":baked.surface_get_primitive_type(surface)})
		var count := indices.size() if not indices.is_empty() else vertices.size()
		for triangle: int in range(0,count,3):
			var ids: Array[int] = []
			for corner: int in range(3): ids.append(indices[triangle+corner] if not indices.is_empty() else triangle+corner)
			var a := vertices[ids[0]];var b := vertices[ids[1]];var c := vertices[ids[2]]
			if material in ["skin","insect_primary"]:
				# Render indices are clockwise. The physics triangle test uses
				# counter-clockwise outward faces; preserve the same positions.
				lid_faces.append_array(PackedVector3Array([a,c,b]))
			elif material in ["eye_white","pupil"]:
				var normal := (normals[ids[0]]+normals[ids[1]]+normals[ids[2]]).normalized()
				# Vertices, edge interiors and face interior of the imported index
				# set are all checked; no reliance on authoring vertex bounds.
				for point: Vector3 in [a,b,c,(a+b)*.5,(b+c)*.5,(c+a)*.5,(a+b+c)/3.0]:
					samples.append([mesh.global_transform*point,(mesh.global_basis*normal).normalized(),material])
	var body := StaticBody3D.new();root.add_child(body)
	if str(mesh.name)=="human_eyes_0":
		var debug_points: Array=[]
		for p: Vector3 in lid_faces:debug_points.append([p.x,p.y,p.z])
		var file := FileAccess.open(ProjectSettings.globalize_path("res://../work/blink08-lid-debug.json"),FileAccess.WRITE)
		file.store_string(JSON.stringify({"faces":debug_points}));file.close()
	# Godot's absolute ray/triangle epsilon rejects sub-millimetre triangles.
	# A uniform scale preserves all intersections and gives a stable numeric
	# range. This isolated test collider never becomes gameplay geometry.
	body.global_transform=Transform3D(mesh.global_basis.scaled(Vector3.ONE*RAY_NUMERIC_SCALE),mesh.global_position*RAY_NUMERIC_SCALE)
	var shape_node := CollisionShape3D.new();body.add_child(shape_node)
	var shape := ConcavePolygonShape3D.new();shape.backface_collision=true;shape.set_faces(lid_faces);shape_node.shape=shape
	await physics_frame
	await physics_frame
	var space := body.get_world_3d().direct_space_state
	var rays := 0;var uncovered := 0;var witnesses: Array = []
	for sample: Array in samples:
		for raw: Vector3 in [Vector3(0,0,-1),Vector3(1,0,-1),Vector3(-1,0,-1),Vector3(0,1,-1),Vector3(0,-1,-1),Vector3(1,1,-1),Vector3(-1,-1,-1)]:
			var direction := raw.normalized()
			if Vector3(sample[1]).dot(direction)<.05: continue
			var point: Vector3=Vector3(sample[0])*RAY_NUMERIC_SCALE
			var start := point+direction*.25*RAY_NUMERIC_SCALE
			var end := point-direction*.00001*RAY_NUMERIC_SCALE
			var query := PhysicsRayQueryParameters3D.create(start,end)
			query.hit_back_faces=true
			query.hit_from_inside=true
			var hit := space.intersect_ray(query)
			rays+=1
			if hit.is_empty() or Vector3(hit.position).distance_to(start)>.24999*RAY_NUMERIC_SCALE:
				uncovered+=1
				if witnesses.size()<8:
					var exact_distance := INF
					for triangle: int in range(0,lid_faces.size(),3):
						var exact: Variant=Geometry3D.segment_intersects_triangle(start,end,(mesh.global_transform*lid_faces[triangle])*RAY_NUMERIC_SCALE,(mesh.global_transform*lid_faces[triangle+1])*RAY_NUMERIC_SCALE,(mesh.global_transform*lid_faces[triangle+2])*RAY_NUMERIC_SCALE)
						if exact!=null:exact_distance=minf(exact_distance,Vector3(exact).distance_to(start)/RAY_NUMERIC_SCALE)
					witnesses.append({"point":str(point/RAY_NUMERIC_SCALE),"direction":str(direction),"material":sample[2],"exact_distance":exact_distance if is_finite(exact_distance) else -1.0,"physics_hit":str(hit)})
	body.queue_free();await physics_frame
	var weights: Dictionary={}
	for key: int in range(mesh.mesh.get_blend_shape_count()):weights[str(mesh.mesh.get_blend_shape_name(key))]=mesh.get_blend_shape_value(key)
	return {"rays":rays,"uncovered":uncovered,"witnesses":witnesses,"sampling":"vertices, 3 edge midpoints and triangle centroid; 7 view directions", "debug_surfaces":debug_surfaces,"debug_weights":weights,"debug_transform":str(mesh.global_transform),"debug_mode":mesh.mesh.blend_shape_mode}

func _run() -> void:
	var camera := Camera3D.new();root.add_child(camera);camera.current=true
	for role: String in ["human","mosquito"]:
		var skin := Character.new();root.add_child(skin);skin.setup(role)
		var head := skin.skeleton.to_global(skin.skeleton.get_bone_global_pose(skin.bone_ids.head).origin)
		camera.global_position=head+Vector3(0,0,.8)
		var body_lods: Dictionary = {}
		for mesh: MeshInstance3D in skin.meshes:
			if not skin.face_channels.has(mesh): body_lods[mesh]=mesh.lod_bias
		skin.apply_facial_values({"BlinkL":1.0,"BlinkR":1.0})
		for mesh: MeshInstance3D in skin.face_channels:check(mesh.lod_bias==Character.FACIAL_NEAR_LOD_BIAS,"reading distance keeps authored facial index set")
		camera.global_position=head+Vector3(0,0,100)
		skin.apply_facial_values({})
		for mesh: MeshInstance3D in skin.face_channels:check(mesh.lod_bias==1.0,"distant facial LOD remains available")
		camera.global_position=head+Vector3(0,0,.8)
		skin.apply_facial_values({"BlinkL":.5,"BlinkR":.5})
		for mesh: MeshInstance3D in skin.face_channels:check(mesh.lod_bias==Character.FACIAL_NEAR_LOD_BIAS,"approach restores detail without waiting for a blink timer")
		for mesh: MeshInstance3D in body_lods:check(mesh.lod_bias==body_lods[mesh],"body and hair LOD unchanged")
		for option: int in range(3):
			skin.set_appearance({"eyes":option,"brows":option,"mouth":option,"accessory":0})
			var eye: MeshInstance3D
			for mesh: MeshInstance3D in skin.face_channels:
				if mesh.visible and str(mesh.name).contains("_eyes_"): eye = mesh
			check(eye!=null,role+" selected eye "+str(option))
			skin.apply_facial_values({})
			await process_frame
			await RenderingServer.frame_post_draw
			var neutral := vertices_by_material(eye)
			var lid_material := "skin" if role=="human" else "insect_primary"
			check(neutral.has(lid_material),role+" real lid surface")
			var option_motion := 0.0
			for step: int in range(17):
				var closure := float(step)/16.0
				skin.apply_facial_values({"BlinkL":closure,"BlinkR":closure})
				await process_frame
				await RenderingServer.frame_post_draw
				var posed := vertices_by_material(eye)
				for material: String in ["eye_white","pupil"]:
					check(posed[material].size()==neutral[material].size(),"ocular topology unchanged")
					var max_delta := 0.0
					for index: int in range(posed[material].size()):
						max_delta=maxf(max_delta,posed[material][index].distance_to(neutral[material][index]))
					maximum_ocular_delta=maxf(maximum_ocular_delta,max_delta)
					check(max_delta<.000002,role+" "+str(option)+" "+material+" keeps every vertex at Blink "+str(closure))
				var motion := 0.0
				for index: int in range(posed[lid_material].size()):
					motion=maxf(motion,posed[lid_material][index].distance_to(neutral[lid_material][index]))
				if step==8 or step==16: check(motion>.006,"half/full closure moves a real curved lid "+role)
				option_motion=maxf(option_motion,motion)
				for blink: String in ["BlinkL","BlinkR"]:
					for knot: int in range(1,8):
						var key := blink+"Arc"+str(knot)
						check(skin.face_channels[eye].has(key),"import retains arc corrective "+key)
						var expected := maxf(0.0,1.0-absf(closure*8.0-float(knot)))
						check(absf(eye.get_blend_shape_value(skin.face_channels[eye][key])-expected)<.0001,"common driver evaluates exact curve interval")
			minimum_lid_motion=minf(minimum_lid_motion,option_motion)
			var coverage: Dictionary = await closure_coverage(eye)
			check(coverage.uncovered==0,role+" "+str(option)+" imported closed lids cover ocular triangle interiors: "+str(coverage.uncovered))
			rows.append({"role":role,"eyes":option,"samples":17,"maximum_lid_motion_authoring_m":option_motion,"closure_coverage":coverage,
				"reading_lod_bias":eye.lod_bias})
			skin.apply_facial_values({"GazeX":.5})
			await process_frame
			await RenderingServer.frame_post_draw
			var gaze := vertices_by_material(eye)
			var gaze_motion := 0.0
			for index: int in range(gaze.pupil.size()): gaze_motion=maxf(gaze_motion,gaze.pupil[index].distance_to(neutral.pupil[index]))
			check(gaze_motion>.002,"pupil gaze remains functional "+role)
		skin.queue_free();await process_frame
	var report := {"checks":checks,"failures":failures,"maximum_ocular_blink_delta_m":maximum_ocular_delta,"cases":rows,
		"scope":"Imported ocular vertex invariance, closure ray coverage including triangle interiors, shared intervals and scale-aware facial LOD policy; visual review remains separate."}
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="):
			var file := FileAccess.open(arg.trim_prefix("--output="),FileAccess.WRITE)
			file.store_string(JSON.stringify(report,"\t"));file.close()
	print("FACIAL_BLINK08_RESULT checks=%d failures=%d ocular_delta=%.8f lid_motion=%.6f"%[checks,failures,maximum_ocular_delta,minimum_lid_motion])
	quit(1 if failures>0 else 0)
