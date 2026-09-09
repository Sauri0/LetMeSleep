extends SceneTree
## Read-only baseline of 0.4 production. Staged snapshots, real Client/UI camera.
## Native run: --script res://tests/v05_baseline_audit.gd
const Simulation = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
class Transport:
	extends Node
	signal lobby_updated(data: Dictionary)
	signal snapshot_updated(data: Dictionary)
	signal private_updated(data: Dictionary)
	signal notice(message: String)
	signal accepted(peer_id: int)
	signal disconnected
	signal waiting_updated(data: Dictionary)
	signal connection_state_changed(data: Dictionary)
	var local_cosmetics: Dictionary = {}
	var is_server := false
	var voice: Node
	func _ready() -> void:
		# Real Client now requires the voice transport even in staged camera
		# fixtures. Keep its lifecycle real while this transport stays offline.
		voice = load("res://scripts/voice_transport.gd").new()
		add_child(voice)
	func _client_connected() -> bool:
		return false
	func connect_room(_address: String,_port: int,_player_name: String,_code: String,_create: bool) -> void:
		pass
	func lobby_action(_verb: String,_value: Variant = null) -> void:
		pass
	func send_input(_seq: int,_move: Vector3,_yaw: float,_pitch: float,_interact: bool,_sprint: bool=false,_crouch: bool=false,_jump: bool=false) -> void:
		pass
	func send_action(_seq: int,_verb: String,_yaw: float=NAN,_pitch: float=NAN) -> void:
		pass
	func close_client() -> void:
		pass
	func cancel_connect() -> void:
		pass
	func retry_connect() -> void:
		pass
var client: Node
var simulation: RefCounted
var audit_camera: Camera3D
var manifest: Array[Dictionary] = []
var output_dir: String
var failures := 0

func _initialize() -> void:
	_run.call_deferred()

func settle(frames: int = 14) -> void:
	for frame: int in range(frames):
		await process_frame
	await physics_frame
	await RenderingServer.frame_post_draw

func capture(file_name: String, description: String, staged: Dictionary = {}) -> void:
	await settle()
	var path: String = output_dir.path_join(file_name + ".png")
	var result: Error = root.get_texture().get_image().save_png(path)
	if result != OK:
		failures += 1
	var active: Camera3D = root.get_camera_3d()
	var data: Dictionary = {"file":file_name+".png","description":description,"camera":str(active.global_position),"rotation":str(active.global_rotation),"fov":active.fov,"staged":staged,"save_error":result}
	manifest.append(data)
	print("BASELINE_CAPTURE " + JSON.stringify(data))

func _human(extra: Dictionary = {}, look_pitch: float = 0.0) -> void:
	client.playing = false
	client.local_id = 1
	var actor: Dictionary = simulation.actors[1]
	actor.merge({"p":Vector3(-6.4,0,7.8),"yaw":0.0,"body_yaw":0.0,"pitch":look_pitch,"tool":"hands","swing":0.0,"strike":{},"bitten":false,"motion_speed":0.0,"motion_phase":0.0,"crouch_amount":0.0,"grounded":true,"sprinting":false,"appearance":{"color":1,"accessory":0}},true)
	actor.merge(extra,true)
	simulation.actors[2].p = Vector3(-9.0,1.1,7.7)
	simulation.actors[2].state = "flying"
	simulation.actors[2].attached_to = 0
	client._snapshot(simulation.public_snapshot())
	client.pitch = look_pitch
	client.yaw = float(actor.yaw)
	client.personal = simulation.private_for(1)
	client.ui.show_game(client.state,client.personal,1)
	client.set_process(true)
	client.ui.visible = true
	await settle()

func _attach(zone_id: int) -> Dictionary:
	var zone: Dictionary = Pose.zone_pose(simulation.actors[1],Simulation.BODY_ZONES[zone_id])
	simulation.actors[2].p = zone.p + zone.normal * 0.045
	simulation.actors[2].state = "biting"
	simulation.actors[2].attached_to = 1
	simulation.actors[2].yaw = atan2(zone.normal.x,zone.normal.z)
	simulation.actors[2].pitch = asin(-zone.normal.y)
	simulation.actors[1].bitten = true
	client._snapshot(simulation.public_snapshot())
	return zone

func zone_index(label: String) -> int:
	for index: int in range(Simulation.BODY_ZONES.size()):
		if str(Simulation.BODY_ZONES[index].label)==label:
			return index
	return 0

func _external(position: Vector3, target: Vector3) -> void:
	client.set_process(false)
	client.ui.visible = false
	client.world.set_local_role(0,"lobby")
	client.world.sync_actors(client.state.actors,0,1.0)
	audit_camera.global_position = position
	audit_camera.look_at(target)
	audit_camera.make_current()
	await settle()

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("The baseline audit requires the native renderer.")
		quit(2)
		return
	root.size = Vector2i(1280,720)
	output_dir = ProjectSettings.globalize_path("res://../outputs/0.5-preview" if "--after" in OS.get_cmdline_user_args() else "res://../outputs/0.5-auditoria")
	if "--after" not in OS.get_cmdline_user_args() and FileAccess.file_exists(output_dir.path_join("capturas-manifest.json")):
		push_error("The archived 0.4 baseline must not be overwritten. Use -- --after for current source.")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(output_dir)
	var network := Transport.new()
	root.add_child(network)
	client = load("res://scripts/client.gd").new()
	client.network = network
	client.options = {"visual-preview":"human"}
	root.add_child(client)
	client.set_process_unhandled_input(false)
	audit_camera = Camera3D.new()
	audit_camera.near = 0.025
	audit_camera.fov = 70.0
	root.add_child(audit_camera)
	simulation = Simulation.new()
	simulation.start({1:{"name":"Luna","role":"human"},2:{"name":"Zeta","role":"mosquito"}},Simulation.DEFAULT_CONFIG)
	await _human()
	await capture("01-humano-frente","Cámara y HUD reales de Client, humano quieto, manos iniciales.")
	await _human({},-0.95)
	await capture("02-humano-abajo-54grados","Humano mira abajo; cuerpo real y brazos FPS simultáneos.")
	await _human({},-1.4)
	await capture("03-humano-abajo-limite","Límite inferior real de cámara: -1.4 radianes (80.2 grados).")
	var zone: Dictionary = _attach(zone_index("Pecho izquierdo"))
	await capture("04-picadura-pecho-propio","Snapshot de insecto adherido al pecho izquierdo, posición de la cápsula compartida.",{"zone":5,"point":str(zone.p)})
	await _human({},-1.4)
	zone = _attach(zone_index("Antebrazo izquierdo"))
	await capture("05-picadura-brazo-propio","Mosquito sobre antebrazo físico; el brazo real se oculta en primera persona.",{"zone":7,"point":str(zone.p)})
	await _human({"crouch_amount":1.0},-1.4)
	zone = _attach(zone_index("Muslo izquierdo"))
	await capture("06-humano-agachado-abajo","Humano agachado mirando abajo, insecto en muslo izquierdo.",{"zone":11,"eye_y":float(Pose.sample(simulation.actors[1]).eye.y)})
	await _human({"motion_speed":5.0,"sprinting":true,"motion_phase":PI/2},-1.12)
	await capture("07-humano-corriendo-abajo-a","Pose de carrera fase PI/2 con cámara real, snapshot congelado.")
	await _human({"motion_speed":5.0,"sprinting":true,"motion_phase":3*PI/2},-1.12)
	await capture("08-humano-corriendo-abajo-b","Pose de carrera fase 3PI/2 con cámara real, snapshot congelado.")
	await _human({"swing":0.62,"strike":{"active":true,"progress":0.5,"point":Vector3(-6.73,0.98,7.36),"hand":"right","tool":"hands"}},-1.1)
	await capture("09-palmada-propia-pico","Pico del gesto de palmada .18s, vista real de Client.")
	await _human()
	await _external(Vector3(-4.85,1.25,5.75),Vector3(-6.4,0.92,7.8))
	await capture("10-cuerpo-exterior-quieto","Mismo cuerpo completo visto externamente: cámara de auditoría, sin HUD.")
	for step: int in range(3):
		simulation.actors[1].merge({"motion_speed":5.0,"sprinting":true,"motion_phase":float(step)*PI/2},true)
		client.state = simulation.public_snapshot()
		client.world.sync_actors(client.state.actors,0,1.0)
		await capture("11-carrera-exterior-"+str(step),"Pose exterior de carrera fase "+str(float(step)*PI/2)+", snapshot congelado.")
	simulation.actors[1].merge({"motion_speed":0.0,"sprinting":false,"motion_phase":0.0,"crouch_amount":1.0},true)
	client.state = simulation.public_snapshot()
	client.world.sync_actors(client.state.actors,0,1.0)
	await capture("12-cuerpo-exterior-agachado","Cuerpo completo agachado y articulaciones reales.")
	await _human()
	zone = _attach(zone_index("Pecho izquierdo"))
	client.local_id = 2
	client.role = "mosquito"
	client.world.set_local_role(2,"mosquito")
	client.yaw = float(simulation.actors[2].yaw)
	client.pitch = float(simulation.actors[2].pitch)
	client.personal = {"assignment":{"human":1,"zone":zone_index("Pecho izquierdo"),"p":zone.p,"normal":zone.normal,"label":zone.label},"focus":{"state":"attached","progress":1.0,"distance":0.0}}
	client.ui.show_game(client.state,client.personal,2)
	await capture("13-mosquito-adherido","Client real en perspectiva mosquito: cuerpo a escala .35 y anillo privado. Snapshot de contacto congelado.")
	simulation.actors[2].p = zone.p + zone.normal * 0.85
	simulation.actors[2].state = "flying"
	client.state = simulation.public_snapshot()
	client.personal.focus = {"state":"ready","progress":0.0,"distance":0.85,"can_focus":true}
	client.ui.show_game(client.state,client.personal,2)
	await capture("14-mosquito-aproximacion","Client real mosquito aproximándose a marca de pecho. Snapshot congelado.")
	client.world.clear_actors()
	client.state.actors = {}
	await _external(Vector3(3.0,4.75,6.2),Vector3(6.2,4.05,9.6))
	await capture("15-dormitorio-planta-alta","Dormitorio rosa y cuarto contiguo, geometría/iluminación 0.4.")
	await _external(Vector3(-11,1.55,-5.15),Vector3(-11,2.7,2.7))
	await capture("16-escalera-desde-abajo","Escalera oeste desde planta baja, ambos niveles y peldaños.")
	await _external(Vector3(-11,4.75,4.9),Vector3(-11,2.5,-2))
	await capture("17-escalera-desde-arriba","Escalera oeste desde planta alta.")
	await _external(Vector3(-2.75,1.6,2.65),Vector3(-6.6,0.95,-1.6))
	await capture("18-sala-de-estar","Sala de estar, muebles y espacio navegable baseline.")
	await _external(Vector3(-2.8,1.6,-6.45),Vector3(-6.2,0.95,-9.6))
	await capture("19-cocina","Cocina, mesada/alacena y estación de ventana baseline.")
	var file := FileAccess.open(output_dir.path_join("capturas-manifest.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"version":"0.5 candidate" if "--after" in OS.get_cmdline_user_args() else "0.4 baseline","renderer":RenderingServer.get_video_adapter_name(),"viewport":"1280x720","scope":"State snapshots staged; human/mosquito cameras and HUD are real Client. Environment/exterior cameras are audit-only. Room/exterior camera transforms match the 0.4 baseline.","captures":manifest},"\t"))
	file.close()
	client.playing = false
	client.world.clear_actors()
	await settle(3)
	client.queue_free()
	audit_camera.queue_free()
	await process_frame
	await process_frame
	print("BASELINE_RESULT captures=%d failures=%d" % [manifest.size(),failures])
	quit(failures)
