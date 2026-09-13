# Surface orientation: projected 3D view and transported tangent

First surface delivery, base `a581a40`, branch `codex/gameplay-surface-continuity`.
Runtime files: ActorVisualBinding.cs (surface orientation only) and the separately
authorized pure helper SurfaceVisualFrame.cs. No authority locomotion, physical
position, collision, bite, IK, facial controller or wire change in this commit.

The visual now projects accepted `current.ViewForward` onto the support plane,
including pitch. It no longer substitutes BodyRotation's yaw-only forward. At a
near-normal view the prior tangent remains the stable fallback. Existing heading
turn speed and approach tilt/release smoothing remain in place.

When the support normal changes, `TransportForward` carries the prior tangent by
the minimal normal-to-normal rotation before applying the new projected view.
Exactly opposite normals retain heading by selecting that tangent as the half-turn
axis. Invalid history returns zero so the existing resolver picks a finite fallback.
Leaving support or disabling the binding clears tangent history. `TryResolve`'s
signature and behavior remain compatible.

Six new CPU cases cover positive/negative pitch on a wall, floor→wall→ceiling and
reverse, tilted moving-object normals, opposite normals, convex/concave quarter
turns and invalid history. Full suite: **110/110 PASS**. These are mathematical
normal changes, not PhysX traversal or a claim that actors already cross edges.

Evidence:

- `N:/LetMeSleep/Validation/SurfaceOrientation-20260912-v1/`: CPU suite and original
  domain/Unity adapter compilation.
- `N:/LetMeSleep/Validation/SurfaceAdapter-20260912/20260913-010117-774/`: independent
  offline build of current Gameplay, Gameplay.Unity and ActorVisualBinding against
  Unity 6000.3.24f1, **0 errors / 0 warnings**, immutable source copies and hashes.

`Compile-SurfaceAdapter.ps1` reproduces the offline compilation. Its replacement
assemblies are not runtime plugins and must not be loaded into the central editor.
No Unity/native/render/audio/GPU execution was performed by this worker.

Director still needs to observe real wall pitch/turn response, floor/ceiling,
rotating support and local/remote presentation. Actual support acquisition and
contiguous-edge traversal are the next separate commit, not part of this result.
