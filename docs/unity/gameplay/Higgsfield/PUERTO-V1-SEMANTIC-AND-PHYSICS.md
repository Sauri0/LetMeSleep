# Puerto v1 - semantic navigation and exterior stair repair

Worker Codigo / Gameplay, 2026-09-13. Unity6000.3.24f1 external fixture,
using integrated central assemblies only. Frozen alfa WIP remains untouched.
Evidence root: `N:/LetMeSleep/Validation/Higgsfield/PuertoV1-20260913`.

## Original source

- Map `hf-puerto-del-faro-v1`:458 static colliders,5 human and16 mosquito spawns.
- FBX SHA256 `aa4bb192353bfbebe5f3d48e4dd6950245b854f397b95a7fb7d7ac85a2a834a4`.
- Original ContentHash `3384ca697cd6f09501fc012c3993fb6acd59411ee0cc11a9a63753e5016adfa2`.
- Source `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/05-pueblo/HF_MAP_05_pueblo_UNITY.fbx`.

## Navigation by Modelador Terreno y Mapas

Final proposal v4:47 regions,57 portals, SHA256
`86c770ee8175e55490812bcf4b6a8040f6cd0a6ba33c06ade7fb52cc84d56e8d`.
It resolves first-containing-region ambiguity at overlapping street/house boxes,
separates harbor west/bridge/east, and uses physical helical segments in the
lighthouse. All114 portal ends match the runtime first matching region offline.

- v3 native `semantic-native-01/map-checks-20260913-084257-741.json`:
  42/49 cases,53/53 portals. All8 human routes completed, but the exterior stair
  exceeded2mm penetration. Six Runtime patrols did not leave their initial region.
- v4 native `v4-native-01/map-checks-20260913-085945-259.json`:
  48/49,57/57 portals,21/21 spawns,16/16 Runtime and4/4 manual flights PASS.
  The physical stair failure remained identical. Human coverage: village/cottages,
  workshop doorway/boat bypass, footbridge both ways, both pier stairs,
  exterior lighthouse stairs both ways and56-tread internal ascent.
- Rocks are excluded only as waypoint support surfaces; all retain their original
  colliders during native traversal. The footbridge routes pass both ways.

## Measured defect and authorized repair

The original exterior-stair route completes57/57 points, but peaks at
11.0744527mm penetration at tick228, feet Unity(17.620203,4.93414068,14.6918182).
Collider: `Environment/Lighthouse_Approach_StoneStairway`. The lower capsule
sphere touches the front upper edge of the second tread. Exact-source triangle
analysis predicts11.0759651mm, within0.0016mm of the native result, recorded in
`exterior-stair-contact-source.json`. Original evidence is preserved.

Coordinator authorized a30mm bevel only on this edge, with visible geometry and
collision sharing the same mesh. Route, motor, human radius and2mm threshold
remain unchanged. Source originals remain untouched.

- Unity edge endpoints:(17.4654388,5.05693865,15.1861744) and
  (19.08456,5.05693865,13.2813263); inward:(0.7619393,0,0.6476484).
- Within the second tread only, clip where
  `Y - 5.05693865 - dot(P-edgeStart,inward) + 0.03 > 0`.
  New diagonal cap retains original front-riser material.
- Exactly12 original triangles selected.468 other triangles retain original local
  positions, normals, tangents and material assignments. Whole mesh480 to492
  triangles. Outer bounds unchanged; no additional collider or invisible proxy.
- Native mesh has896 generated tangents despite no source UV/color layers.
  Unchanged tangents are copied; new faces receive an orthogonal tangent basis.
  Initial diagnostic guard stopped before traversal until preservation was added.
- Original mesh SHA `590d5afb1bab0e206525127a5c6cf1425ce57612005448b0673b46d48ca9aec2`.
- Repaired mesh SHA `9095d590b03a6d9be01a22adb40c1be388ef9201e0caf85a788bed4907bb54d9`.
- Original FBX GUID `dc7b6cc7e78f7b844b07f36a67609fc1`, mesh localID
  `5135238771083559522`.

Reduced `bevel-reduced-native-02/map-checks-20260913-090439-949.json`:57/57
points PASS, maximum0.857829931mm, no errors. Its report remains INCOMPLETE
because spawns/portals/other routes are intentionally omitted.

Full `technical-final-native-01/map-checks-20260913-090551-119.json`:
49/49 PASS,57/57 portals, maximum0.857829931mm, no errors, cleanup true.

## Final persistence and regression

`applied-01/puerto-semantic-apply.json`:APPLIED_READBACK_PASS prefab and scene.
Original import recipe provenance is retained in Data. New ContentHash:
`3be1751178d5df73b75b089c315e617b8935466bf7814866c502824db9787fa9`.
Navigation GUID `f3fe41654cd52da4b8b127dedd298b87`.

Unrelated-content stamp is identical before/after in both assets:
`45298f16bc0d10fee15f7df966522a73dbf026dc605565d51c4d15470e3a0b3a`.
Only the authorized stair mesh reference is excluded from that stamp; exact
original/revised geometry hashes have separate readback guards. All transforms,
renderers, material references and other collider properties are included.

Data assets: `puerto-navigation-schema1.json`,
`puerto-exterior-stair-bevel-v1.asset`, `puerto-technical-revision-v1.json`,
all with Unity metas. Prefab and scene reference these saved assets.

Final persisted regression, no navigation/geometry overrides:
`applied-regression-01/map-checks-20260913-090832-888.json`.
**49/49 PASS**,57/57 portals,21/21 spawns,8/8 human routes,4/4 manual flights,
16/16 actual Runtime patrols. Maximum0.857829931mm, zero errors, cleanup true.

Mapas received the final receipt and Blender-equivalent clipping plane for a
separate editable source copy. No source replacement or reimport authorized.

## Scope

Native headless EditMode geometry queries and actual authority at30Hz. Runtime
patrols use GameplayRuntime/ObserveBot/BotController/SteerBot at10Hz with authority
at30Hz for20s per spawn. One actor is tested at a time with a distant opposite-role
actor. This does not certify combat, simultaneous21-player load, WAN, rendering,
frame rate or complete arbitrary-path physical coverage.
