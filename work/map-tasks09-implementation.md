# Map identity and task travel authority

Source: `game/scripts/simulation.gd`; new fixture: `game/tests/map_tasks09_test.gd`.

`start` checks the original requested map identity before UI sanitization can
substitute `house`. Unknown, non-playable, malformed, or failed generation leaves
`phase=lobby`, a reason, no winner, and empty actors/pickups/doors. The previous
room and surface solver are discarded. Sleep additionally verifies a supported
route from the first human spawn to every station before clocks or actors start.

Only the actual map supplies public config metadata: `map_fingerprint`,
`map_generator_version`, `map_seed`. Authored house uses version/seed zero and a
fingerprint of its real catalog data. Client-provided metadata is ignored. Existing
`sanitize_config` behavior remains suitable for room UI defaults; transport must
pass the original chosen map ID to `start` and check its phase/reason.

Task policy `walkable-route-v1` uses MapNavigation's supported human route through
the selected map, including its floors, stairs, railings and object supports.
Horizontal route distance is divided by 2.635 m/s: ordinary 3.1 m/s walking with a
15% allowance for turns/alignment. A closing or closed oriented leaf intersecting
the route reserves one full opening, cooldown and alignment operation (2.0476 s).
Work plus 0.5 s margin completes the required budget, rounded up to 0.05 s.

The preferred station order remains deterministic. The first reachable station
that fits the personal deadline is chosen. If none fits, a reachable station may
receive an explicitly displayed larger budget, limited by the next scheduled task
minus 0.5 s and the round end. Route estimates do not change personal deadlines,
penalties, floor, fixed cadence, the conservative final-round dispatch cutoff or
the collective two-thirds goal. In the air or temporarily without a route, the
same dispatch stays pending and retries at most every 0.5 s. Expiring such a slot
does not lower the goal or add a personal failure. Player movement therefore
cannot manufacture an easier team target.

Private task data adds `budget`, `base_deadline`, `required_seconds`,
`route_meters`, `travel_seconds`, `door_count`, `door_seconds`, `extended` and
`rerouted`. Its existing `remaining` is the actual countdown. Human-only
`task_status.pending/reason` explains a deferred dispatch. No destination or task
timing is added to public actors or another role's private data.

The door interaction label also now uses the instance's map-specific definitions,
so generated door IDs do not index the authored ten-door dictionary.

## Executed source gates

- `map_tasks09_test.gd`: **86/86**, exit 0, `work/map-tasks09.log`.
- `task_deadline_test.gd`: **59/59**, exit 0,
  `work/map-tasks09-deadline-regression.log`; no changes to its expectations.
- Structured observations: `work/map-tasks09-results.json`.

Seed 1 (three floors): selected route 35.571 m, travel plus work 14.05 s within
24 s. Seed 2 (two floors): 41.675 m, travel plus work 15.90 s within 24 s. Both use
real Simulation movement/collision/task completion and ordinary walking. The
authored worst-route regression completes in 19.45 s within 24 s, and the default
round still finishes three tasks against a goal of two.

The extension branch is tested with an explicitly restricted one-station fixture
on the same real three-floor geometry, not a claim that generation emits only one
station: 84.15 m, visible budget 35.45 s, completed in 29.60 s without sprint. A
closed authored door is opened by a real aimed action before traversal/work. The
suite also checks every generated human spawn plus an upper-floor witness, bad
map identities, failed-generation fault injection, metadata spoofing, air-to-floor
retry, final-round cutoff, privacy and a non-reducible collective target.

The first fixture run exposed two fixture setup errors, corrected before the pass:
the door reach check ran before the body followed the newly aimed view, and the
late-slot fixture inherited explicit goal 99 while asserting automatic goal 2.
Neither correction changed production reach or task limits.

These are finite source tests, not an exhaustive seed corpus, network proof, or
claim of uninterrupted completion despite other players repeatedly blocking doors
or interrupting work. The model reserves one door operation per encountered
closed/closing leaf; subsequent player interference remains gameplay.
