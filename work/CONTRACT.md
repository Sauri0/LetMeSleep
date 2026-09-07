# Integration contract v1

Project `game/`, Godot 4.5.2 stable, typed GDScript. Arena coordinates shared in scripts/arena.gd; front=-Z, human p=feet. Root owns main.gd, network.gd, client.gd, project.godot/export and integration. Agents own only assigned files. Avoid edits to others; ask via message.

## Simulation API (RefCounted script scripts/simulation.gd)

`const DEFAULT_CONFIG` dict with mode="blood"/"survival"/"sleep", round_seconds, blood_goal, rotation_seconds, respawn_seconds, task_interval, task_deadline, task_work, task_penalty, task_floor, task_goal.
`start(players: Dictionary, config: Dictionary) -> void` (players int id=>{name,role="human"/"mosquito",ready}); `validate_roster(players) -> String` empty valid; `submit_input(id:int, seq:int, move:Vector3, yaw:float, pitch:float, interact:bool)`; `action(id:int,seq:int,verb:String)` verbs bite (toggle explicit), attack (aimed), self_swat, perch; `step(dt:float)`; `public_snapshot()->Dictionary`; `private_for(id:int)->Dictionary`; `abort(reason:String)`.
Public snapshot: phase="playing"/"results"/"lobby", time_left float, elapsed float, config, blood, winner string, reason, tasks_done int, task_goal int, actors dict int=>{name,role,p:Vector3,yaw:float,pitch:float,state:"human"/"flying"/"perched"/"biting"/"dead",alive:bool,swing:float,bitten:bool}. NEVER broadcast assignments or body zone IDs. Bite physical positions and bitten yes; assignment target NOT in public actors.
Private: mosquito {assignment:{human:int,zone:int,p:Vector3,normal:Vector3,label:String,revision:int}, state:String, respawn_left:float}; human {task:{name,station:int,p:Vector3,remaining:float,progress:float,work:float},deadline:float,failures:int}. Empty assignment if dead. No rotation countdown. Public snapshots clone allowed fields.

## Visual API scripts/world.gd (extends Node3D)

`build()` builds static procedural furnished living room and menu camera, environment; `sync_actors(actors:Dictionary,local_id:int,dt:float)` updates procedural avatars with interpolation; `get_actor(id:int)->Node3D`; `set_local_role(id:int,role:String)` masks own head (human body remains); `show_assignment(data:Dictionary, camera:Camera3D, mosquito_position:Vector3)` private marker with geometry and facing occlusion; `clear_actors()`; optional `play_event(verb:String)`. Shared arena must inform visible furniture dimensions. Client controller root owns cameras/input. World must offer `menu_camera:Camera3D`; own actor model human's head visibility configurable.

## UI API scripts/ui.gd extends CanvasLayer

Signals `connect_requested(address:String,port:int,player_name:String,code:String,create:bool)`, `local_server_requested`, `role_requested(role:String)`, `ready_requested(value:bool)`, `config_requested(config:Dictionary)`, `start_requested`, `rematch_requested`, `leave_requested`.
`show_home()`; `show_status(message:String)`; `show_lobby(data:Dictionary,local_id:int)` data={code,owner:int,players dict,config,can_start:bool,start_reason:String}; `show_game(snapshot:Dictionary,private_data:Dictionary,local_id:int)`; `show_results(snapshot:Dictionary)`; `set_pause(open:bool)`; `is_menu_open()->bool`. No per-frame rebuild of full screen: update labels. Root calls show_game ~20Hz.
UI settings preferences.gd class_name Preferences extends RefCounted: static `setup_inputs()`, static `load_settings()`, static `save_settings()`, static vars human_sensitivity float=.0025, mosquito_sensitivity=.0025, invert_y bool=false, marker_pulse bool=true, master_volume float=.6; remappable actions move_forward(W),move_back(S),move_left(A),move_right(D),ascend(Space),descend(Ctrl),bite(E),attack(LMB),self_swat(Q),perch(F),interact(E held),pause(Escape). client polls preferences; pause releases mouse. Body marker ONLY client private_data assignment.

Prototype capacity 4 humans, 12 mosquitoes max subject valid ratio and zone alternatives (experimental, not user-final). Practical scope 1v2 and 2v4. Client online requires separately running headless server; localhost helper optional. Root adds automated loopback bots using same RPC and rules tests.
