# Bounded receiver voice jitter core

`src/voice_jitter_buffer.gd` is a Godot 4.5 `RefCounted` packet timeline. One
instance belongs to one speaker/stream and one serialized caller. It invokes
no network, audio, codec, microphone, timer or scene API. Packets are opaque;
the host validates that their codec format represents mono 48 kHz, 20 ms audio.
Output events tell a separate integration layer what to decode or conceal.

```gdscript
const VoiceJitterBuffer = preload("res://src/voice_jitter_buffer.gd")
var jitter = VoiceJitterBuffer.new()
jitter.begin_stream(1, 0, 1000000)
jitter.push_packet(1, 0, PackedByteArray([1, 2, 3]), 1010000)
var result: Dictionary = jitter.poll(1060000)
# result.events[0]: data, sequence 0, deadline_us 1060000, reset_decoder true.
# Reset the speaker's decoder BEFORE processing this event when flagged.
```

## API and exact timeline

- `configure(options: Dictionary) -> Dictionary`: returns `{ok, reason}`.
  Only allowed while inactive. Partial options preserve other settings; all
  validation is transactional. Unknown options return `unknown_option`, an
  active instance returns `active_stream`, and bad values return `invalid_config`.
- `begin_stream(epoch: int, first_sequence: int, local_now_us: int)`: returns
  `{ok, reason}` with `epoch`, `next_sequence` and `next_deadline_us` on success.
  Clears prior queued data, sets the first deadline to `now + prebuffer_ms`, and
  requires the next output event to reset the decoder. The caller explicitly
  authorizes this transition; packet contents never authorize epochs.
- `push_packet(epoch: int, sequence: int, payload: Variant, local_arrival_us: int)`:
  returns `{accepted: bool, reason: String, queued: int}`. An accepted payload is
  duplicated into private storage. Only accepted packets refresh the idle clock.
- `finish_stream(epoch: int, last_sequence: int, local_now_us: int)`: returns
  `{ok, reason}` and marks the inclusive last sequence of a normal talk spurt.
  It preserves pending deadlines and drains data/PLC through that sequence,
  then stops with `ended`. See the end-of-stream rules below.
- `poll(local_now_us: int)`: returns `{active, reason, events, queued, epoch,
  next_sequence, next_deadline_us}`. Every event contains `type` (`data` or `plc`),
  `sequence`, `deadline_us`, `frame_samples: 960`, and `reset_decoder: bool`.
  Only a `data` event contains `payload: PackedByteArray`. A PLC is one missing
  20 ms slot, not an empty packet. Calls before the next deadline return
  `reason: waiting` and an empty events array. Exactly at a deadline, it is due.
- `stop(reason: String = "stopped")`: purges queued audio, disables output,
  cancels pending reset, and returns the poll-shaped state with no events.
- `clear()`: same as `stop("cleared")`. It does not forget epoch/clock history.
- `get_stats()`: a detached snapshot of lifetime counters, rejection reasons,
  queue size/high watermark, cursor/deadline, clock history and configuration.

Epochs and sequences are integers in `0..2147483647`. Every successful begin
must use an epoch strictly greater than every prior successful begin on that
instance, including after stop/clear/timeout. Otherwise it returns `epoch_not_new`.
An invalid epoch or first sequence returns `invalid_epoch`/`invalid_sequence`.
An unsuccessful begin retains the current stream except for clock rollback.
There is no sequence wrap: the final allowed sequence can be emitted once,
then the stream stops with `sequence_exhausted`. A skip beyond that sequence
also stops. Construct a new instance when the epoch namespace is exhausted.

All `begin`, `push`, `finish` and `poll` times are **receiver-local monotonic microseconds**,
in `0..9007199254740991`. Equal times are allowed. No remote timestamp is used.
Time observation precedes other argument validation; even a rejected packet
can advance the local observed clock, but cannot refresh the idle clock.
A negative/out-of-range time returns `invalid_time` without changing the
timeline. A time smaller than any prior observed time returns `clock_rollback`,
immediately stops and purges the queue. The high-water clock remains unchanged.
Recovery requires a greater epoch and a timestamp at least that high-water value.

A begin whose prebuffer deadline would exceed `MAX_TIME_US` is rejected with
`invalid_time_horizon`. After the last representable deadline, poll returns any
current due event, then stops with `time_exhausted`; it cannot leave an active
stream waiting for an unrepresentable time.

The core epoch may be a **local playout generation**, distinct from the wire PTT
epoch. For proximity/mute re-entry during the same PTT, the transport validates
the wire epoch and a fresh authorized first sequence, then begins a new local
generation and maps only validated packets to it. Preserve consumed/skipped
sequence barriers as well as accepted-packet high watermarks. A packet alone must
never authorize a restart or reuse a pre-mute timeline.

## Bounds, reordering, overload and idle

| Option | Default | Allowed range |
| --- | ---: | --- |
| `prebuffer_ms` | 60 | 20..120, multiples of 20 |
| `max_queue_frames` | 12 | 1..64 |
| `max_payload_bytes` | 256 | 1..1275 |
| `max_future_frames` | 24 | max_queue_frames..256 |
| `max_poll_frames` | 3 | 1..6 |
| `idle_timeout_ms` | 300 | 100..1000, at least prebuffer_ms + 20 |

All option values must be integers; floats, including whole-valued floats, are
rejected. Frame duration is fixed at 20000 microseconds. At the defaults, payload
storage is at most 12 x 256 bytes plus container overhead. Opus 24 kbit/s CBR
20 ms packets normally need 60 bytes, but codec validation belongs to the host.

Accepted sequence numbers are in `[next_sequence, next_sequence + max_future_frames)`
and within the integer bounds. The queue tolerates arrival reordering in that
window. A full queue rejects new arrivals; it never evicts or silently relabels
accepted packets. Once a slot has been emitted as data or PLC, an arrival for it
is `late`. The deadline itself does not reject a packet if that slot has not yet
been polled; arrival-before-poll at the same timestamp is usable.

Packet rejection reasons are `inactive`, `idle_timeout`, `stale_epoch` (any
unapproved different epoch, older or newer), `invalid_sequence`,
`invalid_payload_type`, `empty_payload`, `oversize_payload`, `late`, `future`,
`duplicate`, `queue_full`, `after_final`, `invalid_time`, and `clock_rollback`. Validation order
is clock, active/idle, epoch, sequence bounds, declared final sequence, payload, sequence window,
duplicate, capacity. The implementation does not parse Opus bitstreams.

A normal poll emits up to `max_poll_frames` due slots, preserving their original
deadlines. If more slots are due, it skips `due - 1` old slots and returns only
the most recent due slot (`reason: resynced`). Queued packets before that cursor
are purged; future packets remain. The event has `reset_decoder: true`, even
if it is PLC. Reset must happen before decoding/concealment so stale codec
history is not replayed after a stall. The next deadline remains on the original
receiver timeline, after now; no arbitrary new prebuffer or unlimited catch-up
burst is created. The first event of each stream also requires reset.

Idle expires at `now - last_accepted >= idle_timeout_ms`, checked before output
and before accepting arrivals. It purges even buffered audio and stops with
`idle_timeout`; a late packet cannot restart it. Initially the idle clock begins
at `begin_stream`, so a stream that never receives a packet also stops. Until
expiry its missing due slots are bounded PLC events. Duplicate, stale, invalid,
late or rejected packets do not keep a stream alive. For a normal end of speech,
call `finish_stream` with the inclusive final sequence. Use `stop` for immediate
discard (mute, disconnect, proximity exit or cancellation). Authorize a new epoch
for the next talk spurt. This core never automatically restarts a stopped stream.

## Normal end of stream

The first valid `finish_stream` declares an immutable inclusive endpoint. The
epoch must match the locally authorized active epoch. The endpoint must be in
`[first_sequence - 1, 2147483647]`, at least the highest sequence ever accepted
in this stream (including packets already emitted or purged during overload),
and strictly below `next_sequence + max_future_frames`. No packet can implicitly
declare an end. The host must validate and authorize end markers separately.

`first_sequence - 1` declares an empty episode if no packet has been accepted;
for a stream starting at zero this is the special value -1. A final sequence
already behind the playout cursor ends immediately (`ok: true, reason: ended`).
Otherwise the marker returns `finishing`. Frames at or below the endpoint may
still arrive while the stream is active; later sequences return `after_final`.

A repeated equal endpoint is idempotent: `already_finishing` while active or
`already_ended` after normal completion. A different endpoint after a registered
finish returns `finish_conflict`; it cannot silently extend or shorten the
episode. Before the first finish, an invalid integer range returns
`invalid_sequence`, a final sequence below accepted history returns
`finish_before_accepted`, and an endpoint beyond the accepted future window
returns `future`. Other failures follow the existing clock/active/idle/epoch
rules. Equal-end idempotence does not apply after hard stop or idle expiry.

Poll emits data or ordinary 960-sample PLC only through the final sequence,
inclusive. The final event can share a result with `active: false, reason: ended`;
the caller must process returned events in order before closing the stream.
There is never a PLC event beyond the declared endpoint. If an overload skip
would put the latest due slot beyond the endpoint, the stream ends and purges
the old tail without replaying it. Only remaining authorized stream slots count
as overload skips in that case. A skip landing on/before the endpoint follows
the normal bounded resync/reset behavior.

Finishing does not refresh the idle clock, extend deadlines or override hard
stop/time limits. A late finish after idle expiry fails with `idle_timeout` and
does not resurrect output. If a final frame is emitted at the maximum sequence
or representable time, normal `ended` takes precedence because no next deadline
is needed. The diagnostic cursor advances one past an emitted final sequence,
except that it stays capped at `MAX_SEQUENCE` at the integer boundary.

`get_stats` adds `first_sequence`, `highest_accepted_sequence`,
`finish_registered`, `ending`, `final_sequence` and the lifetime `streams_ended`
counter. `ending` means active with a finish registered. `finish_registered` and
the endpoint remain visible after stopping until the next successful begin;
they are diagnostic history, not restart authorization. A `final_sequence` of
-1 is disambiguated by `finish_registered`. Poll-shaped results also expose
`ending` and `final_sequence`.

Counters include accepted/rejected packets, data/PLC events, skipped overload
slots, purged queued packets, reset events, idle stops, clock rollbacks, sequence
exhaustions, streams started and queue high watermark. Stop/clear/begin preserve
lifetime statistics and rejected-reason counts. `next_deadline_us` is -1 when
inactive. The last valid sequence cursor and epoch remain visible for diagnosis.

The parent owns independent synthetic tests and any actual-codec bridge smoke.
This file alone claims no WAN behavior, game FPS, playable output, speech quality,
thread safety or validated simultaneous-player capacity.
