extends SubViewportContainer
## Staged models only. No production ActorView, colliders or profile writes.
const CharacterSkin=preload("res://assets/art/characters/shared/character_skin.gd")
const Pose=preload("res://scripts/human_pose.gd")
var viewport:SubViewport
var studio:Node3D
var character:Node3D
var skin:Node3D
var camera:Camera3D
var species:="human"
var proposal:="A"
var expression:="neutral"
var clock:=0.0
var magnification:=1.0
var exploded:=false
var freeze_blinks:=true
var mesh_positions:Dictionary={}
var other_skin:Node3D

func _ready()->void:
	stretch=true
	custom_minimum_size=Vector2(300,300)
	viewport=SubViewport.new()
	viewport.own_world_3d=true
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	viewport.msaa_3d=Viewport.MSAA_2X
	viewport.positional_shadow_atlas_size=2048
	viewport.positional_shadow_atlas_quad_0=Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_1
	add_child(viewport)
	studio=Node3D.new()
	viewport.add_child(studio)
	var env_node:=WorldEnvironment.new()
	var env:=Environment.new()
	env.background_mode=Environment.BG_COLOR
	env.background_color=Color("273b46")
	env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color=Color("b6c8d9")
	env.ambient_light_energy=.28
	env_node.environment=env
	studio.add_child(env_node)
	# The same Compatibility light types/material response used by the house.
	# Fixed setup for every proposal, with a real contact shadow and no SSAO.
	var key:=SpotLight3D.new()
	key.position=Vector3(-2.2,3.4,2.5)
	key.light_color=Color("ffe0bd")
	key.light_energy=1.85
	key.spot_range=8.0
	key.spot_angle=55.0
	key.shadow_enabled=true
	key.shadow_bias=.20
	key.shadow_normal_bias=2.0
	studio.add_child(key)
	key.look_at(Vector3(0,.9,0))
	var fill:=DirectionalLight3D.new()
	fill.rotation_degrees=Vector3(-30,145,0)
	fill.light_color=Color("aec8dd")
	fill.light_energy=.32
	studio.add_child(fill)
	var floor_mesh:=MeshInstance3D.new()
	var plane:=PlaneMesh.new()
	plane.size=Vector2(12,12)
	floor_mesh.mesh=plane
	var material:=StandardMaterial3D.new()
	material.albedo_color=Color("677272")
	material.roughness=.9
	floor_mesh.material_override=material
	studio.add_child(floor_mesh)
	camera=Camera3D.new()
	camera.near=.015
	camera.fov=36
	studio.add_child(camera)
	camera.make_current()

func set_sample(variant:String,role:String,face:int=1)->void:
	proposal=variant
	species=role
	if character!=null:
		studio.remove_child(character)
		character.queue_free()
	character=Node3D.new()
	character.rotation.y=PI
	studio.add_child(character)
	skin=CharacterSkin.new()
	character.add_child(skin)
	var directory:="res://assets/art/samples07/characters/%s/%s/" % [proposal,species]
	skin.setup(species,directory+species+"_lms06.glb",directory+"rig_contract.json")
	skin.set_appearance({"color":1,"accent":4,"face":face,"hair":0,"outfit":0,"accessory":3 if species=="human" else 0,"footwear":0})
	magnification=1.0 if species=="human" else 4.0
	character.position=Vector3(0,0 if species=="human" else .55,0)
	skin.scale=Vector3.ONE*(1.0 if species=="human" else .35*magnification)
	mesh_positions.clear()
	for mesh:MeshInstance3D in skin.meshes:mesh_positions[mesh]=mesh.position
	set_view("three-quarter")
	step(1.0/30.0)

func set_expression(value:String)->void:
	expression=value

func set_view(view:String)->void:
	var target:=Vector3(0,.94 if species=="human" else .55,0)
	var yaw:=0.0 if view=="front" else PI*.5 if view=="side" else PI if view=="back" else -.60
	camera.projection=Camera3D.PROJECTION_ORTHOGONAL if view in ["front","side","back","exploded"] else Camera3D.PROJECTION_PERSPECTIVE
	camera.size=2.20 if species=="human" else 1.20
	if view=="exploded":camera.size*=1.35
	var distance:=4.0 if species=="human" else 2.30
	camera.position=target+Vector3(0,0.0 if view in ["front","side","back"] else .30,distance).rotated(Vector3.UP,yaw)
	camera.look_at(target)

func set_face_focus()->void:
	camera.projection=Camera3D.PROJECTION_PERSPECTIVE
	var target:=Vector3(0,1.595 if species=="human" else .55+.012*.35*magnification,0)
	camera.position=target+Vector3(0,0,.86 if species=="human" else .78)
	camera.look_at(target)

func set_exploded(value:bool)->void:
	exploded=value
	var offsets:Dictionary={"head":Vector3(0,.16,0),"face":Vector3(0,.16,-.24),"hair":Vector3(.30,.30,0),"accessory":Vector3(-.32,.47,0),"core":Vector3(-.30,0,0),"footwear":Vector3(.28,0,0)}
	if species=="mosquito":offsets={"face":Vector3(0,.13,-.17),"hair":Vector3(.22,.15,0),"accessory":Vector3(-.22,.23,0),"outfit":Vector3(0,.05,.22),"footwear":Vector3(0,-.17,0),"wing":Vector3(0,.20,0)}
	for mesh:MeshInstance3D in skin.meshes:
		var category:String=str(mesh.name).split("_")[1]
		mesh.position=Vector3(mesh_positions[mesh])+(Vector3(offsets.get(category,Vector3.ZERO))/skin.scale.x if value else Vector3.ZERO)

func set_real_scale_pair()->void:
	if species!="human":return
	other_skin=CharacterSkin.new()
	studio.add_child(other_skin)
	var directory:="res://assets/art/samples07/characters/%s/mosquito/" % proposal
	other_skin.setup("mosquito",directory+"mosquito_lms06.glb",directory+"rig_contract.json")
	other_skin.set_appearance({"color":1,"accent":4,"face":0,"hair":0,"outfit":0,"accessory":0,"footwear":0})
	other_skin.scale=Vector3.ONE*.35
	other_skin.position=Vector3(.46,1.17,.10)
	other_skin.rotation.y=PI

func step(dt:float)->void:
	if skin==null:return
	clock+=dt
	var data:Dictionary={"p":Vector3.ZERO,"yaw":PI,"body_yaw":PI,"pitch":0.0,"state":"human" if species=="human" else "flying","velocity":Vector3.ZERO,"motion_speed":0.0,"grounded":true,"relaxed_pose":true,"pose_time":0.0 if expression=="neutral" else clock,"preview_only":true,"facial_preview":expression,"facial_no_blink":freeze_blinks,"tool":"hands"}
	if species=="human":skin.apply_human(Pose.sample(data),data,dt)
	else:skin.apply_mosquito(data,clock,0.0)
	if other_skin!=null:other_skin.apply_mosquito({"state":"flying","preview_only":true,"facial_preview":"neutral"},clock,0.0)
