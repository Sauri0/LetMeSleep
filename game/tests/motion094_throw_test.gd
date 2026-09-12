extends SceneTree
const Maps = preload("res://scripts/map_catalog.gd")
const Projectiles = preload("res://scripts/projectile_collision.gd")
const Doors = preload("res://scripts/door_catalog.gd")
var checks := 0
var failures := 0

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("FAIL: " + label)

func _initialize() -> void:
	var map_id := Maps.default_map_id()
	var data := Maps.get_map(map_id)
	var open_doors: Dictionary = {}
	for id: String in data.doors:
		open_doors[id] = {"angle":Doors.OPEN_ANGLE}
	for tool: String in ["newspaper","slipper"]:
		for portal: Dictionary in data.portals:
			var id: String = portal.id
			var normal := Vector3.ZERO
			normal[int(portal.axis)] = 1
			var center: Vector3 = Vector3(portal.p)+Vector3.UP*1.2
			var closed := open_doors.duplicate(true)
			closed[id].angle = 0.0
			for side: float in [-1.0,1.0]:
				var direction: Vector3 = normal*side
				var start: Vector3 = center-direction*.85
				var shape := Projectiles.capsule(tool,start,Projectiles.launch_basis(direction).get_euler())
				var hit := Projectiles.map_hit(shape,direction*1.7,map_id,closed)
				check(hit.get("kind","")=="door" and float(hit.get("fraction",1))>0 and float(hit.get("fraction",1))<.5, "%s closed leaf stops sweep from side %s %s" % [tool,side,id])
				if not hit.is_empty():
					check(Vector3(hit.normal).dot(direction)<-.99 and hit.material=="wood", "door impact world normal and material " + id)
				check(Projectiles.map_hit(shape,direction*1.7,map_id,open_doors).is_empty(), "%s open doorway allows free travel %s" % [tool,id])
				check(Projectiles.map_hit(shape,direction*.25,map_id,closed).is_empty(), "%s short throw stops before closed leaf %s" % [tool,id])
			for angle: float in [PI*.25,Doors.OPEN_ANGLE]:
				var definition: Dictionary = data.doors[id]
				var transform := Doors.leaf_transform(definition,angle)
				var leaf_center: Vector3 = transform*Vector3(float(definition.width)*.5,1.2,0)
				var direction: Vector3 = transform.basis.z
				# Keep the full capsule clear of adjacent corridor walls at release.
				var shape := Projectiles.capsule(tool,leaf_center-direction*.5,Projectiles.launch_basis(direction).get_euler())
				var states := open_doors.duplicate(true)
				states[id].angle = angle
				var hit := Projectiles.map_hit(shape,direction,map_id,states)
				check(hit.get("kind","")=="door", "%s angled/open leaf remains physical angle=%s %s" % [tool,angle,id])
				if hit.get("kind","")=="door":
					check(Vector3(hit.normal).dot(direction)<-.99, "oriented leaf returns rotated world normal " + id)
		var roof_shape := Projectiles.capsule(tool,Vector3(0,7.4,0),Vector3.ZERO)
		check(Projectiles.map_hit(roof_shape,Vector3.DOWN*7,map_id,open_doors).get("kind","")=="map", "nearer roof wins over doors below " + tool)
		var free_shape := Projectiles.capsule(tool,Vector3(0,7,15),Vector3.ZERO)
		check(Projectiles.map_hit(free_shape,Vector3.RIGHT*2,map_id,open_doors).is_empty(), "clear patio throw has no building ceiling collision " + tool)
	print("MOTION094_THROW checks=%d failures=%d" % [checks,failures])
	quit(0 if failures == 0 else 1)
