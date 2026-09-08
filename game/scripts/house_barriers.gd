class_name HouseBarriers
extends RefCounted
## Open balustrades shared by authority, navigation and rendering.
## Only individual wooden members collide; air between balusters stays open.
static var _parts: Array[Dictionary] = []
static var _boxes: Array[AABB] = []

static func get_parts(map_id: String = "house") -> Array[Dictionary]:
	if map_id!="house":return []
	_build()
	return _parts.duplicate(true)

static func get_boxes(map_id: String = "house") -> Array[AABB]:
	if map_id!="house":return []
	_build()
	return _boxes.duplicate()

static func _build() -> void:
	if not _parts.is_empty():return
	for side:float in [-1.0,1.0]:
		var center_x:=side*11.0
		# Stair handrails are supported every metre, outside the 2.8m tread.
		for edge:float in [-1.0,1.0]:
			var x:=center_x+edge*1.48
			for i:int in range(9):
				var z:float=-4.0+i
				var y:float=(z+4.0)*.4 if side<0 else (4.0-z)*.4
				_post(Vector3(x,y,z),.96)
			var a:=Vector3(x,.99 if side<0 else 4.19,-4)
			var b:=Vector3(x,4.19 if side<0 else .99,4)
			_parts.append({"kind":"slope","from":a,"to":b})
			# Fine boxes track only the sloping wooden rail, within 2.5cm of its
			# top/bottom envelope. They never fill the opening under the rail.
			for i:int in range(64):
				var start:=a.lerp(b,float(i)/64)
				var end:=a.lerp(b,float(i+1)/64)
				var p:=Vector3(x-.0475,minf(start.y,end.y)-.0425,start.z)
				_boxes.append(AABB(p,Vector3(.095,absf(end.y-start.y)+.085,end.z-start.z)))
		# Guard both long sides of each upstairs stair opening. Ends are the
		# actual landings and remain open; rails do not cross them.
		for edge:float in [-1.0,1.0]:
			var x:=center_x+edge*1.60
			for i:int in range(9):_post(Vector3(x,3.2,-4.0+i),.96)
			_bar(AABB(Vector3(x-.0475,4.1475,-4),Vector3(.095,.085,8)))
			_bar(AABB(Vector3(x-.0225,3.27,-4),Vector3(.045,.055,8)))

static func _post(p:Vector3,height:float) -> void:
	var box:=AABB(p+Vector3(-.035,0,-.035),Vector3(.07,height,.07))
	_boxes.append(box);_parts.append({"kind":"post","box":box})

static func _bar(box:AABB) -> void:
	_boxes.append(box);_parts.append({"kind":"bar","box":box})
