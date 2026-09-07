extends RefCounted
## Original sculpted furniture built in the unchanged catalog collision envelope.
const Shape = preload("res://assets/procedural_shapes.gd")
const Model = preload("res://scripts/actor_view.gd")

static func build(root: Node3D, data: Dictionary, materials: Dictionary) -> void:
	var size: Vector3 = data.box.size
	var label: String = str(data.get("label",""))
	var style: String = str(data.get("style","cabinet"))
	var at: Vector3 = data.box.get_center()
	# Existing AABB dimensions and position are untouched. Rotate the entire
	# furnishing 180 degrees inside its envelope to face the traversable room.
	var room_center: Vector3 = data.get("room_center",Vector3.ZERO)
	var face_positive_z: bool = at.z < room_center.z
	if size.z>size.x*1.25:
		root.rotation.y = -PI/2.0 if room_center.x>at.x else PI/2.0
		root.position += Vector3(0,0,size.z) if room_center.x>at.x else Vector3(size.x,0,0)
		size = Vector3(size.z,size.y,size.x)
	elif face_positive_z:
		root.position += Vector3(size.x,0,size.z)
		root.rotation.y = PI
	if label == "Mesada":
		_stove(root,size,materials)
	elif label == "Alacena":
		_sink(root,size,materials)
	elif label == "Despensa" and at.z < -5.9:
		_fridge(root,size,materials)
	elif style == "bed":
		_bed(root,size,materials)
	elif style == "sofa":
		_sofa(root,size,materials)
	elif label in ["Biblioteca","Estantes"]:
		_bookcase(root,size,materials)
	elif style in ["table","desk"]:
		_table(root,size,materials)
	else:
		_storage(root,size,materials,label=="Ropero")

static func _round(root: Node3D, point: Vector3, size: Vector3, material: Material, radius: float = 0.035) -> MeshInstance3D:
	return Shape.rounded(root,point,size,radius,material)

static func _legs(root: Node3D, size: Vector3, height: float, material: Material) -> void:
	for x: float in [0.09,size.x-0.09]:
		for z: float in [0.09,size.z-0.09]:
			Model._segment(root,Vector3(x,0.025,z),Vector3(x,height,z),0.04,material)

static func _bed(root: Node3D, s: Vector3, m: Dictionary) -> void:
	_legs(root,s,0.16,m.dark)
	_round(root,Vector3(s.x*0.5,0.19,s.z*0.5),Vector3(s.x,0.22,s.z),m.timber,0.07)
	_round(root,Vector3(s.x*0.5,0.345,s.z*0.5),Vector3(s.x*0.96,0.22,s.z*0.95),m.linen,0.095)
	# Headboard follows the short end of the bed, instead of a sofa-like rail.
	_round(root,Vector3(0.075,s.y*0.57,s.z*0.5),Vector3(0.14,s.y*0.85,s.z),m.timber,0.065)
	_round(root,Vector3(0.16,s.y*0.68,s.z*0.5),Vector3(0.045,s.y*0.4,s.z*0.84),m.accent,0.02)
	_round(root,Vector3(s.x*0.64,0.47,s.z*0.5),Vector3(s.x*0.64,0.20,s.z*0.92),m.cloth,0.095)
	_round(root,Vector3(s.x*0.34,0.56,s.z*0.5),Vector3(0.15,0.11,s.z*0.91),m.accent,0.045)
	for side: float in [0.28,0.72]:
		var pillow: MeshInstance3D = _round(root,Vector3(s.x*0.2,0.53,s.z*side),Vector3(s.x*0.22,0.21,s.z*0.36),m.linen,0.09)
		pillow.rotation.x = -0.05 if side<0.5 else 0.05
	for line: int in range(3):
		Model._segment(root,Vector3(s.x*0.42,0.572,s.z*(0.28+line*0.22)),Vector3(s.x*0.91,0.572,s.z*(0.28+line*0.22)),0.007,m.stitch)

static func _sofa(root: Node3D, s: Vector3, m: Dictionary) -> void:
	_legs(root,s,0.15,m.dark)
	_round(root,Vector3(s.x*0.5,s.y*0.26,s.z*0.5),Vector3(s.x*0.96,s.y*0.30,s.z*0.94),m.cloth,0.10)
	var count: int = 3 if s.x>2.0 else 2
	var seat_width: float = (s.x-0.30)/count
	for i: int in range(count):
		var x: float = 0.15+(i+0.5)*seat_width
		_round(root,Vector3(x,s.y*0.48,s.z*0.40),Vector3(seat_width-0.018,s.y*0.25,s.z*0.66),m.cloth,0.095)
		var back: MeshInstance3D = _round(root,Vector3(x,s.y*0.74,s.z*0.78),Vector3(seat_width-0.025,s.y*0.48,s.z*0.29),m.accent,0.10)
		back.rotation.x = -0.06
		Model._sphere(root,Vector3(x,s.y*0.76,s.z*0.62),Vector3(0.026,0.026,0.012),m.cloth)
	for x: float in [0.085,s.x-0.085]:
		_round(root,Vector3(x,s.y*0.51,s.z*0.45),Vector3(0.17,s.y*0.64,s.z*0.83),m.cloth,0.08)
	var pillow: MeshInstance3D = _round(root,Vector3(s.x*0.26,s.y*0.70,s.z*0.49),Vector3(minf(0.3,s.x*0.24),s.y*0.3,0.14),m.linen,0.065)
	pillow.rotation.z = 0.18

static func _table(root: Node3D, s: Vector3, m: Dictionary) -> void:
	_legs(root,s,s.y-0.08,m.dark)
	# A recessed lower shelf visually declares the same occupied footprint.
	_round(root,Vector3(s.x*0.5,0.16,s.z*0.5),Vector3(s.x*0.86,0.12,s.z*0.80),m.timber,0.05)
	_round(root,Vector3(s.x*0.5,s.y-0.09,s.z*0.5),Vector3(s.x,0.15,s.z),m.timber,minf(s.z*0.25,0.12))
	_round(root,Vector3(s.x*0.5,s.y-0.012,s.z*0.5),Vector3(s.x*0.9,0.018,s.z*0.84),m.accent,0.008)
	for i: int in range(3):
		_round(root,Vector3(s.x*0.33+i*0.09,0.27+i*0.037,s.z*0.49),Vector3(s.x*0.27,0.04,s.z*0.42),m.linen if i==1 else m.cloth,0.008)

static func _storage(root: Node3D, s: Vector3, m: Dictionary, wardrobe: bool = false) -> void:
	_round(root,Vector3(s.x*0.5,s.y*0.5,(s.z+0.075)*0.5),Vector3(s.x,s.y,s.z-0.075),m.timber,0.055)
	_round(root,Vector3(s.x*0.5,s.y*0.5,0.05),Vector3(s.x*0.92,s.y*0.89,0.045),m.dark,0.025)
	var count: int = 2 if wardrobe or s.x>s.y else 3
	for i: int in range(count):
		var width: float = s.x*0.85/count if wardrobe else s.x*0.85
		var height: float = s.y*0.82 if wardrobe else s.y*0.82/count
		var x: float = s.x*(0.075+(i+0.5)*0.85/count) if wardrobe else s.x*0.5
		var y: float = s.y*0.5 if wardrobe else s.y*(0.09+(i+0.5)*0.82/count)
		_round(root,Vector3(x,y,0.02),Vector3(width-0.025,height-0.023,0.025),m.accent,0.012)
		var handle_x: float = s.x*0.5+(-0.045 if i==0 else 0.045) if wardrobe else x
		Model._segment(root,Vector3(handle_x-0.05,y,0.012),Vector3(handle_x+0.05,y,0.012),0.012,m.metal)
	_round(root,Vector3(s.x*0.5,0.028,s.z*0.5),Vector3(s.x*0.92,0.055,s.z*0.91),m.dark,0.018)

static func _bookcase(root: Node3D, s: Vector3, m: Dictionary) -> void:
	_round(root,Vector3(s.x*0.5,s.y*0.5,s.z*0.91),Vector3(s.x,s.y,s.z*0.18),m.timber,0.035)
	for x: float in [0.04,s.x-0.04]:
		_round(root,Vector3(x,s.y*0.5,s.z*0.5),Vector3(0.08,s.y,s.z),m.timber,0.025)
	for shelf: int in range(4):
		var y: float = 0.06+shelf*(s.y-0.12)/3.0
		_round(root,Vector3(s.x*0.5,y,s.z*0.5),Vector3(s.x,0.06,s.z),m.timber,0.02)
		if shelf==3:
			continue
		for book: int in range(4):
			var h: float = (s.y-0.12)/3.0*(0.58+0.09*(book%3))
			var x: float = 0.10+book*(s.x-0.16)/4.0
			_round(root,Vector3(x,y+0.04+h*0.5,s.z*0.38),Vector3((s.x-0.2)/5.0,h,s.z*0.48),m.cloth if book%2==0 else m.linen,0.008)

static func _stove(root: Node3D, s: Vector3, m: Dictionary) -> void:
	var full_size: Vector3 = s
	s = Vector3(minf(0.87,s.x),s.y*0.92,s.z)
	var counter := Node3D.new()
	counter.position.x = s.x+0.035
	root.add_child(counter)
	var counter_size := Vector3(full_size.x-s.x-0.035,s.y,full_size.z)
	_storage(counter,Vector3(counter_size.x,counter_size.y-0.035,counter_size.z),m,true)
	_round(counter,Vector3(counter_size.x*0.5,counter_size.y-0.018,counter_size.z*0.5),Vector3(counter_size.x,0.035,counter_size.z),m.enamel,0.015)
	# A folded tea towel and a small covered dish occupy the spare work surface.
	_round(counter,Vector3(counter_size.x*0.5,s.y+0.025,counter_size.z*0.54),Vector3(counter_size.x*0.55,0.044,counter_size.z*0.45),m.cloth,0.017)
	Model._sphere(counter,Vector3(counter_size.x*0.74,s.y+0.036,counter_size.z*0.58),Vector3(0.09,0.034,0.09),m.linen)
	_round(root,Vector3(s.x*0.5,s.y*0.5,(s.z+0.065)*0.5),Vector3(s.x,s.y,s.z-0.065),m.enamel,0.06)
	_round(root,Vector3(s.x*0.5,s.y*0.44,0.034),Vector3(s.x*0.83,s.y*0.61,0.04),m.dark,0.065)
	_round(root,Vector3(s.x*0.5,s.y*0.44,0.008),Vector3(s.x*0.65,s.y*0.39,0.012),m.glass,0.005)
	Model._segment(root,Vector3(s.x*0.25,s.y*0.71,0.012),Vector3(s.x*0.75,s.y*0.71,0.012),0.022,m.metal)
	for i: int in range(4):
		var x: float = s.x*(0.18+i*0.21)
		Model._sphere(root,Vector3(x,s.y*0.88,0.025),Vector3(0.038,0.038,0.021),m.dark)
	for x: float in [s.x*0.27,s.x*0.73]:
		for z: float in [s.z*0.26,s.z*0.72]:
			var burner := TorusMesh.new()
			burner.inner_radius = 0.074
			burner.outer_radius = 0.10
			burner.rings = 16
			burner.ring_segments = 6
			Model.mesh(root,burner,Vector3(x,s.y-0.01,z),m.dark)
			Model._segment(root,Vector3(x-0.11,s.y-0.009,z),Vector3(x+0.11,s.y-0.009,z),0.007,m.dark)
			Model._segment(root,Vector3(x,s.y-0.006,z-0.10),Vector3(x,s.y-0.006,z+0.10),0.009,m.dark)
			Model._sphere(root,Vector3(x,s.y-0.004,z),Vector3(0.051,0.009,0.051),m.dark)

static func _sink(root: Node3D, s: Vector3, m: Dictionary) -> void:
	_storage(root,Vector3(s.x,s.y-0.20,s.z),m,true)
	_round(root,Vector3(s.x*0.5,s.y-0.195,s.z*0.5),Vector3(s.x,0.08,s.z),m.enamel,0.035)
	_round(root,Vector3(s.x*0.38,s.y-0.15,s.z*0.48),Vector3(s.x*0.47,0.035,s.z*0.65),m.metal,0.017)
	_round(root,Vector3(s.x*0.38,s.y-0.127,s.z*0.48),Vector3(s.x*0.40,0.015,s.z*0.50),m.glass,0.007)
	# Faucet is a recognisable bent neck, capped below the collision top.
	var x: float = s.x*0.38
	var previous := Vector3(x,s.y-0.08,s.z*0.81)
	Model._segment(root,Vector3(x,s.y-0.18,s.z*0.81),previous,0.018,m.metal)
	for index: int in range(1,9):
		var angle: float = PI*index/8.0
		var point := Vector3(x,s.y-0.08+sin(angle)*0.062,s.z*0.66+cos(angle)*s.z*0.15)
		Model._segment(root,previous,point,0.018,m.metal)
		previous = point
	Model._segment(root,previous,previous-Vector3.UP*0.026,0.018,m.metal)
	_round(root,Vector3(s.x*0.86,s.y-0.12,s.z*0.35),Vector3(0.14,0.07,0.12),m.cloth,0.025)

static func _fridge(root: Node3D, s: Vector3, m: Dictionary) -> void:
	_round(root,Vector3(s.x*0.5,s.y*0.5,(s.z+0.085)*0.5),Vector3(s.x,s.y,s.z-0.085),m.enamel,0.075)
	for section: int in range(2):
		var fraction: float = 0.30 if section==0 else 0.61
		var y: float = 0.82 if section==0 else 0.335
		_round(root,Vector3(s.x*0.5,s.y*y,0.04),Vector3(s.x*0.92,s.y*fraction,0.07),m.enamel,0.032)
		Model._segment(root,Vector3(s.x*0.14,s.y*y-0.08,0.008),Vector3(s.x*0.14,s.y*y+0.08,0.008),0.018,m.metal)
	_round(root,Vector3(s.x*0.72,s.y*0.83,0.006),Vector3(0.15,0.14,0.008),m.cloth,0.003)
