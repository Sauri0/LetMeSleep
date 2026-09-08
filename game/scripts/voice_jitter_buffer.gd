extends RefCounted
## One receiver-owned, serialized, bounded 20 ms packet timeline per speaker.
## No network, codec, audio API, remote clock, timer, or scene dependency.

const FRAME_US: int = 20000
const FRAME_SAMPLES: int = 960
const MAX_SEQUENCE: int = 2147483647
const MAX_TIME_US: int = 9007199254740991
const DEFAULTS: Dictionary = {
	"prebuffer_ms": 60,
	"max_queue_frames": 12,
	"max_payload_bytes": 256,
	"max_future_frames": 24,
	"max_poll_frames": 3,
	"idle_timeout_ms": 300,
}

var _settings: Dictionary = DEFAULTS.duplicate()
var _queue: Dictionary = {}
var _active: bool = false
var _epoch: int = -1
var _last_epoch: int = -1
var _first_sequence: int = 0
var _highest_accepted_sequence: int = -1
var _finish_registered: bool = false
var _final_sequence: int = -1
var _next_sequence: int = 0
var _next_deadline_us: int = -1
var _last_local_us: int = -1
var _last_accepted_us: int = -1
var _reset_pending: bool = false
var _stop_reason: String = "not_started"
var _counts: Dictionary = {
	"streams_started": 0,
	"streams_ended": 0,
	"packets_accepted": 0,
	"packets_rejected": 0,
	"data_frames": 0,
	"plc_frames": 0,
	"frames_skipped_overload": 0,
	"queued_packets_purged": 0,
	"decoder_reset_events": 0,
	"idle_stops": 0,
	"clock_rollbacks": 0,
	"sequence_exhaustions": 0,
	"queue_high_watermark": 0,
}
var _rejected_reasons: Dictionary = {}

func configure(options: Dictionary) -> Dictionary:
	if _active:
		return {"ok": false, "reason": "active_stream"}
	var candidate: Dictionary = _settings.duplicate()
	for key in options:
		if not DEFAULTS.has(key):
			return {"ok": false, "reason": "unknown_option"}
		if typeof(options[key]) != TYPE_INT:
			return {"ok": false, "reason": "invalid_config"}
		candidate[key] = options[key]
	if candidate["prebuffer_ms"] < 20 or candidate["prebuffer_ms"] > 120 or candidate["prebuffer_ms"] % 20 != 0:
		return {"ok": false, "reason": "invalid_config"}
	if candidate["max_queue_frames"] < 1 or candidate["max_queue_frames"] > 64:
		return {"ok": false, "reason": "invalid_config"}
	if candidate["max_payload_bytes"] < 1 or candidate["max_payload_bytes"] > 1275:
		return {"ok": false, "reason": "invalid_config"}
	if candidate["max_future_frames"] < candidate["max_queue_frames"] or candidate["max_future_frames"] > 256:
		return {"ok": false, "reason": "invalid_config"}
	if candidate["max_poll_frames"] < 1 or candidate["max_poll_frames"] > 6:
		return {"ok": false, "reason": "invalid_config"}
	if candidate["idle_timeout_ms"] < 100 or candidate["idle_timeout_ms"] > 1000 or candidate["idle_timeout_ms"] < candidate["prebuffer_ms"] + 20:
		return {"ok": false, "reason": "invalid_config"}
	_settings = candidate
	return {"ok": true, "reason": "configured"}

func _purge_queue() -> void:
	_counts["queued_packets_purged"] += _queue.size()
	_queue.clear()

func _stop_internal(reason: String) -> void:
	_active = false
	_stop_reason = reason
	_reset_pending = false
	_next_deadline_us = -1
	_purge_queue()

func _end_stream() -> void:
	if _active:
		_counts["streams_ended"] += 1
	_stop_internal("ended")

func _observe_clock(local_now_us: int) -> String:
	if local_now_us < 0 or local_now_us > MAX_TIME_US:
		return "invalid_time"
	if _last_local_us >= 0 and local_now_us < _last_local_us:
		_counts["clock_rollbacks"] += 1
		_stop_internal("clock_rollback")
		return "clock_rollback"
	_last_local_us = local_now_us
	return ""

func begin_stream(epoch: int, first_sequence: int, local_now_us: int) -> Dictionary:
	var clock_error: String = _observe_clock(local_now_us)
	if not clock_error.is_empty():
		return {"ok": false, "reason": clock_error}
	if epoch < 0 or epoch > MAX_SEQUENCE:
		return {"ok": false, "reason": "invalid_epoch"}
	if epoch <= _last_epoch:
		return {"ok": false, "reason": "epoch_not_new"}
	if first_sequence < 0 or first_sequence > MAX_SEQUENCE:
		return {"ok": false, "reason": "invalid_sequence"}
	if local_now_us > MAX_TIME_US - int(_settings["prebuffer_ms"]) * 1000:
		return {"ok": false, "reason": "invalid_time_horizon"}
	_purge_queue()
	_epoch = epoch
	_last_epoch = epoch
	_first_sequence = first_sequence
	_highest_accepted_sequence = first_sequence - 1
	_finish_registered = false
	_final_sequence = -1
	_next_sequence = first_sequence
	_next_deadline_us = local_now_us + int(_settings["prebuffer_ms"]) * 1000
	_last_accepted_us = local_now_us
	_reset_pending = true
	_active = true
	_stop_reason = ""
	_counts["streams_started"] += 1
	return {"ok": true, "reason": "started", "epoch": _epoch, "next_sequence": _next_sequence, "next_deadline_us": _next_deadline_us}

func _reject_packet(reason: String) -> Dictionary:
	_counts["packets_rejected"] += 1
	_rejected_reasons[reason] = int(_rejected_reasons.get(reason, 0)) + 1
	return {"accepted": false, "reason": reason, "queued": _queue.size()}

func _expire_idle(local_now_us: int) -> bool:
	if _active and local_now_us - _last_accepted_us >= int(_settings["idle_timeout_ms"]) * 1000:
		_counts["idle_stops"] += 1
		_stop_internal("idle_timeout")
		return true
	return false

func finish_stream(epoch: int, last_sequence: int, local_now_us: int) -> Dictionary:
	var clock_error: String = _observe_clock(local_now_us)
	if not clock_error.is_empty():
		return {"ok": false, "reason": clock_error}
	if not _active:
		if epoch == _epoch and _finish_registered and _stop_reason == "ended":
			return {"ok": last_sequence == _final_sequence, "reason": "already_ended" if last_sequence == _final_sequence else "finish_conflict"}
		return {"ok": false, "reason": "inactive"}
	if _expire_idle(local_now_us):
		return {"ok": false, "reason": "idle_timeout"}
	if epoch != _epoch:
		return {"ok": false, "reason": "stale_epoch"}
	if _finish_registered:
		return {"ok": last_sequence == _final_sequence, "reason": "already_finishing" if last_sequence == _final_sequence else "finish_conflict"}
	if last_sequence < _first_sequence - 1 or last_sequence > MAX_SEQUENCE:
		return {"ok": false, "reason": "invalid_sequence"}
	if last_sequence < _highest_accepted_sequence:
		return {"ok": false, "reason": "finish_before_accepted"}
	if last_sequence - _next_sequence >= int(_settings["max_future_frames"]):
		return {"ok": false, "reason": "future"}
	_finish_registered = true
	_final_sequence = last_sequence
	# A finish marker does not refresh packet liveness or move any deadline.
	if _final_sequence < _next_sequence:
		_end_stream()
		return {"ok": true, "reason": "ended"}
	return {"ok": true, "reason": "finishing"}

func push_packet(epoch: int, sequence: int, payload: Variant, local_arrival_us: int) -> Dictionary:
	var clock_error: String = _observe_clock(local_arrival_us)
	if not clock_error.is_empty():
		return _reject_packet(clock_error)
	if not _active:
		return _reject_packet("inactive")
	# Expiry applies to arrival too: a late packet cannot silently revive a stream.
	if _expire_idle(local_arrival_us):
		return _reject_packet("idle_timeout")
	if epoch != _epoch:
		return _reject_packet("stale_epoch")
	if sequence < 0 or sequence > MAX_SEQUENCE:
		return _reject_packet("invalid_sequence")
	if _finish_registered and sequence > _final_sequence:
		return _reject_packet("after_final")
	if typeof(payload) != TYPE_PACKED_BYTE_ARRAY:
		return _reject_packet("invalid_payload_type")
	var bytes: PackedByteArray = payload
	if bytes.is_empty():
		return _reject_packet("empty_payload")
	if bytes.size() > int(_settings["max_payload_bytes"]):
		return _reject_packet("oversize_payload")
	if sequence < _next_sequence:
		return _reject_packet("late")
	if sequence - _next_sequence >= int(_settings["max_future_frames"]):
		return _reject_packet("future")
	if _queue.has(sequence):
		return _reject_packet("duplicate")
	if _queue.size() >= int(_settings["max_queue_frames"]):
		return _reject_packet("queue_full")
	_queue[sequence] = bytes.duplicate()
	_highest_accepted_sequence = maxi(_highest_accepted_sequence, sequence)
	_last_accepted_us = local_arrival_us
	_counts["packets_accepted"] += 1
	_counts["queue_high_watermark"] = maxi(int(_counts["queue_high_watermark"]), _queue.size())
	return {"accepted": true, "reason": "accepted", "queued": _queue.size()}

func _poll_result(reason: String, events: Array) -> Dictionary:
	return {
		"active": _active, "reason": reason, "events": events,
		"queued": _queue.size(), "epoch": _epoch,
		"next_sequence": _next_sequence, "next_deadline_us": _next_deadline_us,
		"ending": _active and _finish_registered, "final_sequence": _final_sequence,
	}

func poll(local_now_us: int) -> Dictionary:
	var events: Array = []
	var clock_error: String = _observe_clock(local_now_us)
	if not clock_error.is_empty():
		return _poll_result(clock_error, events)
	if not _active:
		return _poll_result(_stop_reason, events)
	if _expire_idle(local_now_us):
		return _poll_result("idle_timeout", events)
	if local_now_us < _next_deadline_us:
		return _poll_result("waiting", events)
	var due: int = (local_now_us - _next_deadline_us) / FRAME_US + 1
	var result_reason: String = "due"
	if due > int(_settings["max_poll_frames"]):
		# Retain the most recent due slot, preserving the original local timeline.
		# Do not relabel old queued audio as current or emit a catch-up burst.
		var skipped: int = due - 1
		if _finish_registered and skipped > _final_sequence - _next_sequence:
			# The latest due slot is already beyond the authorized end. Count
			# only skipped stream slots; do not replay stale queued tail audio.
			_counts["frames_skipped_overload"] += _final_sequence - _next_sequence + 1
			_next_sequence = mini(_final_sequence + 1, MAX_SEQUENCE)
			_end_stream()
			return _poll_result("ended", events)
		if skipped > MAX_SEQUENCE - _next_sequence:
			_counts["sequence_exhaustions"] += 1
			_stop_internal("sequence_exhausted")
			return _poll_result("sequence_exhausted", events)
		_next_sequence += skipped
		_next_deadline_us += skipped * FRAME_US
		_counts["frames_skipped_overload"] += skipped
		for sequence in _queue.keys():
			if sequence < _next_sequence:
				_queue.erase(sequence)
				_counts["queued_packets_purged"] += 1
		_reset_pending = true
		due = 1
		result_reason = "resynced"
	for slot in range(due):
		var has_data: bool = _queue.has(_next_sequence)
		var event: Dictionary = {
			"type": "data" if has_data else "plc",
			"sequence": _next_sequence,
			"deadline_us": _next_deadline_us,
			"frame_samples": FRAME_SAMPLES,
			"reset_decoder": _reset_pending,
		}
		if _reset_pending:
			_counts["decoder_reset_events"] += 1
			_reset_pending = false
		if has_data:
			event["payload"] = _queue[_next_sequence]
			_queue.erase(_next_sequence)
			_counts["data_frames"] += 1
		else:
			_counts["plc_frames"] += 1
		events.append(event)
		if _finish_registered and _next_sequence == _final_sequence:
			_next_sequence = mini(_next_sequence + 1, MAX_SEQUENCE)
			_end_stream()
			return _poll_result("ended", events)
		if _next_sequence == MAX_SEQUENCE:
			_counts["sequence_exhaustions"] += 1
			_stop_internal("sequence_exhausted")
			return _poll_result("sequence_exhausted", events)
		_next_sequence += 1
		if _next_deadline_us > MAX_TIME_US - FRAME_US:
			_stop_internal("time_exhausted")
			return _poll_result("time_exhausted", events)
		_next_deadline_us += FRAME_US
	return _poll_result(result_reason, events)

func stop(reason: String = "stopped") -> Dictionary:
	_stop_internal(reason if not reason.is_empty() else "stopped")
	return _poll_result(_stop_reason, [])

func clear() -> Dictionary:
	return stop("cleared")

func get_stats() -> Dictionary:
	var result: Dictionary = _counts.duplicate(true)
	result["rejected_reasons"] = _rejected_reasons.duplicate(true)
	result["active"] = _active
	result["queued"] = _queue.size()
	result["epoch"] = _epoch
	result["last_authorized_epoch"] = _last_epoch
	result["first_sequence"] = _first_sequence
	result["highest_accepted_sequence"] = _highest_accepted_sequence
	result["finish_registered"] = _finish_registered
	result["ending"] = _active and _finish_registered
	result["final_sequence"] = _final_sequence
	result["next_sequence"] = _next_sequence
	result["next_deadline_us"] = _next_deadline_us
	result["last_local_us"] = _last_local_us
	result["last_accepted_packet_or_begin_us"] = _last_accepted_us
	result["stop_reason"] = _stop_reason
	result["configuration"] = _settings.duplicate(true)
	return result
