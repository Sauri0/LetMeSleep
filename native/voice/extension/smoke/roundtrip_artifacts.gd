extends SceneTree
## Optional offline artifact/throughput test. No audio devices or network APIs.

const RATE: int = 48000
const FRAME: int = 960
const OUTPUT_DIR: String = "res://artifacts"
var failure: String = ""

func _initialize() -> void:
	call_deferred("run")

func fail(message: String) -> void:
	failure = message
	printerr("ARTIFACT_TEST_FAIL: ", message)
	quit(1)

func read_pcm16_mono(path: String) -> PackedFloat32Array:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		failure = "Cannot read fixture: " + path
		return PackedFloat32Array()
	if file.get_length() > 16 * 1024 * 1024:
		failure = "Fixture exceeds 16 MiB bound."
		return PackedFloat32Array()
	var bytes: PackedByteArray = file.get_buffer(file.get_length())
	if bytes.size() < 44 or bytes.slice(0, 4).get_string_from_ascii() != "RIFF" or bytes.slice(8, 12).get_string_from_ascii() != "WAVE":
		failure = "Expected RIFF/WAVE fixture."
		return PackedFloat32Array()
	var offset: int = 12
	var format_ok: bool = false
	var data_offset: int = -1
	var data_size: int = 0
	while offset + 8 <= bytes.size():
		var chunk: String = bytes.slice(offset, offset + 4).get_string_from_ascii()
		var size: int = bytes.decode_u32(offset + 4)
		var start: int = offset + 8
		if start + size > bytes.size():
			failure = "Truncated WAV chunk."
			return PackedFloat32Array()
		if chunk == "fmt ":
			format_ok = size >= 16 and bytes.decode_u16(start) == 1 and bytes.decode_u16(start + 2) == 1 and bytes.decode_u32(start + 4) == RATE and bytes.decode_u16(start + 12) == 2 and bytes.decode_u16(start + 14) == 16
		elif chunk == "data":
			data_offset = start
			data_size = size
		offset = start + size + (size % 2)
	if not format_ok or data_offset < 0 or data_size == 0 or data_size % 2 != 0:
		failure = "Expected nonempty 48 kHz mono PCM16 WAV."
		return PackedFloat32Array()
	var pcm := PackedFloat32Array()
	pcm.resize(data_size / 2)
	for i in range(pcm.size()):
		var value: int = bytes.decode_u16(data_offset + i * 2)
		if value >= 32768:
			value -= 65536
		pcm[i] = float(value) / 32768.0
	return pcm

func make_frames(pcm: PackedFloat32Array) -> Array[PackedFloat32Array]:
	var frames: Array[PackedFloat32Array] = []
	for start in range(0, pcm.size(), FRAME):
		var frame: PackedFloat32Array = pcm.slice(start, mini(start + FRAME, pcm.size()))
		frame.resize(FRAME)
		frames.append(frame)
	return frames

func valid_pcm(pcm: PackedFloat32Array) -> bool:
	if pcm.size() != FRAME:
		return false
	for sample in pcm:
		if not is_finite(sample):
			return false
	return true

func write_wav(path: String, pcm: PackedFloat32Array) -> int:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		failure = "Cannot write decoded WAV: " + path
		return -1
	file.big_endian = false
	file.store_buffer("RIFF".to_ascii_buffer())
	file.store_32(36 + pcm.size() * 2)
	file.store_buffer("WAVEfmt ".to_ascii_buffer())
	file.store_32(16)
	file.store_16(1)
	file.store_16(1)
	file.store_32(RATE)
	file.store_32(RATE * 2)
	file.store_16(2)
	file.store_16(16)
	file.store_buffer("data".to_ascii_buffer())
	file.store_32(pcm.size() * 2)
	var clipped: int = 0
	for sample in pcm:
		if sample < -1.0 or sample > 32767.0 / 32768.0:
			clipped += 1
		var quantized: int = clampi(roundi(sample * 32768.0), -32768, 32767)
		file.store_16(quantized & 65535)
	file.flush()
	if file.get_error() != OK:
		failure = "Error writing decoded WAV."
		return -1
	return clipped

func run_benchmark(frames: Array[PackedFloat32Array], pair_count: int) -> Dictionary:
	var encoders: Array[Object] = []
	var decoders: Array[Object] = []
	for speaker in range(pair_count):
		var encoder: Object = ClassDB.instantiate("LMSOpusCodec")
		var decoder: Object = ClassDB.instantiate("LMSOpusCodec")
		if not encoder.is_ready() or not decoder.is_ready():
			failure = "Benchmark codec initialization failed."
			return {}
		encoders.append(encoder)
		decoders.append(decoder)
	# Warm up persistent objects before measuring; do not include construction.
	for tick in range(10):
		for speaker in range(pair_count):
			var packet: PackedByteArray = encoders[speaker].encode_frame(frames[tick % frames.size()])
			var decoded: PackedFloat32Array = decoders[speaker].decode_frame(packet)
			if packet.is_empty() or not valid_pcm(decoded):
				failure = "Benchmark warm-up round trip failed."
				return {}
	var tick_us: Array[int] = []
	var encode_us: int = 0
	var decode_us: int = 0
	var byte_count: int = 0
	var wall_start: int = Time.get_ticks_usec()
	for tick in range(150):
		var tick_start: int = Time.get_ticks_usec()
		for speaker in range(pair_count):
			var encode_start: int = Time.get_ticks_usec()
			var packet: PackedByteArray = encoders[speaker].encode_frame(frames[tick % frames.size()])
			encode_us += Time.get_ticks_usec() - encode_start
			var decode_start: int = Time.get_ticks_usec()
			var decoded: PackedFloat32Array = decoders[speaker].decode_frame(packet)
			decode_us += Time.get_ticks_usec() - decode_start
			if packet.is_empty() or decoded.size() != FRAME or decoders[speaker].get_last_error() != 0:
				failure = "Measured benchmark round trip failed."
				return {}
			byte_count += packet.size()
		tick_us.append(Time.get_ticks_usec() - tick_start)
	var wall_us: int = Time.get_ticks_usec() - wall_start
	tick_us.sort()
	return {
		"persistent_encoder_decoder_pairs": pair_count,
		"execution": "serialized on one GDScript thread, no pacing",
		"warmup_ticks_excluded": 10,
		"measured_ticks": 150,
		"round_trips": pair_count * 150,
		"encoded_bytes": byte_count,
		"wall_us": wall_us,
		"encode_calls_total_us": encode_us,
		"decode_calls_total_us": decode_us,
		"tick_p50_us": tick_us[74],
		"tick_p95_us": tick_us[142],
		"tick_max_us": tick_us[149],
		"nominal_audio_tick_us": 20000,
		"wall_fraction_of_3s_audio_window": float(wall_us) / 3000000.0,
		"timing_scope": "Includes bindings and packed-array outputs; wall and tick timing include loop/error checks. OS scheduling and concurrent load affect results."
	}

func run() -> void:
	if not ClassDB.class_exists("LMSOpusCodec"):
		fail("LMSOpusCodec not registered; build DLL and import isolated project.")
		return
	var fixture: String = ProjectSettings.globalize_path("res://../../../voice-fixtures/tone440-48000-mono.wav").simplify_path()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--fixture="):
			fixture = argument.trim_prefix("--fixture=")
		else:
			fail("Unknown argument: " + argument)
			return
	var pcm: PackedFloat32Array = read_pcm16_mono(fixture)
	if pcm.is_empty():
		fail(failure)
		return
	var frames: Array[PackedFloat32Array] = make_frames(pcm)
	var encoder: Object = ClassDB.instantiate("LMSOpusCodec")
	var decoder: Object = ClassDB.instantiate("LMSOpusCodec")
	if not encoder.is_ready() or not decoder.is_ready():
		fail("Artifact codec initialization failed.")
		return
	var lookahead: int = encoder.get_lookahead_samples()
	var decoded_all := PackedFloat32Array()
	var encoded_bytes: int = 0
	var packet_sizes: Dictionary = {}
	var begin: int = Time.get_ticks_usec()
	for frame in frames:
		var packet: PackedByteArray = encoder.encode_frame(frame)
		if packet.is_empty():
			fail("Artifact encode failed: " + encoder.get_last_error_message())
			return
		var decoded: PackedFloat32Array = decoder.decode_frame(packet)
		if not valid_pcm(decoded):
			fail("Artifact decode failed: " + decoder.get_last_error_message())
			return
		encoded_bytes += packet.size()
		packet_sizes[str(packet.size())] = int(packet_sizes.get(str(packet.size()), 0)) + 1
		decoded_all.append_array(decoded)
	var roundtrip_us: int = Time.get_ticks_usec() - begin
	var padded_samples: int = decoded_all.size() - pcm.size()
	decoded_all.resize(pcm.size())
	var output: String = ProjectSettings.globalize_path(OUTPUT_DIR)
	if DirAccess.make_dir_recursive_absolute(output) != OK:
		fail("Cannot create isolated artifact directory.")
		return
	var wav_path: String = output.path_join("opus-decoded-tone-48k-mono.wav")
	var clipped_samples: int = write_wav(wav_path, decoded_all)
	if clipped_samples < 0:
		fail(failure)
		return
	# Separate benchmark objects and timers; WAV parsing/writing is excluded.
	var benchmarks: Array[Dictionary] = []
	for pairs in [1, 4, 8, 16]:
		var result: Dictionary = run_benchmark(frames, pairs)
		if result.is_empty():
			fail(failure)
			return
		benchmarks.append(result)
	var manifest: Dictionary = {
		"fixture": fixture,
		"decoded_wav": wav_path,
		"opus_version": encoder.get_opus_version(),
		"engine_version": Engine.get_version_info(),
		"sample_rate": RATE,
		"channels": 1,
		"frame_samples": FRAME,
		"frame_count": frames.size(),
		"source_samples": pcm.size(),
		"source_duration_seconds": float(pcm.size()) / RATE,
		"output_samples": decoded_all.size(),
		"last_frame_zero_padding_samples": padded_samples,
		"output_pcm16_clipped_samples": clipped_samples,
		"encoder_lookahead_samples": lookahead,
		"lookahead_compensated": false,
		"tail_flushed": false,
		"encoded_bytes": encoded_bytes,
		"packet_size_histogram": packet_sizes,
		"artifact_roundtrip_wall_us": roundtrip_us,
		"artifact_timing_scope": "Encode/decode, validation and output accumulation; excludes WAV IO and benchmark.",
		"frequency_analysis": "Not measured here; inspect the decoded WAV with the independent analyzer. No frequency, pitch or speech-quality result is claimed.",
		"benchmarks": benchmarks,
		"benchmark_limitations": "Synthetic serialized CPU/binding throughput only. Not game FPS, network capacity, audio-device latency, simultaneous threads or a player-count guarantee."
	}
	var manifest_path: String = output.path_join("opus-roundtrip-manifest.json")
	var manifest_file := FileAccess.open(manifest_path, FileAccess.WRITE)
	if manifest_file == null:
		fail("Cannot create artifact manifest.")
		return
	manifest_file.store_string(JSON.stringify(manifest, "\t") + "\n")
	manifest_file.flush()
	if manifest_file.get_error() != OK:
		fail("Cannot write artifact manifest.")
		return
	print("LMS_OPUS_ARTIFACT_PASS frames=", frames.size(), " samples=", pcm.size(), " encoded_bytes=", encoded_bytes)
	print("MANIFEST=", manifest_path)
	print("DECODED_WAV=", wav_path)
	quit(0)
