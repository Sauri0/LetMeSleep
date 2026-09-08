extends RefCounted
## Geometry-only acoustic permission. Both teams use the same portal routes.
const Maps=preload("res://scripts/map_catalog.gd")
const Doors=preload("res://scripts/door_catalog.gd")
var map_id:=""
var data: Dictionary={}
var walls: Array[AABB]=[]
var nodes: Array[Vector3]=[]
var edges: Array=[]
var definitions: Dictionary={}

func configure(id: String) -> void:
	if id==map_id: return
	map_id=id;data=Maps.get_map(id);walls.clear();nodes.clear();edges.clear()
	definitions=Doors.get_doors(id)
	for structure: Dictionary in data.get("structures",[]):
		if str(structure.get("kind","")) in ["wall","floor","ceiling"]: walls.append(structure.box)
	for point: Vector3 in data.get("nav_nodes",[]): nodes.append(point+Vector3.UP*1.4);edges.append([])
	for link: Vector2i in data.get("nav_edges",[]):
		edges[link.x].append(link.y);edges[link.y].append(link.x)

static func mouth(actor: Dictionary) -> Vector3:
	return Vector3(actor.get("p",Vector3.ZERO))+Vector3.UP*(lerpf(1.48,1.05,float(actor.get("crouch_amount",0.0))) if str(actor.get("role",""))=="human" else 0.0)

func clear_line(from: Vector3, to: Vector3) -> bool:
	var delta:=to-from
	if delta.length_squared()<.0001: return true
	# Endpoints at a portal lie on its opening; inset avoids boundary noise.
	var inset:=delta.normalized()*.015
	for box: AABB in walls:
		if box.intersects_segment(from+inset,to-inset)!=null: return false
	return true

func route(from: Vector3, to: Vector3, maximum: float) -> PackedVector3Array:
	if from.distance_to(to)>maximum: return PackedVector3Array()
	if clear_line(from,to): return PackedVector3Array([from,to])
	# A bounded Dijkstra visits only nodes within the audible path budget.
	var distance: Dictionary={};var previous: Dictionary={};var open: Array[int]=[]
	for index: int in range(nodes.size()):
		var length:=from.distance_to(nodes[index])
		if length<=maximum and clear_line(from,nodes[index]): distance[index]=length;open.append(index)
	var best:=maximum+.0001;var last:=-1
	while not open.is_empty():
		var selected:=0
		for index: int in range(1,open.size()):
			if float(distance[open[index]])<float(distance[open[selected]]): selected=index
		var at: int=open[selected];open.remove_at(selected)
		var travelled: float=distance[at]
		if travelled>best: continue
		var end_distance:=nodes[at].distance_to(to)
		if travelled+end_distance<best and clear_line(nodes[at],to): best=travelled+end_distance;last=at
		for neighbor: int in edges[at]:
			var next:=travelled+nodes[at].distance_to(nodes[neighbor])
			if next<best and next<float(distance.get(neighbor,INF)):
				distance[neighbor]=next;previous[neighbor]=at
				if not open.has(neighbor): open.append(neighbor)
	if last<0: return PackedVector3Array()
	var result:=PackedVector3Array([to,nodes[last]])
	while previous.has(last): last=previous[last];result.append(nodes[last])
	result.append(from);result.reverse();return result

func permission(speaker: Dictionary, listener: Dictionary, door_states: Dictionary) -> Dictionary:
	if not bool(speaker.get("alive",false)) or not bool(listener.get("alive",false)): return {}
	var insect:=str(speaker.get("role",""))=="mosquito"
	var human_listener:=str(listener.get("role",""))=="human"
	var maximum:=3.0 if insect and human_listener else 8.0 if insect else 10.0
	var role_gain:=.25 if insect and human_listener else .85 if insect else 1.0
	var path:=route(mouth(speaker),mouth(listener),maximum)
	if path.is_empty(): return {}
	var length:=0.0;var crossed: Dictionary={}
	for index: int in range(1,path.size()):
		length+=path[index-1].distance_to(path[index])
		for id: String in definitions:
			# The closed portal plane identifies a crossing at any current angle.
			if not Doors.ray_leaf(definitions[id],0.0,path[index-1],path[index],.025).is_empty(): crossed[id]=true
	var transmission:=1.0;var cutoff:=12000.0
	for id: String in crossed:
		var openness:=clampf(float(door_states.get(id,{}).get("angle",Doors.OPEN_ANGLE))/Doors.OPEN_ANGLE,0.0,1.0)
		transmission*=lerpf(.25,1.0,openness)
		cutoff=minf(cutoff,lerpf(1200,12000,openness))
	var gain:=role_gain*pow(maxf(0.0,1.0-pow(length/maximum,2.0)),2.0)*transmission
	return {"gain":gain,"cutoff":cutoff,"distance":length,"pitch":1.55 if insect else 1.0} if gain>.001 else {}
