# Nearby character attention — 2026-09-12

Worker Código / Gameplay delivery, based on central `198dbe9`, branch
`codex/gameplay-nearest-attention`. Temporary Presentation ownership was assigned
by Director for this change.

## Contract

- Gameplay and lobby select any other current-session character, without species
  or team filters. Self, previews, inactive visuals and stale instances are excluded.
- Candidates must be within an inclusive 2 m three-dimensional radius measured
  between visual character roots, and within the inclusive 120-degree frontal cone
  (`dot >= 0.5`). The cone runs from this rig's `LookOrigin` to the candidate's
  configured look origin, camera/head anchor, or root fallback.
- Gameplay uses actual local `LocalViewForward` or accepted remote `ViewForward`;
  lobby uses the visual's interpolated yaw. No input, camera or view direction is written.
- Keep the current eligible target until the same nearest challenger stays strictly
  closer for 2 continuous seconds of unscaled time. Changing challenger, a tie with
  the current target, or losing eligibility resets the wait. Current wins distance
  ties; otherwise the pending challenger wins a nearest tie before roster order.
- An invalid, removed, inactive, out-of-range or out-of-cone current target is
  replaced immediately. With no eligible candidate, look along the actual view
  direction. Session/round/lobby-revision changes and disable clear the selection.
- Discovery scans only the accepted session roster and those actors' visual
  subtrees (session limit 16); it performs no global scene search. Transform
  instance identity prevents an actor ID reused by a new visual retaining a timer.
- The visibility filter is geometric radius/cone only; there is no wall-occlusion test.

## Validation

`NearestAttentionChecks.cs` is a standalone CPU test runner for the pure policy.
All 14 cases passed: 1.9/2-second boundary, changing challenger, ties, losing
distance advantage, removal, inclusive radius, immediate range/cone exit,
front/lateral/back directions, challenger eligibility, reset with new instances,
species independence, invalid geometry and clock rewind.

The tests supply candidate lists; removal simulates despawn/inactivity. They do
not execute Unity discovery, transforms, lifecycle callbacks or the fallback pose.

External reproduction projects and logs are at
`N:/LetMeSleep/Validation/NearestAttention-20260912/`:

```powershell
dotnet run --project N:/LetMeSleep/Validation/NearestAttention-20260912/Checks.csproj
dotnet build N:/LetMeSleep/Validation/NearestAttention-20260912/adapter/Adapter.csproj
```

The adapter and policy compiled against Unity 6000.3.24f1 and central imported
LetMeSleep assemblies with zero errors and zero warnings. `receipt.json` records
source/log/dependency hashes observed at handoff. No Unity editor, player, render
or GPU slot was used. Native gameplay/lobby observation of head selection,
local/remote pitch, cone/range exits, fallback and round recreation remains for
Director's scheduled runtime validation.
