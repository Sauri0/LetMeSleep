class_name Arena
extends RefCounted

# Shared metric layout. Feet at y=0, human eye y=1.55; front is -Z.
const HALF_X := 6.0
const HALF_Z := 5.0
const CEILING := 2.8
const HUMAN_SPEED := 3.1
const MOSQUITO_SPEED := 3.5
const HUMAN_RADIUS := 0.60 # Keeps every rotated body mark + attached mosquito inside walls/furniture clearance.
const MOSQUITO_RADIUS := 0.10
const OBSTACLES := [
	AABB(Vector3(-5.5, 0, 1.5), Vector3(2.4, 0.80, 1.05)), # sofa
	AABB(Vector3(-4.7, 0, -0.6), Vector3(1.4, 0.50, 1.0)), # coffee table
	AABB(Vector3(3.9, 0, 1.7), Vector3(1.0, 1.1, 1.8)), # sideboard
	AABB(Vector3(2.8, 0, -3.8), Vector3(1.9, 0.8, 0.9)), # task counter
]
const STATIONS := [
	{"name": "Cerrar la ventana", "label": "VENTANA", "p": Vector3(-2.5, 0, -4.1)},
	{"name": "Prender el ventilador", "label": "VENTILADOR", "p": Vector3(4.8, 0, -0.6)},
	{"name": "Preparar repelente", "label": "REPELENTE", "p": Vector3(3.1, 0, -2.6)},
]

static func human_spawn(index: int) -> Vector3:
	return Vector3(-2.0 + float(index % 5) * 1.0, 0, 0.4)

static func mosquito_spawn(index: int) -> Vector3:
	return Vector3(-2.0 + float(index % 6) * 0.75, 1.2 + float(index / 6) * 0.35, 3.2 + float(index / 6) * 0.5)

static func move_body(pos: Vector3, displacement: Vector3, human: bool) -> Vector3:
	var radius: float = HUMAN_RADIUS if human else MOSQUITO_RADIUS
	var next := pos
	for axis in [0, 2, 1]:
		var trial := next
		trial[axis] += displacement[axis]
		trial.x = clampf(trial.x, -HALF_X + radius, HALF_X - radius)
		trial.z = clampf(trial.z, -HALF_Z + radius, HALF_Z - radius)
		trial.y = 0.0 if human else clampf(trial.y, radius, CEILING - radius)
		var blocked := false
		var center := trial + Vector3(0, 0.85, 0) if human else trial
		var half := Vector3(radius, 0.84 if human else radius, radius)
		var body := AABB(center - half, half * 2)
		for obstacle: AABB in OBSTACLES:
			if body.intersects(obstacle):
				blocked = true
				break
		if not blocked:
			next = trial
	return next

static func clear_segment(from: Vector3, to: Vector3) -> bool:
	for obstacle: AABB in OBSTACLES:
		if obstacle.intersects_segment(from, to) != null:
			return false
	return true
