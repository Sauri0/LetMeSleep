# Campamento v2 — semantic navigation and physical review

2026-09-13, Worker Código / Gameplay. Native execution uses Unity 6000.3.24f1
and the integrated central motor; frozen alfa worktree sources are excluded.
Only the coordinator's repaired v2 is admissible for these conclusions.

## Source and candidate

- Map: `hf-campamento-pinar-v2`.
- FBX: `NormalsV2/HF_MAP_03_campamento_UNITY_V2.fbx`, SHA256
  `3fd036eb8ede448aa48eb72764ae9f143dbf6ecff375e1ae10d144dfb44badc3`.
- Artifacts root: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento`.
- Original content hash:
  `d9b4ff479990e28d34bd14d1e5b18ceaceb687fc5b95d03efa2fe669f6dd62af`.
- Native import: 501 active colliders, 5 human and 16 mosquito spawns.
- Semantic candidate: 20 zones, 25 portals. Camp clearings, two tent clusters,
  shelter, rotated washroom entry, both bridges, creek and lake air connect.
  Tent zones begin above canvas; creek/lake zones describe flight, not human floor.

`Build-CampSemanticProposal.py` verifies source hashes and writes the schema1
candidate and bounded routes. `HiggsfieldCampPreparation` measures human waypoint
floor heights on an owned clone. The authored probe Y is not an assumed floor.
The coordinator accepted one explicitly recorded strict-test exception and
authorized persistence. Prefab and scene now reference the measured revision
described below; the contract threshold remains unchanged.

## Initial native evidence

Evidence root: `N:/LetMeSleep/Validation/Higgsfield/CampV2-20260913`.

- `preparation-01/camp-v2-preparation.json`: all human probes found eligible floor.
- `semantic-native-01/map-checks-20260913-074953-500.json`: **41/49 PASS**,
  no fixture errors, cleanup true. All **25/25 portal clearances PASS**.
- All **16/16 actual Runtime patrols PASS**: 600 authority ticks / 20 seconds
  each; real BotController/SteerBot at 10Hz, authority at 30Hz. Distance
  37.400–37.813m, union 17 of 20 zones, longest still streak 1 tick, no lost
  navigation streak. This does not establish every graph edge or long sessions.
- All four manual mosquito flight routes passed.
- `water-playmode-01/results.xml`: **1/1 PlayMode PASS**. Native water receipt
  `UnityFinalV2/water-playmode.txt` confirms movement and restoration for all
  15 authored water/foam objects. No rendering/performance claim.

| Initial physical failure | Result |
|---|---|
| Human05 spawn | 28.829mm initial capsule penetration against Paths |
| Plaza north | 8/8 points reached; 12.534mm transient Paths penetration |
| North tents | 6/6 reached; 12.841mm transient Paths penetration |
| South tents | First manual waypoint obstructed by Crate02 |
| Shelter | Manual lane obstructed by CookTable; earlier Paths peak 15.866mm |
| Washroom approach | Manual lane obstructed by CookTable |
| North bridge return | 19/19 reached; initial spawn defect and 56.924mm Paths peak before bridge |
| South bridge approach | First manual waypoint obstructed by Crate02 |

Revision02 reroutes only the four furniture-obstructed human routes. The three
completed routes with path penetrations remain unchanged for comparison. Original
input/config/source copies remain in immutable native evidence directories.

## Confirmed source contact and bounded experiment

`Inspect-CampPathEdges.py` reads the exact v2 FBX and correlates recorded peaks.
Paths comprise 320 positions / 149 quads in 11 flat ribbons at Unity Y=.04;
grass top is Y=-.035. Five recorded peak overlaps match the lower capsule sphere
against the sheet's perimeter within **0.001579mm** analytical/native difference.
This identifies the contacted source feature; it does not isolate the internal
motor sweep/projection that generated the overlap.

Spawn05 is outside the path at its center, but the radius .25 capsule reaches
the sheet edge .121210m away in XZ. At fixed authored XZ, the analytical minimum
feet Y is .008651, versus original -.025. A center ray to grass is insufficient.
The native helper surveys sphere support, adds 3mm vertical clearance, then checks
the full 1.72m capsule against every map collider. Changes are clone-only.

Coordinator-authorized experiment `camp-paths-exact-down-extrusion` retains all
original top vertices and triangle indices. It adds reversed bottom faces at
grass Y=-.035 and outward sides only on the source boundary edges. It preserves
all 11 components and their concavities in the original nonconvex MeshCollider;
no convex enclosure or connections across gaps. The MeshFilter and materials
retain original references. Receipt records original asset GUID/path, old/new
geometry hashes, revision, component/edge counts and preservation checks.

The fair A/B uses the same revision02 routes and corrected Spawn05 on both sides:
`original-corrected-native-01` passes 43/49; `closed-paths-native-01` passes 48/49.
Every human route reaches every waypoint. Original sheet peaks are 12.534,
12.841, 3.255, 12.163, 41.594 and 53.871mm. The closed candidate leaves only
2.32617534mm on north-tent circulation at tick192, position
(-11.15747,.0215819068,17.5604782). `closed-paths-repeat-01` repeats that exact
value, tick and position three times; every run reaches6/6 points and ends at
zero penetration.

The 2mm limit is explicitly documented in central `docs/unity/gameplay/INTERFACES.md:191`,
under the initial revisable balance/tolerance profile. It is not merely an
arbitrary fixture threshold. This contact exceeds that contract by .32617534mm.
Motor Skin remains1mm; native collider contactOffset is10mm and cookingOptions30.
ContactOffset is not a penetration acceptance tolerance and does not justify PASS.
Samples188–195 rise continuously through this contact; the route does not stall.
Headless evidence cannot establish whether a visual artifact is perceptible.

On2026-09-13 the coordinator explicitly accepted this measured exception for the
catalog integration and instructed us to preserve the strict failure. No further
motor, geometry or tolerance modification was made. A read-only motor trace was
compiled but never executed or retained in the delivered sources after that
decision; the exact internal sweep/snap origin remains unisolated.

## Persisted result and final regression

`applied-01/camp-semantic-apply.json` reports
`APPLIED_READBACK_PASS_WITH_RECORDED_STRICT_EXCEPTION` in both prefab and scene.
`ApplyCampSemanticNavigation` guards the exact48/49 report, accepted failure,
25 clear portals, hashes and original content before saving through Unity APIs.

- Final content hash:
  `fc9399387e8a9d4bcae5f4a70e3291233170f54d92383d63a9a8b5cb648ad168`.
- Navigation SHA256:
  `c51fa78d0f4203d38c6e0eeb0713da48fb0772d6437aad5971b6276b878aa9bb`.
- Navigation GUID: `9c1cf8ff13207694f84b6176aa364564`.
- Closed collider GUID: `832c19931529fa044a43b69dce85fc6c`.
- Original collision mesh SHA256:
  `3a3850bd72bb1aacaa5b7455b74b607540d2f96c0b32e5f8ef28eb4bad41df14`.
- Closed collision mesh SHA256:
  `43572715161070e52e41da24c078d672421ec3c694f0daa868b0c339855f0239`.
- Spawn05: (29.8,-.02499937,15.4) → (29.8,.011650065,15.4).

Saved files beneath the map's `Data/` folder are
`camp-navigation-schema1.json`, `camp-paths-closed-v1.asset` and
`camp-technical-revision-v1.json`, each with a Unity-generated meta. The provenance
retains original FBX mesh GUID/path/hash, original recipe reference/hash,
new collision hash, spawn measurement and the strict failure decision. The
visible MeshFilter still references the original FBX mesh. The closed collider
has640vertices/1236triangles, including all298 original top triangles,11components
and320original boundary edges.

All unrelated transforms, renderer materials, visible mesh references and
collision configuration match before/after in prefab and scene, stamp
`2840a23b525a1f44dd0d5fa112f9f17dfc701fd54197b401cca61e66b519898a`.

Final native regression with **no navigation, collider or spawn override**:
`applied-regression-01/map-checks-20260913-081340-818.json` preserves **48/49**, the
same sole2.32617534mm peak, **25/25 portal PASS and16/16 Runtime PASS**, no fixture
errors and cleanuptrue. It uses the saved new ContentHash and prefab SpatialData.

`preparation-02/camp-v2-preparation.json` also records25native `LGT_*` transforms:
all23review anchors plus `LGT_Moon` and `LGT_Moon_Fill`. Light configuration remains
the Presentation owner's responsibility.

## Scope

Each physical test has one tested actor and a distant opposite-role actor.
The 5/16 pools do not mean 21 actors were tested simultaneously. Acceptance
requires initial, transient and final overlap <=2mm, finite states, in-bounds
positions, grounded human settling and route completion within 600 ticks.
No WAN, player render, combat, full-map coverage or target-hardware FPS is claimed.
