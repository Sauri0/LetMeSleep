class_name ToolCatalog
extends RefCounted
## Metres, seconds. Canonical +Y points from grip toward the active part; +Z
## is the striking face normal. All returned Dictionaries are private copies.
## Source transform is applied before subtracting the canonical grip vector.

const IDS: Array[String] = ["hands","swatter","racket","newspaper","broom","slipper"]
const ARM_REACH := .93 # Shoulder to the centre of the palm/grip, not the eye.
const GRASPS := {
	"swatter":{"radius":.0185,"depth":.0115,"curl":1.15,"rest":Vector3(-.015,-.24,-.54)},
	"racket":{"radius":.0265,"curl":1.07,"rest":Vector3(.005,-.23,-.54)},
	"newspaper":{"radius":.046875,"curl":.72,"rest":Vector3(-.02,-.19,-.55)},
	"broom":{"radius":.0265,"curl":1.10,"rest":Vector3(.02,-.24,-.52)},
	"slipper":{"radius":.052,"depth":.033,"curl":.90,"rest":Vector3(-.005,-.20,-.55)},
}
const DATA := {
	"hands": {"label":"Manos / palmadas","reach":ARM_REACH,"reach_origin":"shoulder","cooldown":.80,"radius":.095,"length":0.0,"gesture":.36,"damage_start":.08,"damage_end":.25,"throwable":false,"material":"skin","use":"Palmada manual"},
	"swatter": {"label":"Matamoscas","reach":ARM_REACH+.4014545455,"reach_origin":"shoulder","cooldown":.60,"radius":.115,"length":.4014545455,"gesture":.32,"damage_start":.065,"damage_end":.22,"throwable":false,"material":"plastic","use":"Golpe de cara plana"},
	"racket": {"label":"Raqueta eléctrica","reach":ARM_REACH+.485,"reach_origin":"shoulder","cooldown":1.05,"radius":.14,"length":.485,"gesture":.44,"damage_start":.11,"damage_end":.32,"throwable":false,"material":"plastic","use":"Golpe de malla ancha"},
	"newspaper": {"label":"Diario enrollado","reach":ARM_REACH+.24,"reach_origin":"shoulder","cooldown":.43,"radius":.06,"length":.24,"gesture":.28,"damage_start":.05,"damage_end":.19,"throwable":true,"material":"cloth","use":"Golpe corto / lanzamiento"},
	"broom": {"label":"Escoba","reach":ARM_REACH+.8675,"reach_origin":"shoulder","cooldown":1.20,"radius":.17,"length":.8675,"gesture":.52,"damage_start":.14,"damage_end":.38,"throwable":false,"material":"wood","use":"Golpe de cepillo largo"},
	"slipper": {"label":"Pantufla de mano","reach":ARM_REACH+.18,"reach_origin":"shoulder","cooldown":.60,"radius":.075,"length":.18,"gesture":.34,"damage_start":.07,"damage_end":.23,"throwable":true,"material":"cloth","use":"Palmada con suela / lanzamiento"},
}
# Backward-compatible Sim.TOOL_STATS alias; existing consumers read four keys.
const MELEE_STATS := DATA
const THROW_STATS := {
	"newspaper": {"launch_seconds":.12,"recovery_seconds":.45,"charge_seconds":.85,"min_speed":4.0,"max_speed":10.0,"radius":.0468751,"gravity":9.8,"lifetime":8.0,"bounce":.10,"capsule_from":Vector3(0,-.0131249,0),"capsule_to":Vector3(0,.2118749,0)},
	"slipper": {"launch_seconds":.12,"recovery_seconds":.45,"charge_seconds":1.15,"min_speed":4.0,"max_speed":12.0,"radius":.066,"gravity":9.8,"lifetime":8.0,"bounce":.12,"capsule_from":Vector3(0,.025,.020),"capsule_to":Vector3(0,.192,.020)},
}
const VISUALS := {
	"hands": {"scene":"","asset_rotation":Vector3.ZERO,"scale":Vector3.ONE,"grip":Vector3.ZERO,"contact":Vector3.ZERO,"axis":Vector3.UP,"face_normal":Vector3.BACK,"bounds":AABB(),"rest_direction":Vector3.DOWN,"ground_rotation":Vector3.ZERO},
	"swatter": {"scene":"res://assets/art/house/swatter.glb","asset_rotation":Vector3.ZERO,"scale":Vector3.ONE*(.46/.55),"grip":Vector3(0,.0585454545,0),"contact":Vector3(0,.4014545455,0),"axis":Vector3.UP,"face_normal":Vector3.BACK,"bounds":AABB(Vector3(-.123484,-.058546,-.014219),Vector3(.246968,.602117,.028538)),"rest_direction":Vector3(0,.86,-.5),"ground_rotation":Vector3(PI/2,0,0)},
	"racket": {"scene":"res://assets/art/characters/tools/racket.glb","asset_rotation":Vector3(0,0,PI),"scale":Vector3.ONE,"grip":Vector3(0,.025,0),"contact":Vector3(0,.485,0),"axis":Vector3.UP,"face_normal":Vector3.BACK,"bounds":AABB(Vector3(-.153990,-.080001,-.029925),Vector3(.307991,.742002,.055926)),"rest_direction":Vector3(0,.82,-.57),"ground_rotation":Vector3(PI/2,0,0)},
	"newspaper": {"scene":"res://assets/art/characters/tools/newspaper.glb","asset_rotation":Vector3.ZERO,"scale":Vector3.ONE,"grip":Vector3(0,.06,0),"contact":Vector3(0,.24,0),"axis":Vector3.UP,"face_normal":Vector3.BACK,"bounds":AABB(Vector3(-.046876,-.06,-.046876),Vector3(.093752,.318751,.093752)),"rest_direction":Vector3(0,.65,-.76),"ground_rotation":Vector3(PI/2,0,0)},
	"broom": {"scene":"res://assets/art/characters/tools/broom.glb","asset_rotation":Vector3(0,0,PI),"scale":Vector3.ONE,"grip":Vector3(0,.0125,0),"contact":Vector3(0,.8675,0),"axis":Vector3.UP,"face_normal":Vector3.BACK,"bounds":AABB(Vector3(-.170001,-.072501,-.049001),Vector3(.340002,1.020444,.098002)),"rest_direction":Vector3(0,.94,-.34),"ground_rotation":Vector3(PI/2,0,0)},
	"slipper": {"scene":"res://assets/art/characters/tools/slipper.glb","asset_rotation":Vector3.ZERO,"scale":Vector3.ONE,"grip":Vector3(0,.035,0),"contact":Vector3(0,.18,0),"axis":Vector3.UP,"face_normal":Vector3.BACK,"bounds":AABB(Vector3(-.064001,-.035,-.022001),Vector3(.128002,.29,.077002)),"rest_direction":Vector3(0,.72,-.69),"ground_rotation":Vector3(PI/2,0,0)},
}

static func has_tool(id: String) -> bool:
	return DATA.has(id)

static func get_tool(id: String) -> Dictionary:
	var key := id if DATA.has(id) else "hands"
	var data: Dictionary = DATA[key].duplicate(true)
	data.id = key
	data.visual = visual(key)
	data.throw = throw_stats(key)
	data.charge_seconds = float(data.throw.get("charge_seconds",0.0))
	data.throw_min = float(data.throw.get("min_speed",0.0))
	data.throw_max = float(data.throw.get("max_speed",0.0))
	data.projectile_radius = float(data.throw.get("radius",0.0))
	return data

static func melee_stats(id: String) -> Dictionary:
	var source: Dictionary = DATA.get(id,DATA.hands)
	return {"label":source.label,"reach":source.reach,"reach_origin":source.reach_origin,"cooldown":source.cooldown,"radius":source.radius,"gesture":source.gesture,"damage_start":source.damage_start,"damage_end":source.damage_end}

static func contact_length(id: String) -> float:
	return float(DATA.get(id,DATA.hands).length)

static func visual(id: String) -> Dictionary:
	return VISUALS.get(id,VISUALS.hands).duplicate(true)

static func throwable(id: String) -> bool:
	return bool(DATA.get(id,DATA.hands).throwable)

static func throw_stats(id: String) -> Dictionary:
	return THROW_STATS.get(id,{}).duplicate(true)

## WORLD orientation at release and during projectile flight. The shaft is +Y;
## choose the same roll on the held mesh and the authoritative thrown capsule.
static func launch_basis(direction: Vector3) -> Basis:
	var axis := direction.normalized()
	if axis.length_squared()<.5: axis=Vector3.FORWARD
	var face := Vector3.UP-axis*axis.dot(Vector3.UP)
	if face.length_squared()<.0001: face=Vector3.BACK-axis*axis.dot(Vector3.BACK)
	face=face.normalized()
	return Basis(axis.cross(face).normalized(),axis,face)
