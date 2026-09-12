extends RefCounted
## Shared dimensions for exterior rendering and authoritative collision.
const Assets = preload("res://assets/art/house/alfa_library.gd")

static func plan(building: AABB, lot: AABB) -> Dictionary:
	var eave := building.end.y + .24
	var rise := minf(2.0, lot.end.y - eave - .2)
	var run := building.size.x * .5 + .55
	var ridge_x := building.get_center().x
	var result := {"eave":eave,"rise":rise,"run":run,"ridge":eave+rise,
		"depth":building.size.z+1.1,"props":[],"collision_boxes":[]}
	# The attic is sealed. Narrow columns approximate the sloped roof envelope
	# within 2 cm vertically, including the overhang, without a giant box above it.
	var count := ceili(run * 2.0 / .2)
	var width := run * 2.0 / count
	for index: int in range(count):
		var x := ridge_x-run+index*width
		var top := eave+rise*(1.0-absf(x+width*.5-ridge_x)/run)+.081
		var bottom := building.end.y if x+width>building.position.x and x<building.end.x else eave-.081
		result.collision_boxes.append(AABB(Vector3(x,bottom,building.position.z-.55),Vector3(width,top-bottom,result.depth)))
	for z: float in [.02,.62]:
		for x: float in [-1.6,0.0,1.6]:
			result.props.append({"asset_id":"alfa_canopy","p":Vector3(ridge_x+x,2.62,building.position.z-z)})
	for x: float in [-2.28,2.28]:
		result.props.append({"asset_id":"alfa_porch_post","p":Vector3(ridge_x+x,0,building.position.z-1.14)})
	var chimney_x := building.position.x+building.size.x*.72
	var chimney_y := minf(eave+rise*(1.0-absf(chimney_x-ridge_x)/run)-.1,lot.end.y-Assets.bounds("alfa_chimney").end.y-.04)
	result.props.append({"asset_id":"alfa_chimney","p":Vector3(chimney_x,chimney_y,building.get_center().z+1.2)})
	for prop: Dictionary in result.props:
		for local_box: AABB in Assets.collision_boxes(prop.asset_id):
			result.collision_boxes.append(AABB(local_box.position+Vector3(prop.p),local_box.size))
	return result
