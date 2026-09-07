# Integration contract — Let me sleep 0.4.0 / protocol 4

Project: game/, Godot 4.5.2 stable, typed GDScript. Human position p is feet; forward is -Z. Preserve parallel edits and assigned file ownership. Current code and confirmed decisions below supersede older proposals. The visible product name is Let me sleep; visible sleep mode is Tareas.

## Social lobby and invitation

Windows client and separate headless ENet/UDP server hosted on the user's PC; one registered room per server. Default port 27840. No external hosted service, relay, NAT traversal or HTTP public-IP lookup.

Primary guest flow takes one DD3 invitation containing address, port and room code. This is a versioned encoding, not encryption or cryptographic secrecy. invitation.gd exposes static encode(host:String,port:int,room:String)->String, decode(value:String)->Dictionary, validate_host(value:String)->String and local_addresses()->Array[String]. Valid decode returns {ok,error,host,port,room}; bad data returns an error. Host chooses a reachable shared address independently from the local connection (usually 127.0.0.1). LAN lists eligible local IPv4 addresses. WAN still needs an externally reachable UDP route; CGNAT remains unresolved. Advanced UI permits separate address/port/code when invitation is empty.

Owner chooses exact config.human_count integer1..5. Others become mosquitoes. Both teams nonempty, 1v1 valid, maximum12 mosquitoes and16 total. Do not clamp to connected count. Invalid capacity prevents start. All ready required. Every round draws independently and may repeat roles. Lobby role="waiting" is unrelated to later team.

LobbyRules.validate(players,config,require_ready=true)->String; empty means valid. LobbyRules.draw(players,config,rng=null)->Dictionary returns a deep copy with exact assigned roles, preserving cosmetics/name/ready; invalid draw returns{}. draw itself does not require readiness. Server passes RNG; default RNG randomizes. Sorted IDs plus Fisher–Yates permit deterministic injected-seed tests.

## Maps and locomotion

MapCatalog.get_map(id="house")->Dictionary returns a deep copy; unknown ID returns{}. is_playable(id)->bool currently accepts only house. human_spawn(map_id,index) and mosquito_spawn(map_id,index) return authored positions.

Map schema: id,label,playable,bounds:AABB,half_x,half_z,ceiling,obstacles:Array[AABB],stations:Array[Dictionary],pickups:Array[Dictionary],human_spawns,mosquito_spawns,lobby_spawns,respawn_points.

House bounds 28×22×6.4m, feet levels 0/3.2, sixteen furnished rooms, two stairs, eight stations and eight pickups. structures contains 228 categorized physical boxes matching obstacles; floors/walls/steps, rooms, floor_levels and stair_holes describe the same geometry. Each staircase has 16 steps, rise0.2/tread0.5, with real slab openings. Opposing spawns have no initial LOS. Lobby is separate8×6×4m with two benches and16 human waiting spawns; no round pickups/tasks. Simulation sanitizes config.map_id to a playable map, currently house.

Arena.move_body(pos,displacement,human,map_id="house",height=HUMAN_HEIGHT)->Vector3 and clear_segment(from,to,map_id="house")->bool preserve old calls. Authoritative shared human integration: static step_human(actor:Dictionary,intent:Dictionary,dt:float,map_id:String="house")->void. Intent contains local move:Vector3,yaw,pitch,sprint,crouch,jump. Network owns sequence validation/freshness for waiting actors and always integrates zero intent after staleness so gravity lands.

Prototype values: walk3.1m/s,run5,crouch1.55,jump impulse4.6m/s,gravity12m/s². Human collision radius0.60m,height1.95m,crouched1.40m. Sprint is overridden by crouch. Jump is grounded rising-edge only; holding does not auto-repeat. Integrator substeps, clamps finite input, collides against selected bounds/furniture, lands on surfaces and blocks standing if there is no headroom. These are balance hypotheses.

HumanPose.sample(actor)->Dictionary supplies local torso,head,eye,pelvis,hip_l/r,knee_l/r,ankle_l/r,shoulder_l/r,elbow_l/r,hand_l/r, plus head_basis:Basis and torso_height. It depends only on replicated motion and swing, never local wall clock. zone_pose(actor,zone)->{p,normal,label} gives world contact and outward normal; BODY_ZONES include bone. Visual body and private marks use these points. Hands clap together with normalized cooldown pulse; equipped tools animate the right arm. Human movement runs before insect projection regardless of peer IDs.

MapNavigation.path(from:Vector3,to:Vector3,human:bool,map_id="house")->PackedVector3Array uses 94 authored nodes /126 links, cached per-role valid edges and A*. Human points are feet; mosquito points are flight centers. can_travel checks clearance and human floor support; no flying through slabs or walking across unsupported holes. Bots follow routes and repath around 1 s, not every frame. Both stairs are physical up/down routes and each floor remains connected if one stair route is omitted.

## Simulation API

GameSimulation extends RefCounted:
- DEFAULT_CONFIG: mode,map_id,human_count,round_seconds,blood_goal,rotation_seconds,respawn_seconds,mosquito_lives,task_interval,task_deadline,task_work,task_penalty,task_floor,task_goal.
- static sanitize_config(requested:Dictionary)->Dictionary; validate_roster(players:Dictionary)->String for already assigned roles and spare zone capacity.
- start(players:Dictionary,config:Dictionary)->void.
- submit_input(id:int,seq:int,move:Vector3,yaw:float,pitch:float,interact:bool,sprint:bool=false,crouch:bool=false,jump:bool=false)->void. Old six-argument callers remain valid. Human vertical move ignored; mosquito forward includes yaw and pitch, with optional auxiliary vertical input. Arena.step_mosquito accelerates 13m/s², brakes 28m/s², speed 3.8m/s, radius 0.04m. Releasing forward brakes. Finite inputs and increasing sequence required; stale input stops movement.
- action(id:int,seq:int,verb:String), with separate sequence: bite,attack,self_swat,perch,pickup,drop.
- step(dt), public_snapshot()->Dictionary, private_for(id)->Dictionary, abort(reason).

Snapshot: phase,map_id,elapsed,time_left,config,blood,winner,reason,tasks_done,task_goal,actors,pickups. Public actor allowlist: name,role,p,yaw,pitch,state,alive,swing,bitten,threatened,tool,lives,appearance,velocity,grounded,sprinting,crouching,crouch_amount,motion_phase,motion_speed. Never serialize input bookkeeping, assignments, reservations, private zones or calendars.

Pickups int->{tool,p,yaw,holder}; holder0 means unheld. TOOL_STATS lives in simulation.gd; PICKUP_SPAWNS remains a house compatibility alias. Authored pickups now belong to MapCatalog. Humans start with hands; pickup/swap/drop and hits are authoritative and atomic.

Mosquito private: {assignment:{human,zone,p,normal,label,revision},focus:{state,progress,distance,can_focus,reason},state,respawn_left,lives}. focus.state is idle/ready/charging/blocked/attached. Only the owning insect sees charge/assignment data; public threatened is a generic human warning. Human private: {task:{name,station,p,remaining,progress,work},deadline,failures}. Dead insects have empty assignment; no rotation countdown.

Held bite input concentrates for 1.2 s within 1.6 m with facing/exterior/LOS validation and assists the approach. Release before attachment cancels. Attached bite requires a new press to detach; release alone does not detach. Preparation 1 s, extraction 0.8/s per insect with total team cap 1/s, unchanged configured quota 12 by default. No hidden quota autoscaling. Hand reach 1.7 m/radius 0.34, strike active window 0.08–0.25 s. Q anticipates attachment and selects head/shoulders at pitch>=-0.25, torso at>=-0.85, legs below; rear needs a teammate.

Fixed per-mosquito rotation phase. Bite attaches to animated body and preserves its zone across scheduled rotations. Only explicit bite action detaches, assigns a new reservation immediately and preserves the calendar. Single human uses all16 front zones defendable with initial hands and aimed-band self-swat; multiple humans also permit six cooperative rear zones. Owner cannot self-swat rear, teammate must aim from exposed side. Flight/body collision follows crouch and jump.

## Modes and round states

Blood: shared progressive quota, persists on detach/death; one life/no respawn. Quota wins mosquitoes; timeout or elimination wins humans.

Survival: one life/no respawn, any survivor at timeout wins mosquitoes; elimination wins humans. No hunger or required bite.

Sleep, visible Tareas: default3 personal total lives, configurable1..9. Death consumes one; respawn after delay only with remaining lives. Temporary team death with remaining lives does not finish. No total lives means immediate human victory. Otherwise collective task goal evaluated at round close. A miss reduces only that human's future deadline; task progress persists through interrupted work, bite pauses it.

Disconnect during play aborts without winner to lobby. Reconnection cannot restore interrupted round. Departure after settled results does not erase winner. Round start resets blood/tasks/lives/tools/movement/marks.

## Practice session

PracticeSession is local in-process authority, with selected human/mosquito and blood/survival/sleep. Human faces two mosquito bots; mosquito faces one human bot. No ENet peer, hosted lobby or social role selection is involved. Both perspectives use the same GameSimulation and client rendering.

start(role,mode,cosmetics,display_name="Vos",config_override={}), restart(),stop(),send_input with matching optional locomotion flags,send_action,advance(dt). Signals snapshot_updated and private_updated match client consumption. Snapshot adds practice=true and bot_ids for presentation.

BotBrain.decide(snapshot,own_private,dt) consumes public observations and that bot's own private data only. Human AI navigates, reacts/aims, self-defends, picks tools and works on tasks; insects approach their own marks, bite/detach or avoid danger in Survival. Bots issue normal validated intentions/actions and are identified as bots. The separate ENet harness is diagnostic tooling.

## Cosmetics, typography and navigation

Cosmetics.sanitize(data:Variant)->Dictionary contains human/mosquito profiles {color:int,accessory:int}; appearance_for(data,role) returns only the chosen profile. Invalid values fall back; extras omitted. Six colors/three accessories per role. Cosmetics never affect statistics or hitboxes.

Preferences saves local settings and both appearances. Product rename migration validates and copies legacy Dejame dormir/preferences.cfg only if new preferences do not exist; old source is unchanged. Existing new profiles are preserved. Bangers is the comic title font and Atkinson Hyperlegible the body font; both have bundled SIL Open Font License notices.

UI primary flows: PRÁCTICA,CREAR SALA,UNIRME CON INVITACIÓN,TU PINTA,Ajustes. Signals include practice_requested(role,mode),practice_restart_requested plus social connect/ready/config/start/rematch/leave and customization/preview/walking. No social role selector.

Esc/Volver closes the active layer and restores focus. Key capture consumes Esc as cancel first. Lobby walking restores panels with Esc. In-game menu blocks local intentions and releases mouse; **simulation continues in both practice and online**.

Controls: human WASD/mouse,Shift sprint,Space jump,Ctrl crouch,LMB attack,Q band defense,R pickup,G drop,heldE work. Mosquito W toward aim in 3D, release brakes, A/S/D relative,Space/Ctrl optional altitude,E held focus/cancel on release before attachment/new press detach when biting,F surface perch. Remapping and sensitivity per role persist.

Task timing limits share GameSimulation.TASK_TRAVEL_RESERVE=21.0 and static minimum_task_deadline(work:float)->float = clamp(work,1,8)+21.0. Both task_deadline and task_floor use this minimum; task_interval is at least max(15, minimum+0.5). Default work3 therefore gives floor24 and interval minimum24.5; configured deadline30/interval36 remain unchanged. This explicit reserve accounts for travel across the larger house even while walking. The server never secretly extends an individual assignment. UI reads the same authority and restores valid received values when old range limits were stricter.

static minimum_round_seconds(config:Dictionary)->float returns 30 for blood/survival; for sleep, max(30,ceil(3+(human_count-1)*1.5+minimum_task_deadline(task_work)+0.05)). sanitize_config and UI use this shared limit. A new task is only offered when the remaining round permits work+21 s; its initial remaining time is capped to the remaining round. The opportunity count uses that same cutoff. Automatic goal policy remains ceil(2/3 opportunities), now 3 opportunities per human and a collective goal equal to 2 per human under defaults. Cadence and personal penalties do not change.

Candidate defaults: round_seconds=120, blood_goal=12, rotation_seconds=14, task_interval=36, task_deadline=30, task_work=3, task_penalty=2, task_floor=24, task_goal=0 (automatic 2/3 opportunities), mosquito_lives=3, respawn_seconds=4. Values are prototype hypotheses; mode win/life rules unchanged.

Visual insect scale is approximately 65% smaller than 0.3; human body 15 pieces,22 candidate zones, styled materials and furnished rooms.

## Verification handoff

Source checks confirmed: rules 3256, maps 431, routes 303, focus/combat 277, locomotion 16013, native visual 32, privacy 13, poses 33, UI 77, migration 13 all passed. Final 0.4 EXE validation, captures and hashes remain PENDING integration; distribution/PRUEBAS.md and BUILD.txt are the closure record. Historical 0.1–0.3 binaries do not certify 0.4. Real Internet/multiple PCs, hardware and human balance remain unvalidated.

Packages: Let-me-sleep-0.4.0-Windows.zip and Let-me-sleep-0.4.0-fuentes.zip; executable Let-me-sleep.exe. Protocol 4 rejects 0.3; invitation envelope remains DD3 version 1. Preserve all history.
