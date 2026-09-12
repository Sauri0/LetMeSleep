class_name MosquitoCamera
extends RefCounted
const Pose=preload("res://scripts/mosquito_pose.gd")
const DEFAULT_REACH := .85
const MAX_REACH := 2.5
const ZOOM_STEP := .16

static func zoom_reach(current: float, steps: float) -> float:
	return clampf(current+steps*ZOOM_STEP,0.0,MAX_REACH)

static func smooth_reach(current: float, desired: float, dt: float) -> float:
	var bounded := clampf(desired,0.0,MAX_REACH)
	var result := lerpf(current,bounded,1.0-exp(-14.0*maxf(dt,0.0)))
	return bounded if absf(result-bounded)<.001 else result

static func target(actor: Dictionary, yaw: float, pitch: float, reach: float=DEFAULT_REACH) -> Dictionary:
	var third_person := clampf(reach/.35,0.0,1.0)
	var normal: Vector3=actor.get("surface_normal",Vector3.ZERO)
	var forward: Vector3=actor.get("surface_forward",Vector3.ZERO)
	if str(actor.get("state",""))=="perched" and normal.length_squared()>.5 and forward.length_squared()>.5:
		normal=normal.normalized()
		# Account for input not yet acknowledged by the authority without
		# rebuilding forward from world yaw (which flips W on ceilings).
		forward=forward.rotated(normal,wrapf(yaw-float(actor.get("yaw",yaw)),-PI,PI))
		var basis:=Pose.surface_basis(normal,forward)*Basis(Vector3.RIGHT,clampf(pitch,-1.35,1.35))
		return {"basis":basis,"offset":normal*lerpf(.015,.14,third_person)+forward*lerpf(.04,.05,third_person),"surface":true}
	var basis:=Basis.from_euler(Vector3(pitch,yaw,0))
	return {"basis":basis,"offset":Vector3.UP*lerpf(.014,.12,third_person)-basis.z*lerpf(.04,.10,third_person),"surface":false}

static func smooth_basis(current: Basis, desired: Basis, dt: float) -> Basis:
	return Basis(current.get_rotation_quaternion().slerp(desired.get_rotation_quaternion(),1.0-exp(-12.0*maxf(0.0,dt))))

static func resolve(space: PhysicsDirectSpaceState3D, actor: Dictionary, requested_origin: Vector3, basis: Basis, shape: Shape3D, reach: float) -> Dictionary:
	var query:=PhysicsShapeQueryParameters3D.new()
	query.shape=shape;query.collision_mask=1;query.collide_with_areas=false;query.margin=.002
	var anchor: Vector3=actor.p
	var normal: Vector3=actor.get("surface_normal",Vector3.ZERO)
	if str(actor.get("state",""))=="perched" and normal.length_squared()>.5: anchor+=normal.normalized()*.07
	# The locomotion sphere is 4cm and the camera guard is 4.5cm. Move its
	# anchor out of contact by a few millimetres before sweeping; cast_motion
	# otherwise ignores an object overlapping at the start of the sweep.
	for iteration: int in range(4):
		query.transform=Transform3D(Basis.IDENTITY,anchor)
		var rest: Dictionary=space.get_rest_info(query)
		if rest.is_empty(): break
		anchor+=Vector3(rest.normal)*.025
	query.transform=Transform3D(Basis.IDENTITY,anchor)
	query.motion=requested_origin-anchor
	var sweep: PackedFloat32Array=space.cast_motion(query)
	if not sweep.is_empty(): anchor+=query.motion*maxf(0.0,sweep[0]-.012/maxf(query.motion.length(),.012))
	query.transform=Transform3D(Basis.IDENTITY,anchor)
	query.motion=basis.z*reach
	sweep=space.cast_motion(query)
	var distance:=maxf(0.0,reach*sweep[0]-.012) if not sweep.is_empty() else 0.0
	return {"origin":anchor,"distance":distance}
