# HUD coalescing fixture: 0.9.1 build unblock

The build correctly stopped on stderr even though the fixture's exit code was
0: Client's attachment presentation reads `world.actors`, but the inert
`WorldWitness` did not implement that registry. This caused repeated invalid
property errors during its real `_process` callback.

The fixture now supplies an empty typed actor-view registry. It intentionally
creates no rendered actors. No runtime guard, attachment behavior, HUD assertion
or coalescing expectation was changed.

The corrected test was copied temporarily into the Director's 0.9.1 base
checkout and run there against commit
`d9136b4456ed489122f44c02e43ab5f180acc5eb`. Thus no 0.9.2 HumanPose or skin code
participated in validation. The temporary test was removed in the runner's
finally block; the original base fixture was not modified by this run.

- Native fixture: 29 checks, 0 failures, exit 0, stderr 0 bytes.
- Engine interval: 2026-09-12 00:11:28–00:11:32 UTC, within the authorized slot.
- Engine released; no remaining Godot process or temporary fixture.
- Audit of the base checkout's test doubles found only this `WorldWitness` and
  this Client subclass. Other tests use the production World, including the
  voice-session test; no additional missing actor registry was found.

`motion091-hud-check.ps1` and the adjacent r1 artifacts record the isolated run.
This is a fixture/contract repair only, with no rendered-layout, network or FPS
claim, and is intended for selective integration into 0.9.1.
