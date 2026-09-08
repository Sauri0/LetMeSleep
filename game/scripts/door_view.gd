extends Node3D
## Presentation mirrors the shared hinged geometry. The simulation owns state.
const Catalog = preload("res://scripts/door_catalog.gd")
const LEAF_PATH := "res://assets/art/house/door_leaf.glb"
const FRAME_PATH := "res://assets/art/house/door_frame.glb"
var definitions: Dictionary = {}
var views: Dictionary = {}
var received_state := false

func setup(map_id: String) -> void:
	for child: Node in get_children():
		remove_child(child)
		child.queue_free()
	views.clear()
	definitions = Catalog.get_doors(map_id)
	received_state = false
	for id: String in definitions:
		var definition: Dictionary = definitions[id]
		var frame := _model(FRAME_PATH,definition,false)
		add_child(frame)
		frame.transform = Catalog.leaf_transform(definition,0.0)
		var pivot := Node3D.new()
		pivot.name = "Door_"+id
		add_child(pivot)
		pivot.add_child(_model(LEAF_PATH,definition,true))
		var body := StaticBody3D.new()
		body.collision_layer = 1
		body.collision_mask = 0
		body.set_meta("door_id",id)
		body.set_meta("surface_kind","wood")
		pivot.add_child(body)
		var collision := CollisionShape3D.new()
		var box: AABB = Catalog.leaf_box(definition)
		var shape := BoxShape3D.new()
		shape.size = box.size
		collision.shape = shape
		collision.position = box.get_center()
		body.add_child(collision)
		pivot.transform = Catalog.leaf_transform(definition,Catalog.OPEN_ANGLE)
		views[id] = {"pivot":pivot,"body":body,"angle":Catalog.OPEN_ANGLE}

func _model(path: String, definition: Dictionary, leaf: bool) -> Node3D:
	var result := Node3D.new()
	if ResourceLoader.exists(path):
		var scene: PackedScene = load(path)
		var mesh: Node3D = scene.instantiate()
		mesh.scale.x = float(definition.width)
		result.add_child(mesh)
		result.set_meta("authored_asset",path)
	elif leaf:
		# Visible integration placeholder only; release checks require both GLBs.
		var mesh := MeshInstance3D.new()
		var box := BoxMesh.new()
		var bounds: AABB = Catalog.leaf_box(definition)
		box.size = bounds.size
		mesh.mesh = box
		mesh.position = bounds.get_center()
		var material := StandardMaterial3D.new()
		material.albedo_color = Color("a78666")
		material.roughness = 0.9
		mesh.material_override = material
		result.add_child(mesh)
	return result

func sync(states: Dictionary, dt: float) -> void:
	if states.is_empty(): return
	var snap := not received_state or dt >= 0.25
	for id: String in views:
		if not states.has(id): continue
		var state: Dictionary = states[id]
		var target := clampf(float(state.get("angle",Catalog.OPEN_ANGLE)),0.0,Catalog.OPEN_ANGLE)
		var view: Dictionary = views[id]
		view.angle = target if snap else lerpf(float(view.angle),target,1.0-exp(-28.0*maxf(dt,0.0)))
		# Mesh and local ray collider always have the same pose, including transit.
		var pivot: Node3D = view.pivot
		pivot.transform = Catalog.leaf_transform(definitions[id],float(view.angle))
		pivot.set_meta("door_state",state.duplicate(true))
	received_state = true
