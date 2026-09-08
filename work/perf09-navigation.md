# Navigation path attribution and exact distance reuse

Status: measured, differential passed, runtime frozen. Motor and CPU released.

## Captured workload

`performance09_profile.gd --profile-map=house-v1-1 --seconds=12 --trace-paths`
ran the real Main/Client/Practice with 16 actors, 15 live brains, 1080p,
Compatibility, 60 Hz physics and a two-second warmup. The input scenario and
periodic door commands match the existing performance fixture. This was an
instrumented source run, not a clean release FPS benchmark.

The trace stores the exact numeric origin/destination passed to
`BotBrain._path_direction` only when production `stats.paths` increments. In
that branch production calls `MapNavigation.path` with those same arguments.
There are 171 captured requests: 168 mosquitoes and 3 humans. Vector3 values
are JSON numeric triples, not formatted display strings. Map fingerprint:
`1b34a7206283c854a1fae77ef36c6b8e8d06af30e31044edd35d7d1c842621d9`.

The source engine process exited 0 after 18.728 s with an empty stderr and no
fixture failures. Evidence: `perf09-path-trace.json/.log/.err/.run.json`.

## Baseline attribution

The frozen reference source is the original MapNavigation SHA-256
`229fd8b9836259a1ff36e8ffc5553418050a7bb3140fd368af7d6a456bd6c265`,
with only `class_name MapNavigation` removed. Reference file SHA-256:
`76c68602dfe5df8edb440c62263d61659dc6d5590ff96f7d8e03f73959522761`.

The source-only diagnostic decorates an in-memory copy with nested timers.
The A* body is copied without changing its statements into a test-only
function; its arguments, strict comparisons, queue order and reconstruction
stay identical. All output routes are compared exactly against the untouched
reference and production. Three replays yielded 1,374 checks, zero failures,
exit 0, empty stderr, 6.072 s process time.

| Scope | Mean per full query | Share of total |
| --- | ---: | ---: |
| Full path | 2.813 ms | 100% |
| Both connections, inclusive | 2.365 ms | 84.1% |
| Connection segment predicates | 1.223 ms | 43.5% |
| Connection residual: sorting, filtering, timer overhead | 1.141 ms | 40.6% |
| A* setup, search and reconstruction | 0.319 ms | 11.3% |
| Initial direct segment | 0.104 ms | 3.7% |
| Warm graph lookup | 0.003 ms | 0.1% |

The A* mean among the 498 calls that reached search is 0.328 ms; the table
divides by all 513 replayed queries. Inclusive scopes overlap and must not be
summed. The connection residual is not an isolated sort measurement: it also
contains iteration and profiling bookkeeping. Graph preparation timings are
recorded separately and are not an A/B comparison, since the first reference
also triggers shared map generation.

Uninstrumented baseline ABBA mean full-path times were 3.074 / 3.142 / 2.893 /
2.849 ms. Both implementations were identical then; this spread documents
short-run noise. Evidence: `perf09-path-baseline.json/.log/.err/.run.json`.

## Candidate

`_connections` now computes each node's squared distance once into a local
`PackedFloat64Array`, then uses the same `sort_custom` with strict `<` and the
same initial node order. It replaces repeated dictionary lookups and distance
calculations inside the comparator. No quantization, persistent cache, graph
changes, geometry changes, new tie breaker or gameplay cadence changes.
The array belongs to one exact call, so there is no cross-query invalidation.

## Post-change result

`navigation09_connections_test.gd`: **5,896/5,896 passed**, 18.391 s, exit 0,
empty stderr. It compares exact graph edges, node connections and full routes
for house, generated seeds 1 and 2, lobby, both roles, each graph node with
small offsets and same-floor boundary offsets. Additional synthetic graphs
exercise equal-distance ties, repeated nodes and tiny asymmetric offsets.

`navigation09_path_profile.gd --instrument-current`: **1,374/1,374 passed**,
5.369 s, exit 0, empty stderr. It replays the same 171 pre-change queries;
all routes remain exactly equal to the frozen reference. Both parse checks
were also clean. The two fixtures make 7,270 post-change checks in total.

| Uninstrumented ABBA pass | Mean path | p90 | p99 | Maximum |
| --- | ---: | ---: | ---: | ---: |
| Reference A1 | 2.845 ms | 3.302 ms | 4.529 ms | 9.028 ms |
| Candidate B1 | 2.234 ms | 2.604 ms | 3.554 ms | 9.064 ms |
| Candidate B2 | 2.204 ms | 2.525 ms | 3.498 ms | 9.543 ms |
| Reference A2 | 2.877 ms | 3.257 ms | 4.968 ms | 9.244 ms |

Across the two passes per implementation, mean path time falls from 2.861 to
2.219 ms (**22.4%**). The maxima around 9 ms remain; this does not establish
that all spikes are solved. No global FPS gain is inferred from this replay.

The post-change instrumented connection residual falls from 1.141 to 0.546 ms
per full query (52.2%). Connection segment calls remain exactly 6,930 across
three repetitions; search calls remain 498. Their means remain approximately
1.27 ms per full query for connection segments and 0.335 ms per invoked search.
This supports the narrower cause: repeated comparator work was removed;
the collision and search algorithms were not accelerated or approximated.

Evidence: `perf09-navigation-connections.json/.log/.err/.run.json`,
`perf09-path-optimized.json/.log/.err/.run.json`.

Frozen runtime SHA-256:
`0afa857f13329ed742b6a9c0d72513465475013e68bc84b1d6411c76d535068f`.

## Handoff and limits

Runtime file: `game/scripts/map_navigation.gd` only. `navigation_geometry.gd`,
DoorGeometry, generator, catalog contents, physics, bot cadence and destinations
are unchanged. All reference predicates and strict sorting comparisons remain.
No new persistent cache was introduced, so the existing geometry invalidation
and cache bounds are unchanged.

Tests: `game/tests/navigation09_reference.gd`,
`game/tests/navigation09_connections_test.gd`,
`game/tests/navigation09_path_profile.gd`; tracing option added to the existing
`game/tests/performance09_profile.gd`. The path profiler explicitly requires
source files to construct its instrumentation; it is not an EXE gate. The
connections differential preloads a portable reference under `game/tests`.

The live request sample contains few human requests, so it is not a balanced
CPU workload by species. Exact human behavior is additionally covered by the
multi-map differential. The finite replay does not prove universal numeric
equivalence; the code change preserves the exact distance values and comparator
semantics for every query and only avoids repeated computation of those values.
