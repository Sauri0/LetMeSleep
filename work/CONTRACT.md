# Integration contract — 0.2.0 / protocol 2

Project: game/, Godot 4.5.2 stable, typed GDScript. Shared coordinates in scripts/arena.gd: human position is feet; front is -Z. Agents edit only assigned files and preserve parallel changes. Current code and confirmed decisions below supersede the original planning proposals.

## Confirmed team and lobby rules

Native Windows client and headless ENet/UDP server in the user's PC; one registered room per server. Connection requires server address, UDP port (default 27840) and room code. Code alone does not resolve NAT.

The owner chooses exact config.human_count, integer 1..5. Everyone else becomes a mosquito. Both teams must be nonempty; 1v1 is valid. Technical limits: 12 mosquitoes and 16 total participants. Never clamp human_count to the number currently connected; report invalid combinations.

Lobby players have role="waiting". There is no user role selection. The server draws a fresh random roster for every round, with no forced alternation or memory of previous teams. Consecutive repeats are valid.

LobbyRules (scripts/lobby_rules.gd, RefCounted):
- static validate(players: Dictionary, config: Dictionary, require_ready: bool = true) -> String: empty means valid.
- static draw(players: Dictionary, config: Dictionary, rng: RandomNumberGenerator = null) -> Dictionary: deep-copied assigned roster with exact human_count; remaining players are mosquitoes; cosmetics/name/ready preserved.
- draw returns an empty dictionary on invalid capacity/config, and does not require readiness itself. The start gate calls validate with require_ready=true.
- Server supplies its RNG. Default draw creates and randomizes an RNG. Fisher–Yates operates on sorted peer IDs for deterministic injected-seed tests.

## Simulation API — scripts/simulation.gd

RefCounted GameSimulation:
- DEFAULT_CONFIG includes mode, human_count, round_seconds, blood_goal, rotation_seconds, respawn_seconds, mosquito_lives, task_interval, task_deadline, task_work, task_penalty, task_floor, task_goal.
- static sanitize_config(requested: Dictionary) -> Dictionary.
- static validate_roster(players: Dictionary) -> String validates already assigned roles, nonempty teams, limits and spare body zones.
- start(players: Dictionary, config: Dictionary) -> void receives the server-drawn roster.
- submit_input(id: int, seq: int, move: Vector3, yaw: float, pitch: float, interact: bool): local movement intent, yaw-only travel, finite inputs, bounded speed and stale-input stop.
- action(id: int, seq: int, verb: String): bite, attack, self_swat, perch, pickup, drop. Movement and action sequences are separate.
- step(dt: float), public_snapshot() -> Dictionary, private_for(id: int) -> Dictionary, abort(reason: String).

Public snapshot contains phase, elapsed, time_left, config, blood, winner, reason, tasks_done, task_goal, actors and pickups. Actor allowlist: name, role, p, yaw, pitch, state, alive, swing, bitten, tool, lives, appearance. Never broadcast assignments, target reservations, zone IDs or rotation schedules.

pickups: int -> {tool: String, p: Vector3, yaw: float, holder: int}; holder=0 means on floor, otherwise held by that human. TOOL_STATS and PICKUP_SPAWNS live in simulation.gd. Every human begins with hands. Pickup/swap/drop and hits are authoritative.

Mosquito private data: {assignment: {human, zone, p, normal, label, revision}, state, respawn_left, lives}. Dead players receive an empty assignment. Human private data: {task: {name, station, p, remaining, progress, work}, deadline, failures}. No visible rotation countdown.

Each mosquito owns a fixed rotation phase. Bite attaches relative to the body and keeps its zone through rotation. Only an explicit bite action detaches; releasing a held key never does. Detach gets another valid reservation immediately without moving the timer. One human permits only self-defendable front zones; multiple humans also permit cooperative rear zones.

## Modes

Blood: shared quota, progressive extraction preserved after detach/death. One life, no respawn. Mosquitoes win at quota; humans at timeout or total elimination.

Survival: one life, no respawn. Any mosquito alive at timeout wins for its team. No hunger or mandatory bite. Humans win at total elimination.

Sleep: default 3 personal total lives, configurable 1..9. Death subtracts one; respawn after configured delay only with remaining lives. All temporarily dead with pending lives does not end the round. Total exhausted lives awards humans immediately. Otherwise humans need the collective task goal at round close. A failed task reduces only that human's future deadline, leaving round length, task interval and other humans' deadlines unchanged. Bite pauses task progress while preserving it.

Disconnect during play aborts to lobby with no winner. Reconnection does not resume an old round. A departure after results must not erase the settled winner.

## Appearance and presentation

Cosmetics (scripts/cosmetics.gd):
- static sanitize(data: Variant) -> Dictionary returns {human: {color: int, accessory: int}, mosquito: {color: int, accessory: int}}.
- static appearance_for(data: Variant, role: String) -> Dictionary returns only the selected role's appearance.
- Six palette entries and three accessory options per role; invalid values default to zero and extra fields are omitted.

Preferences stores both role profiles locally. Lobby draw preserves both; simulation.start copies only the assigned profile to actors[id].appearance. Public appearance is a clone and cannot mutate authority. Cosmetics do not affect gameplay statistics or hitboxes.

World builds the furnished house, lobby avatars and customization preview. Shared Arena human radius is 0.60 m so all rotated marks plus attached mosquito clearance remain inside walls. Human view is first-person with own head hidden and body visible; mosquito view uses a colliding third-person camera. Private markers require line of sight and exposed body side.

Client controls waiting-room walking independently of match input. Lobby human avatars are presentation only and do not reveal future teams.

## UI and input behavior

UI signals include connect_requested, local_server_requested, cosmetics_changed, preview_requested, preview_closed, walk_requested, escape_requested, ready_requested, config_requested, start_requested, rematch_requested and leave_requested. No role_requested control is used in 0.2.0.

Core UI methods: show_home, show_status, show_lobby, show_game, show_results, set_pause, is_menu_open, set_lobby_walking. Server lobby data carries exact human_count, player readiness, owner, code and waiting actors.

Esc/Volver closes the active customization/settings layer and restores previous menu focus. During key binding, Esc cancels capture first. During lobby walking, Esc returns access to panels. During play, the menu releases mouse input but does not pause the online simulation. Settings/cosmetics persist locally.

Default controls: WASD, mouse; mosquito Space/Ctrl vertical movement, E explicit bite/detach, F surface perch; human LMB attack, Q aimed body-band self-swat, R pickup/swap, G drop, held E task. Sensitivity is separate per role; inputs are remappable.

## Verification state

0.2.0 executed: rules_test.gd 3242 checks / 0 failures; lobby_rules_test.gd 321 checks / 0 failures, Godot 4.5.2 headless. Network, export, 3D lobby/customization UI, focus and native presentation validation remain pending until the root records new results in distribution/PRUEBAS.md. Historical 0.1.0 outputs must remain preserved and are not evidence for a new binary.