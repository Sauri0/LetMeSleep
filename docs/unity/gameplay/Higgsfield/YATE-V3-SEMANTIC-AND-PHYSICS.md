# Yate v3 — semantic navigation and native circulation

Worker Código / Gameplay, 2026-09-13. Unity6000.3.24f1; external fixtures
reference only integrated central assemblies, excluding frozen alfa sources.

## Source

- Map `hf-yate-a-la-deriva-v3`; original ContentHash
  `e7d8582abe2051468531520905b68f28b55696fb69e5c8705b6c0e38910a43d2`.
- FBX `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/04-yate/NormalsV3/HF_MAP_04_yate_UNITY_V3.fbx`.
- Source SHA256 `217774581132711b6197a6115954e6e63acd3fdbf42b2705c0c115e6bc146870`.
- 96 native static colliders, five human and16 mosquito spawns.
- Actual tread centers extracted from horizontal source faces:15 interior,
  16 aft-to-flybridge,14 swim-to-aft. No invented staircase or connector surface.
- Schema revision02:25 semantic zones,27 portals, all16 mosquito spawns covered.
  Under-stair spaces connect separately from flight paths above the treads.

## Native diagnosis

Root: `N:/LetMeSleep/Validation/Higgsfield/YateV3-20260913`.
Both `preparation-01` and `preparation-02` found support for every human probe.

`semantic-native-01/map-checks-20260913-082309-744.json`:37/49 cases PASS,
28/28 portal clearances PASS and21/21 spawns PASS. Four manual flights passed.
Human interior/flybridge stairs passed in both directions. Failures identified:

- Port manual route intersected DeckStorage00; corrected to go around the
  transom bench and outside the box. Starboard foredeck target crossed the
  tapered rail contour; corrected using actual deck polygon coordinates.
- Nine Runtime patrols stalled at graph connections from above a stair to a
  point beneath its treads. Removed the aft under-stair link and connected lower
  foyers around the foot of the interior stair. No steering/motor change.
- DeckStorage01 and Lid01 occupied the swim stair's human volume. Coordinator
  explicitly authorized moving both together on supported, unobstructed deck.

`storage-native-01/map-checks-20260913-082944-913.json`:48/49 cases PASS,
27/27 portals PASS,16/16 actual Runtime patrols PASS. Both manual side-deck routes
now pass. The remaining swim-stair case advances past the relocated chest and
stops at a requested tread position intercepted by Lower_EndBulkhead_-11.6.

## Chest relocation candidate

`YateStorageCandidate` moves both baked source transforms from world(0,0,0)
by **(-1.4,0,+2)m**, leaving their relative pose, rotations, scales, shared meshes
and materials unchanged. The resulting chest is centered near(.8,3.78,-9.8).
Its combined conservative box has no penetration against any other map collider.
Five footprint samples have native physical deck support and actual visible
teak support. Visible gap is at most0.00024mm; physical deck is34mm below the
teak surface, matching the authored original arrangement. The temporary teak
collider used for these queries is destroyed before the actual gameplay suite.

Original FBX GUID `525211a2994914d41a9ad59f07ba51ef`, original mesh local IDs:
base `-5318090365993279969`, lid `3486938035666019765`. Native receipt includes
material GUIDs, unchanged orientation/scale, before/after transform positions and
all five support points. This exact relocation is now persisted in prefab and scene.

## Confirmed obstruction and authorized repair

Measuring the actual bulkhead support with a sphere allowed complete descent,
but ascent still stopped at27/33 points: feetY2.74256 against wall topY3.18.
Evidence: `swim-sphere-native-01/map-checks-20260913-084127-930.json`.
The obstruction was real, independent of the initial floor sampling error.

Coordinator authorized a local notch in `YATE_Lower_EndBulkhead_-11.6`:
remove material above UnityY2.70 inside X[1.625,3.075], retaining original
Z[-11.67,-11.53] thickness, outer wall, material and stair. Both visible mesh
and MeshCollider use the same repaired mesh. No invisible collision proxy.
The source12 triangles become28; removed volume0.09744049m3.

- Original mesh SHA256 `1f446c88d527823d90e23e45d27735b091f3b75becd481c80b6c3f1f8049791f`.
- Repaired mesh SHA256 `61fe46b11272eda3f4521d6e230a6ecdee79747c2b7ae6a5cadd38f0ce0b970e`.
- Reduced original-waypoint swim route:33/33, max penetration0.
  `bulkhead-reduced-native-01/map-checks-20260913-084848-545.json`.
- Full candidate:49/49,27/27 portals, maximum penetration0.7205009mm.
  `technical-final-native-01/map-checks-20260913-085137-720.json`.

## Applied and verified

`applied-01/yate-semantic-apply.json` reports `APPLIED_READBACK_PASS` for
both prefab and scene. Original recipe SpatialData provenance is retained.
The unchanged-content stamp is identical before/after in both assets:
`82309809dc8e0f76496cdd9dd2536822bffa84a6d106711e0beaae705c12f223`.
It excludes only the authorized chest positions and wall mesh references;
those changes have separate exact pose/mesh/material readback guards.

- New ContentHash `dee1fea0f5f21429ed434668ee408a6ec3efd67984ad24e0640faa1177ca20d6`.
- Navigation SHA256 `fb3054eeb29ce855f32ba6293268fc28d24b0b68da65f6baa8124c171e24142a`.
- Map Data: `yate-navigation-schema1.json`, `yate-swim-bulkhead-v1.asset`,
  `yate-technical-revision-v1.json`, all with Unity metas.
- Regression from persisted prefab, no navigation/geometry overrides:
  **49/49 PASS**,27/27 portals,21/21 spawns,8/8 human routes,4/4 manual flights,
  16/16 actual Runtime patrols. Maximum penetration0.7205009mm, no errors,
  cleanup true. Evidence:
  `applied-regression-01/map-checks-20260913-085904-039.json`.

Mapas received the final Blender equivalent: chest+lid translation(-1.4,+2,0),
and wall removal X[1.625,3.075], BlenderZ>2.70. Source-copy work is separate
from these persisted Unity assets and does not authorize reimport.

All reported tests are native headless queries/authority integration, one tested
actor plus a distant opposite-role actor. Runtime exploration is20s per spawn,
with actual10Hz BotController/SteerBot and30Hz authority. No combat, WAN, simultaneous
21-player load, visual quality or performance acceptance is inferred.
