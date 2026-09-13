# Contiguous surface traversal — second surface delivery

Branch `codex/gameplay-surface-continuity`; follows orientation commit `3ffc9e8`
based on central `a581a40`. Director authorized optional local capability
`ISurfaceTraversalWorld`, defined in GameplayAuthority.cs and implemented by
UnityGameplayWorld.cs. No wire DTO/schema or existing IGameplayWorld method
changed. Runtime changes in this commit are only those two files; bite, arm IK,
facial writer, camera and ragdoll work are untouched.

## Selection and motor behavior

Surface locomotion now shares the projected 3D aim/tangent basis. The previous
heading is transported when the support normal changes, then turns toward view
intent at 12 rad/s. Right is derived from normal × tangent. A view along the
normal retains a stable tangent instead of making W vanish.

Only a registered active CanPerch surface with an enabled collider is eligible.
From the current support the world tries a close forward face, the normal support
probe, then an exposed-edge wrap probe if normal support was lost. No movement
input means no front/wrap acquisition. The first ray hit still blocks anything
behind it; actor anatomy is not an eligible perch surface.

Bounds are explicit: forward reach is 0.070 m including body radius; wrap origin
is 0.030 m ahead and 0.090 m below the old plane, with 0.135 m total ray reach.
The candidate root must be within 0.150 m of the motor, and candidate contact
within 0.180 m of the previous world contact. The candidate root must also pass
the existing free-mosquito volume check.

Different convex/primitive colliders require a nearby physical join witnessed by
three bounded alternating ClosestPoint steps, within a 2 mm seam tolerance.
AABB overlap alone is insufficient. Same convex collider faces can turn across
their edge. Nonconvex mesh joins are deliberately conservative: only same-collider,
near-coplanar following with three short ray witnesses is supported; general
nonconvex edge topology is not implemented. No claim that every imported mesh
edge can be traversed.

A changed normal enters existing ApproachingSurface and reaches its verified
target through the motor at 0.65 m/s. Contact selection never assigns actor
position. While rounding an edge the normal ray may temporarily miss; the new
local contact is retained until the root is within 8 mm, the support disappears,
it moves beyond 0.16 m, F/Detach or a hit interrupts, or 20 transition ticks expire.
There is no unlimited hanging against a blocked approach.

Steady attached motion carries the support's displacement through motor velocity
`(target-current)/dt`, plus local crawl velocity. This fixes loss of most platform
movement from the old spring being reanchored every tick. A large support jump
detaches instead of moving the actor there. Carrier velocity can exceed crawl
speed; it is collision-processed, not a position assignment. Edge-approach speed
remains capped at 0.65 m/s.

`LMS_SURFACE_TRANSITION` records candidate changes with actor, old/new IDs, probe
kind, root-to-target distance and actual world normal. The native harness also
returns structured transition traces. Same-support samples do not spam this log.

## Validation performed

**116/116 CPU checks passed**, including six new authority/capability composition
cases: floor→wall→ceiling and reverse with bounded motor steps and existing-wire
roundtrip/replica gate; F cancellation; strike interruption; blocked/removed new
support; moving support and large-jump rejection; missing/distant neighbor rejection.
These contacts are prescribed by FakeWorld; they do not prove PhysX adjacency.

The initial v2 CPU run had one harness assertion failure: it incorrectly required
zero movement on the selection tick despite a legitimate motor correction toward
the previous support. The corrected assertion requires that selection preserve
the exact motor result and remain within its step bound. v3 passed; after the
moving-support carry refinement the final v4 also passed. Earlier logs are retained.

Evidence:

- `N:/LetMeSleep/Validation/SurfaceTraversal-20260912-v4/`: final CPU results,
  domain/Unity adapter compilation and source receipt.
- `N:/LetMeSleep/Validation/SurfaceAdapter-20260912/20260913-011716-755/`: final
  Gameplay, Gameplay.Unity and ActorVisualBinding offline compilation, immutable
  source copies/hashes, **0 errors / 0 warnings**.
- `N:/LetMeSleep/Validation/SurfaceNativeChecks-20260912/PatchedModules-20260913-011909-135/`:
  final native harness compilation, **0 errors / 0 warnings**. Not executed.

## Director's native checks, prepared but not run here

`SurfaceTraversalNativeChecks.cs` contains eight synchronous real-PhysX fixtures:
concave and rotated joins, convex wrap selection, gap/nonperch rejection, distant
object rejection, actual host motor floor/wall/ceiling and reverse, actual host
motor around a convex box, F interruption, translated/rotated/disabled support.
Each fixture owns and destroys its geometry/proxies. Synthetic map geometry and
scripted inputs are used, with no game rendering or actual human input.

After integrating/importing **both** surface commits:

```powershell
& N:/LetMeSleep/Worktrees/gameplay/docs/unity/gameplay/validation/attention/Compile-SurfaceNativeChecks.ps1
```

Run the resulting Integrated `run-in-director-slot.cs` in the assigned empty-scene
native slot and save JSON. Do not load replacement modules from Compile-SurfaceAdapter
or treat PatchedModules compilation as execution. The harness returns failures
instead of declaring unexecuted scenarios PASS.

No Unity/native/editor/player/render/audio/GPU execution was performed by this
worker. Actual house/patio/lobby objects, imported character feet/body pose,
moving doors, smoothness and remote visual interpolation remain to inspect.
The CPU wire roundtrip is not WAN, host-client play or network transport evidence.
