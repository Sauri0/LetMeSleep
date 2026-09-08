extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
const Geometry=preload("res://scripts/navigation_geometry.gd")
func _initialize() -> void:
	var data: Dictionary=Generator.new().generate(77097386)
	var report: Dictionary=Validation.validate(data)
	print("REPORT "+str(report.errors))
	for edge: Dictionary in report.rejected_edges:
		var a: Vector3=edge.from
		var b: Vector3=edge.to
		print("EDGE "+str(edge))
		for entry: Dictionary in data.structures:
			if AABB(entry.box).grow(.61).intersects_segment(a+Vector3.UP*.9,b+Vector3.UP*.9)!=null: print("BLOCK "+str(entry))
	quit()
