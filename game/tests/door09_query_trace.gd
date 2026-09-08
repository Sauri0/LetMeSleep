extends RefCounted
## Test-only dependency for exact source-derived Arena; no production edits.
const Current=preload("res://scripts/door_catalog.gd")
const Reference=preload("res://tests/door09_catalog_reference.gd")
const DEFINITIONS=Current.DEFINITIONS
const GAP=Current.GAP
static var use_reference:=true
static var capture:=false
static var tick:=0
static var snapshot: Dictionary={}
static var queries: Array=[]
static var total_us:=0
static var calls:=0
static func body_blocked(body: AABB, states: Dictionary, map_id: String="house") -> bool:
	var start:=Time.get_ticks_usec()
	var result: bool=Reference.body_blocked(body,states,map_id) if use_reference else Current.body_blocked(body,states,map_id)
	var duration:=Time.get_ticks_usec()-start
	if capture:
		total_us+=duration;calls+=1
		queries.append({"body":body,"states":snapshot,"map_id":map_id,"result":result,"tick":tick})
	return result
static func intersects_body(definition: Dictionary, angle: float, body: AABB) -> bool:
	return Current.intersects_body(definition,angle,body)
static func ray_doors(from: Vector3, to: Vector3, states: Dictionary, map_id: String="house", padding: float=0.0) -> Dictionary:
	return Current.ray_doors(from,to,states,map_id,padding)
