# LMSOpusCodec isolated native bootstrap

Godot 4.5.x Windows x64 GDExtension. Production codec source and synthetic smoke
project only; this directory does not integrate with the game. Intended engine:
Godot 4.5.2 standard single-precision build. godot-cpp uses the pinned 4.5 API.

## Build

The parent bootstrap supplies pinned source directories `../deps/opus` and
`../deps/godot-cpp`, Python 3 (for binding generation), CMake, Ninja and a portable
LLVM-MinGW UCRT x64 compiler. No downloads occur in this CMake project.

Configure with Ninja, `CMAKE_BUILD_TYPE=Release`, and absolute paths to
`CMAKE_C_COMPILER=x86_64-w64-mingw32-clang.exe` and
`CMAKE_CXX_COMPILER=x86_64-w64-mingw32-clang++.exe`. Pass
`CMAKE_MAKE_PROGRAM` and `Python3_EXECUTABLE` if those tools are not on PATH.
Override `LMS_OPUS_SOURCE_DIR` and `LMS_GODOT_CPP_SOURCE_DIR` if needed. Build
the `lms_opus` target. The output is
`smoke/bin/lms_opus.windows.x86_64.dll` (for either debug or release configuration).

Dependencies are static libraries; the extension is a DLL. The RefCounted + OS
build profile trims bindings (godot-cpp's print helper requires OS). LLVM-MinGW
also supplies the missing `stdlib.h` include to upstream `godot.cpp` through
its compile flags, preserving the pinned dependency sources. It uses `-static` for compiler runtime
dependencies; system Windows/UCRT imports remain expected. Inspect the final PE
import table before distributing; this source alone does not certify a DLL's
runtime imports. No `-march=native` or GPU acceleration is enabled here.
RTTI remains enabled in both godot-cpp and the extension: the pinned bindings
use `dynamic_cast` during object initialization. Disabling RTTI only for the
extension omits runtime type metadata needed by that operation and causes a crash.

## GDScript API

```gdscript
var encoder = LMSOpusCodec.new() # one persistent sending stream
var remote_speaker = LMSOpusCodec.new() # separate object per receiving speaker
var packet: PackedByteArray = encoder.encode_frame(mono_pcm_960)
if packet.is_empty():
    push_error(encoder.get_last_error_message())
else:
    var pcm: PackedFloat32Array = remote_speaker.decode_frame(packet)
    # Submit PCM to your separately implemented playback/jitter system.
```

| Method | Contract |
| --- | --- |
| `is_ready() -> bool` | Native encoder and decoder both initialized. Check after construction. |
| `encode_frame(PackedFloat32Array) -> PackedByteArray` | Exactly 960 mono float samples at 48000 Hz: 20 ms. Every value finite and within [-1,1]. Default hard CBR 24000 bit/s gives 60 bytes. |
| `decode_frame(PackedByteArray) -> PackedFloat32Array` | Structurally valid mono Opus packet, 1..1275 bytes, exactly 20 ms total. Returns 960 finite samples; PCM can overshoot [-1,1], so playback may need a limiter. |
| `conceal_frame() -> PackedFloat32Array` | Explicitly advance decoder by one lost 20 ms frame using Opus PLC. Also defined on fresh/reset decoder. Returns 960 finite samples. |
| `reset_encoder() -> bool` | Clear encoder history; retain bitrate/configuration. |
| `reset_decoder() -> bool` | Clear decoder history on a new stream or discontinuity. |
| `set_bitrate(int) -> bool` | Accept 6000..64000 bit/s; reject values outside bounds, retaining current configuration. Applied to subsequent frames. |
| `get_bitrate() -> int` | Configured bits per second. CBR byte rounding follows Opus. |
| `get_lookahead_samples() -> int` | Encoder lookahead at 48 kHz, or -1 on failure. Does not include frame accumulation, network, jitter buffer, or playback delay. |
| `get_opus_version() -> String` | Version string reported by the statically linked library. |
| `get_last_error() -> int` | Zero on last successful operation, Opus negative code, or wrapper code below. |
| `get_last_error_message() -> String` | Human-readable explanation, empty on success. |

Mutating operations and `get_lookahead_samples` clear the previous error before
work. Other getters preserve it. Failures return empty arrays/false/-1 as
appropriate. Retrieve the error immediately, before calling another operation.
Validation occurs before advancing native state. A malformed payload accepted by
the Opus packet parser may still fail native decoding; such a native failure is
reported, without a promise of transactional rollback. Unexpected nonfinite or
wrong-duration native output resets decoder history and returns ERROR_OUTPUT.

Wrapper errors: NOT_READY=-1000, SAMPLE_COUNT=-1001, SAMPLE_VALUE=-1002,
PACKET_SIZE=-1003, PACKET_CHANNELS=-1004, PACKET_DURATION=-1005,
BITRATE=-1006, OUTPUT=-1007. Each is exposed as an `ERROR_...` class constant.
Sample rate, channel count, duration, sample count, packet limit and bitrate bounds
are exposed as `SAMPLE_RATE`, `CHANNELS`, `FRAME_DURATION_MS`, `FRAME_SAMPLES`,
`MAX_PACKET_BYTES`, `MIN_BITRATE`, `MAX_BITRATE`.

State is owned by RAII objects with matching Opus creation/destruction in this
DLL. Callers receive Godot-managed packed arrays, never native pointers or STL
containers. Array outputs allocate; this API is intended for a serialized codec
worker/main-thread pipeline, not a hard real-time audio callback. Do not use one
instance concurrently or share a decoder between speakers. Each object contains
both an encoder and decoder; unused state is small but still allocated.

Voice application, complexity 5, VBR disabled, DTX disabled, and in-band FEC
disabled are explicit. PLC is implemented; FEC and DRED are not exposed.
An empty packet is an error, never implicit PLC. Scheduling loss recovery,
sequencing, bounded jitter buffers, encryption/authentication, resampling,
microphone capture, role pitch DSP, spatial playback, and transport are outside
this extension. Packet validation is a format/size guard, not authentication.

## Isolated synthetic smoke

After building, import the isolated `smoke` project once:

```powershell
& $Godot --headless --audio-driver Dummy --path $SmokePath --editor --import --quit
& $Godot --headless --audio-driver Dummy --path $SmokePath
```

Use the parent-supplied engine path and allowed test window. These commands must
point to this directory's `smoke` project, never the game. Headless rendering and
the Dummy audio driver avoid GPU rendering and audio devices; the script calls
no AudioServer, capture, playback, or network APIs. It quits with exit code 0 only
when every explicit check passes; failures print `FAIL:` and exit 1. Success ends
with `LMS_OPUS_SMOKE checks=... failures=0`.

Checks cover 100 persistent synthetic frames, exact CBR sizes, finite PCM and
nonzero signal, independent stream state, reset reproducibility, repeated PLC
and recovery, duration/channel/framing rejection, empty/oversized packets,
sample count and NaN/infinity/range rejection, bitrate boundaries, configuration
retention, and validation not advancing state. This is a functional smoke, not
a speech-quality, latency, real-device, network, or hostile-bitstream fuzz test.

## Optional decoded artifact and synthetic throughput test

After the isolated project import and successful primary smoke, run:

```powershell
& $Godot --headless --audio-driver Dummy --path $SmokePath --script res://roundtrip_artifacts.gd
```

This separate script reads the existing 3-second mono PCM16 fixture
`../../../voice-fixtures/tone440-48000-mono.wav` relative to `smoke`, encodes and
decodes its frames through separate persistent codec objects, and writes
`smoke/artifacts/opus-decoded-tone-48k-mono.wav` plus
`smoke/artifacts/opus-roundtrip-manifest.json`. An alternative input can be
passed after `--` as `--fixture=C:/absolute/path.wav`; it must be nonempty 48 kHz
mono PCM16 WAV, at most 16 MiB. Outputs always stay under isolated `smoke/artifacts`.

The final partial frame is zero-padded then trimmed to the original sample count.
The WAV retains codec lookahead; the script neither shifts samples nor flushes
the delayed tail. The manifest records padding, lookahead, frame/byte counts,
PCM16 clipping and timings. It deliberately makes no measured-frequency claim;
run the independent waveform analyzer on the decoded WAV for that evidence.

A separate timed benchmark uses 1, 4, 8 and 16 persistent encoder/decoder pairs,
10 unmeasured warm-up ticks and 150 measured ticks per scenario. It processes
the pairs serially, without real-time pacing. Timings include language bindings
and packed-array outputs; loop/error checks are included in wall and tick
timings. These are synthetic CPU measurements, not game FPS, network capacity,
device latency, concurrent-thread performance or a supported-player guarantee.
Success prints `LMS_OPUS_ARTIFACT_PASS` and exits 0; errors exit 1.

## Official references

- [Opus encoder API](https://opus-codec.org/docs/opus_api-1.5/group__opus__encoder.html)
- [Opus decoder API](https://opus-codec.org/docs/opus_api-1.5/group__opus__decoder.html)
- [godot-cpp 4.5 CMake options](https://github.com/godotengine/godot-cpp/blob/godot-4.5-stable/cmake/godotcpp.cmake)
- [godot-cpp 4.5 Windows runtime flags](https://github.com/godotengine/godot-cpp/blob/godot-4.5-stable/cmake/windows.cmake)

Parent bootstrap owns build provenance, verified binary hashes, dependency
licenses/notices, packaging and recorded test results.
