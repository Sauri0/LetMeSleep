extends Node
## No capture, playback, AudioServer calls, network, or rendering work.
## Explicit checks run in release builds too (no assert-only test outcomes).

var checks: int = 0
var failures: int = 0

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: ", description)

func pcm_frame(frame_index: int = 0) -> PackedFloat32Array:
	var pcm := PackedFloat32Array()
	pcm.resize(960)
	for i in range(960):
		var t := float(frame_index * 960 + i) / 48000.0
		pcm[i] = 0.25 * sin(TAU * 220.0 * t) + 0.1 * sin(TAU * 440.0 * t)
	return pcm

func valid_pcm(pcm: PackedFloat32Array) -> bool:
	if pcm.size() != 960:
		return false
	for sample in pcm:
		if not is_finite(sample):
			return false
	return true

func rejected(codec: Object, output: Variant, expected_error: int, label: String) -> void:
	check(output.is_empty(), label + ": empty result")
	check(codec.get_last_error() == expected_error, label + ": explicit expected error")
	check(not codec.get_last_error_message().is_empty(), label + ": useful error message")

func _ready() -> void:
	# Dynamic lookup lets a missing DLL report a test failure, not a script parse error.
	if not ClassDB.class_exists("LMSOpusCodec"):
		printerr("FAIL: LMSOpusCodec is not registered. Build DLL and import this isolated project first.")
		get_tree().quit(1)
		return
	var codec: Object = ClassDB.instantiate("LMSOpusCodec")
	if codec == null or not codec.is_ready():
		printerr("FAIL: LMSOpusCodec initialization failed.")
		get_tree().quit(1)
		return
	check(codec.get_bitrate() == 24000, "default 24000 bit/s")
	check(codec.get_last_error() == 0, "initialization successful")
	check(not codec.get_opus_version().is_empty(), "Opus version")
	var lookahead: int = codec.get_lookahead_samples()
	check(lookahead > 0 and lookahead < 960, "VOIP encoder lookahead is positive and below 20 ms")
	print("OPUS_VERSION=", codec.get_opus_version(), " LOOKAHEAD_SAMPLES=", lookahead)
	var original := pcm_frame()
	var packet: PackedByteArray = codec.encode_frame(original)
	check(packet.size() == 60, "24 kbit/s hard CBR at 20 ms produces 60 bytes")
	check(codec.get_last_error() == 0, "encode successful")
	var decoded: PackedFloat32Array = codec.decode_frame(packet)
	check(valid_pcm(decoded), "decode produces exactly 960 finite samples")
	check(codec.get_last_error() == 0, "decode successful")
	var energy: float = 0.0
	for sample in decoded:
		energy += sample * sample
	check(energy > 0.001, "synthetic signal decodes to nonzero energy")

	# Independent objects and deterministic reset prove state is owned per stream.
	var fresh: Object = ClassDB.instantiate("LMSOpusCodec")
	check(fresh.is_ready(), "independent speaker codec initialization")
	check(fresh.encode_frame(original) == packet, "independent encoder initial state")
	check(fresh.decode_frame(packet) == decoded, "independent decoder initial state")
	for frame_index in range(1, 100):
		var streamed: PackedByteArray = codec.encode_frame(pcm_frame(frame_index))
		check(streamed.size() == 60, "stream CBR frame %d" % frame_index)
		check(valid_pcm(codec.decode_frame(streamed)), "stream decode frame %d" % frame_index)
	for lost_frame in range(3):
		check(valid_pcm(codec.conceal_frame()), "explicit PLC frame %d" % lost_frame)
		check(codec.get_last_error() == 0, "PLC success %d" % lost_frame)
	check(valid_pcm(codec.decode_frame(codec.encode_frame(pcm_frame(103)))), "decode resumes after PLC")
	check(codec.reset_encoder(), "encoder reset")
	check(codec.encode_frame(original) == packet, "reset reproduces initial encoder packet")
	check(codec.reset_decoder(), "decoder reset")
	check(codec.decode_frame(packet) == decoded, "reset reproduces initial decoded PCM")

	# Wrapper contract errors have their own codes; malformed bitstreams use Opus codes.
	rejected(codec, codec.encode_frame(PackedFloat32Array()), -1001, "empty PCM")
	var wrong_size := original.duplicate()
	wrong_size.resize(959)
	rejected(codec, codec.encode_frame(wrong_size), -1001, "short PCM")
	wrong_size.resize(1920)
	rejected(codec, codec.encode_frame(wrong_size), -1001, "stereo/40ms-size PCM")
	for bad_value in [NAN, INF, -INF, 1.01, -1.01]:
		var bad_pcm := original.duplicate()
		bad_pcm[400] = bad_value
		rejected(codec, codec.encode_frame(bad_pcm), -1002, "nonfinite or out-of-range PCM")
	rejected(codec, codec.decode_frame(PackedByteArray()), -1003, "empty packet is not PLC")
	var oversized := PackedByteArray()
	oversized.resize(1276)
	rejected(codec, codec.decode_frame(oversized), -1003, "oversized packet")
	# Code 3 TOC requires a second byte with framing information; absent here.
	rejected(codec, codec.decode_frame(PackedByteArray([3])), -4, "malformed packet framing")
	# Valid zero-payload Opus TOCs with 10 ms and 40 ms duration must be rejected.
	rejected(codec, codec.decode_frame(PackedByteArray([0])), -1005, "10 ms packet")
	rejected(codec, codec.decode_frame(PackedByteArray([16])), -1005, "40 ms packet")
	var stereo_toc := packet.duplicate()
	stereo_toc[0] = stereo_toc[0] | 4
	rejected(codec, codec.decode_frame(stereo_toc), -1004, "stereo packet")
	for bad_bitrate in [0, 5999, 64001, 9223372036854775807]:
		check(not codec.set_bitrate(bad_bitrate), "invalid bitrate rejected")
		check(codec.get_last_error() == -1006, "bitrate validation error")
		check(codec.get_bitrate() == 24000, "invalid bitrate preserves configuration")
	check(codec.set_bitrate(32000), "set 32 kbit/s")
	check(codec.reset_encoder(), "reset preserves bitrate control")
	check(codec.get_bitrate() == 32000, "configured bitrate survives reset")
	check(codec.encode_frame(original).size() == 80, "32 kbit/s CBR produces 80 bytes")
	for bound in [6000, 64000]:
		check(codec.set_bitrate(bound), "supported bitrate bound %d" % bound)
		check(valid_pcm(codec.decode_frame(codec.encode_frame(original))), "bitrate bound round trip %d" % bound)
	check(codec.set_bitrate(24000), "restore default bitrate")
	check(codec.get_last_error() == 0, "success clears previous error")

	# Rejected inputs must leave history unchanged before decode/encode is called.
	check(codec.reset_encoder() and fresh.reset_encoder(), "state isolation encoder reset")
	codec.encode_frame(wrong_size)
	check(codec.encode_frame(original) == fresh.encode_frame(original), "invalid PCM does not advance encoder")
	check(codec.reset_decoder() and fresh.reset_decoder(), "state isolation decoder reset")
	codec.decode_frame(PackedByteArray())
	codec.decode_frame(stereo_toc)
	check(codec.decode_frame(packet) == fresh.decode_frame(packet), "invalid packet does not advance decoder")
	check(codec.reset_decoder(), "fresh PLC reset")
	check(valid_pcm(codec.conceal_frame()), "PLC is defined even before first received frame")

	# Relay validation must preserve both advanced codec histories. Warming them
	# first makes an accidental reset observable as well as an accidental decode.
	check(codec.reset_encoder() and fresh.reset_encoder(), "validation encoder warm-up reset")
	check(codec.reset_decoder() and fresh.reset_decoder(), "validation decoder warm-up reset")
	check(codec.encode_frame(original) == fresh.encode_frame(original), "validation equal warm encoder state")
	check(codec.decode_frame(packet) == fresh.decode_frame(packet), "validation equal warm decoder state")
	check(codec.validate_frame(packet), "structural validation accepts actual encoded frame")
	check(codec.get_last_error() == 0, "valid structural check has no error")
	check(not codec.validate_frame(PackedByteArray()), "structural validation rejects empty packet")
	check(codec.get_last_error() == -1003, "validation empty packet size error")
	check(not codec.get_last_error_message().is_empty(), "validation rejection has useful error context")
	check(not codec.validate_frame(PackedByteArray([3])), "structural validation rejects malformed framing")
	check(codec.get_last_error() == -4, "validation malformed Opus error")
	check(not codec.validate_frame(stereo_toc), "structural validation rejects stereo")
	check(codec.get_last_error() == -1004, "validation channel error")
	check(not codec.validate_frame(PackedByteArray([0])), "structural validation rejects 10 ms")
	check(codec.get_last_error() == -1005, "validation duration error")
	check(not codec.validate_frame(oversized), "structural validation rejects oversized packet")
	check(codec.get_last_error() == -1003, "validation oversized packet error")
	check(codec.validate_frame(packet), "structural validation recovers after invalid frames")
	check(codec.get_last_error() == 0 and codec.get_last_error_message().is_empty(), "valid structural check clears earlier error")
	check(codec.encode_frame(pcm_frame(1)) == fresh.encode_frame(pcm_frame(1)), "valid and invalid validation preserve warm encoder history")
	check(codec.decode_frame(packet) == fresh.decode_frame(packet), "valid and invalid validation preserve warm decoder history")
	print("LMS_OPUS_SMOKE checks=", checks, " failures=", failures)
	get_tree().quit(0 if failures == 0 else 1)
