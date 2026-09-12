# STAB-AUD-01 — bootstrap shutdown contract

Director assigned only `unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.cs` as additional runtime ownership. No UI/Presentation/Online source was changed. SurfaceVisualProbe was already delivered separately in 9426435. This patch concerns shutdown only; the historical Editor audio incident does not establish a published-player leak.

## Change

`QuitGame()` calls private instance `Quiesce()` before exit. Under UNITY_EDITOR it then sets `EditorApplication.isPlaying=false`; in the player it calls `Application.Quit()`. `OnDestroy()` reuses Quiesce. Its first action records the permanent `quiescing` guard, cancels pending online work and disables this component. Repeated calls are no-ops.

Before disposing sessions, Quiesce removes bootstrap subscriptions to UI feedback, room/lobby changes, appearance/lobby packets, lobby framing/movement output, gameplay begin/failure and local input/action forwarding. Root-creating callbacks/entrypoints also reject work after the guard. The gameplay round-finished callback cannot finish a room while closing.

Owned presentation/menu/gameplay/lobby roots stop their audio directors and deactivate synchronously, before deferred Destroy. Existing StopGame/StopLobbyMovement perform normal teardown; fallback Destroy still releases their separate roots if a stop hook throws. Session fields are cleared before each disposal, so re-entry does not dispose them twice. Each cleanup step logs exceptions and continues attempting the remaining owned resources; a logged cleanup error is a native test failure, not a successful close. Owned menu audio/UI/map are also released. Prefab assets, shared cameras, audio mixer settings, global volume and other processes are untouched.

## Offline evidence

Run `./Compile-Quiesce.ps1` in PowerShell. It snapshots the edited main file plus central Bootstrap partial files and compiles both preprocessor variants against the central imported APIs. This preserves current central partial-file changes without importing them into the gameplay branch.

Completed package: `N:/LetMeSleep/Validation/BootstrapQuiesce-20260912/20260912-200706-627`. Both **Editor and Player compilation: 0 errors, 0 warnings**. `receipt.json` records source/dependency hashes; `Editor/compile.log` and `Player/compile.log` contain results. Player compilation excludes UnityEditor references. The source main file had no logical central delta relative to gameplay HEAD before this fix; central changes elsewhere are outside this commit. No Unity, player, render, audio session or native lifecycle test was started.

## Native recipe coordinated with Estabilidad

Director must allocate the slot. Keep `-noaudio` for lifecycle tests; audible tests require the separate explicit listening slot. Preserve exact candidate HEAD/dirty state and process identity. The independent observer belongs to `N:/LetMeSleep/Validation/TeamRecovery/stability`.

1. **Idempotence while host stays alive.** In a fresh menu/practice or assigned connected lobby, retain references to fields `game`, `presentation`, `menuAudio`, `lobbyMovement`, `map` and `ui` (nulls are valid when a context has not created them). Resolve `Quiesce` by `BindingFlags.Instance | BindingFlags.NonPublic` and invoke it twice on the same AlfaApplication instance. Immediately assert `quiescing=true`, component disabled, each previously active owned root inactive, and no live playing AudioSource under these retained roots. All owned session fields (`gameNetwork`, `room`, `transport`, `lobby`, `connection`) must be null. Do not interpret null fields alone as destroyed Unity objects.
2. **Deferred destruction.** After at least one completed Unity frame, assert those retained roots compare Unity-null. No new gameplay, presentation, waiting-room, map, menu-audio or UI root may appear. Invoke late `OnLobbyChanged` and `OnRoomChanged` with the saved prior room view before destroying the bootstrap if desired; the guard must prevent re-creation. Do not create a synthetic PASS from a null room-view path alone. Console must contain no cleanup exception.
3. **Destruction independent of Quit.** In a separate fresh session, destroy only the AlfaApplication component while its separately instantiated roots exist. After OnDestroy runs, verify immediate deactivation and subsequent destruction with the retained references. The host/Editor may remain alive. Also cover a component destroyed before Start has created any roots.
4. **Quit behavior.** In another fresh session, invoke public QuitGame via the normal UI. Observe Editor `isPlaying=false` externally; a request to stop is not that postcondition. For a built player, use Estabilidad's exact-PID observer to require exit, disappearance of its window and audio session and no owned descendant process. Test menu, mosquito/human practice and an assigned online/lobby session; do not infer network cleanup from practice-only coverage.
5. **Regressions.** Before final quit in an independent session, normal round → menu and room return must still work. A stopped Quiesce instance is deliberately terminal and cannot resume gameplay.

Compile results demonstrate API/syntax compatibility only. Native root/callback assertions, audible menu → game → menu, external PID/window/Core Audio termination and WAN behavior remain pending. Do not close STAB-AUD-01's native gate or STAB-EVID-02 from compilation or from an in-process receipt.
