# Surface departure view handoff

Implemented in `game/scripts/simulation.gd` and
`game/scripts/mosquito_pose.gd`. New fixture:
`game/tests/surface_view09_test.gd`. No SurfaceLocomotion, Arena, Camera,
Client, Network, PracticeSession, renderer, or mesh edits for this fix.

## Direction and controls

The departure direction is the actual surface camera direction, including any
local yaw input that arrived before the next walking tick:

`surface_basis(normal, forward rotated around normal by unconsumed yaw) * rotation_x(surface_pitch) * FORWARD`.

`MosquitoPose.surface_view_direction(normal,forward,pitch)` is the shared pure
helper. `view_angles(direction,fallback_yaw)` converts that direction to world
flight yaw/pitch; at an exactly vertical heading it preserves a stable fallback
yaw while retaining the exact vertical direction. `VIEW_PITCH_LIMIT=PI/2` is
necessary to represent that case without the old 1.48-radian clipping.
`SURFACE_PITCH_LIMIT=1.35` matches the existing surface camera.

Simulation intercepts F, Space, E concentration departure, loss of static support,
and separation from a supporting face caused by collision. It captures the view
before clearing the support, then sets the same world yaw/pitch used by movement.
The existing outward departure velocity, collision predicates and displacement
remain unchanged. A blocked departure creates no transition. Concentration and
assistance while perched now use the same visible direction for their facing
tests; range, LOS, charge, priorities and recovery rates are unchanged.

## Private protocol

Only the owning insect receives `private.surface.view_transition`:

```text
{} before the first departure, otherwise
{
  revision: int,          # increases per actor within the round
  yaw: float,            # authority's rebased world radians
  pitch: float,
  input_seq: int,        # last accepted input at departure
  source_yaw: float,     # old surface-coordinate aim, including captured F aim
  source_pitch: float,
  acknowledged: bool
}
```

The record persists across snapshots, including after ACK. It is not a one-frame
event and publishes no support ID or additional public state. Root's client
consumes a new revision once, preserves its pending mouse delta relative to the
source angles, and sends the rebased angles in the existing reliable JSON action
transport. Authenticated sender and strict wire types remain Network's job.

Exact Simulation entry point:

```gdscript
submit_view_ack(id: int, action_seq: int, revision: int,
                first_input_seq: int, yaw: float, pitch: float) -> bool
```

The ACK must name a pending current revision of a living mosquito, use a fresh
shared `_action_seq`, carry finite angles, and set `first_input_seq` strictly above
the departure's `input_seq`. Its angles are the first rebased view, including the
client's pending mouse delta. Duplicate, stale, wrong-role, wrong-revision and
malformed ACKs cannot unlock or recenter the view.

While ACK is pending, normal input sequence validation, movement and freshness
continue; incoming orientation does not overwrite the departure view. Only the
latest received orientation is buffered. On ACK, the authority installs the
`first_input_seq` barrier and applies the ACK angles atomically. If a buffered
rebased input at or above that barrier arrived first on the other channel, its
newer angles take precedence. Inputs below the barrier never update orientation,
even if their movement sequence had not previously arrived. Regular duplicate
input rejection still applies to movement. F and ACK share the action sequence,
so an old F packet cannot replay after the ACK. New round initialization clears
revision, transition, buffer and barrier.

The camera may return to a world horizon after departure; that removes surface
roll without changing its forward vector or W flight direction. Rendering and
actual ENet integration are Root's separate validation responsibilities.

## Executed source validation

All five serial headless processes exited 0 with clean logs:

| Fixture | Passed | Log |
| --- | ---: | --- |
| surface_view09_test | 213 | work/surface-view09.log |
| surface09_test | 6,722 | work/surface-view09-surface09_test.log |
| focus_combat_test | 145 | work/surface-view09-focus_combat_test.log |
| stun_help_test | 297 | work/surface-view09-stun_help_test.log |
| contact_orientation06_test | 306 | work/surface-view09-contact_orientation06_test.log |

The new test covers six faces at three pitches, an unconsumed local yaw delta,
exact vertical departure, real floor-to-wall-to-ceiling walking and subsequent W
flight under old-angle traffic at 60/20 Hz, Space, both Input/ACK channel orders,
an ACK overtaking unseen old inputs, pending mouse delta, stale/malformed ACKs,
action sequence replay, persistent private data and reset. A real wall E sequence
completes timed focus/acquisition; a wall helper stays supported and reduces the
stun timer at the existing total 4x rate.

No existing regression expectations were changed. This finite source gate is not
a native camera test or an ENet transport claim.
