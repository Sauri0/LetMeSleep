#include "lms_opus_codec.h"
#include <godot_cpp/core/class_db.hpp>
#include <array>
#include <cmath>
#include <cstring>

namespace godot {
void LMSOpusCodec::_bind_methods() {
    ClassDB::bind_method(D_METHOD("is_ready"), &LMSOpusCodec::is_ready);
    ClassDB::bind_method(D_METHOD("set_bitrate", "bits_per_second"), &LMSOpusCodec::set_bitrate);
    ClassDB::bind_method(D_METHOD("get_bitrate"), &LMSOpusCodec::get_bitrate);
    ClassDB::bind_method(D_METHOD("encode_frame", "pcm"), &LMSOpusCodec::encode_frame);
    ClassDB::bind_method(D_METHOD("validate_frame", "packet"), &LMSOpusCodec::validate_frame);
    ClassDB::bind_method(D_METHOD("decode_frame", "packet"), &LMSOpusCodec::decode_frame);
    ClassDB::bind_method(D_METHOD("conceal_frame"), &LMSOpusCodec::conceal_frame);
    ClassDB::bind_method(D_METHOD("reset_encoder"), &LMSOpusCodec::reset_encoder);
    ClassDB::bind_method(D_METHOD("reset_decoder"), &LMSOpusCodec::reset_decoder);
    ClassDB::bind_method(D_METHOD("get_lookahead_samples"), &LMSOpusCodec::get_lookahead_samples);
    ClassDB::bind_method(D_METHOD("get_opus_version"), &LMSOpusCodec::get_opus_version);
    ClassDB::bind_method(D_METHOD("get_last_error"), &LMSOpusCodec::get_last_error);
    ClassDB::bind_method(D_METHOD("get_last_error_message"), &LMSOpusCodec::get_last_error_message);
    BIND_CONSTANT(SAMPLE_RATE); BIND_CONSTANT(CHANNELS); BIND_CONSTANT(FRAME_SAMPLES);
    BIND_CONSTANT(FRAME_DURATION_MS); BIND_CONSTANT(MAX_PACKET_BYTES);
    BIND_CONSTANT(MIN_BITRATE); BIND_CONSTANT(MAX_BITRATE);
    BIND_CONSTANT(ERROR_NOT_READY); BIND_CONSTANT(ERROR_SAMPLE_COUNT);
    BIND_CONSTANT(ERROR_SAMPLE_VALUE); BIND_CONSTANT(ERROR_PACKET_SIZE);
    BIND_CONSTANT(ERROR_PACKET_CHANNELS); BIND_CONSTANT(ERROR_PACKET_DURATION);
    BIND_CONSTANT(ERROR_BITRATE); BIND_CONSTANT(ERROR_OUTPUT);
}

void LMSOpusCodec::clear_error() { last_error = OPUS_OK; last_error_message = String(); }
bool LMSOpusCodec::fail(int64_t code, const String &message) {
    last_error = code;
    last_error_message = message;
    return false;
}
bool LMSOpusCodec::is_ready() const { return encoder != nullptr && decoder != nullptr; }
bool LMSOpusCodec::check_ready() {
    return is_ready() || fail(ERROR_NOT_READY, "Codec initialization failed; create a new codec.");
}

LMSOpusCodec::LMSOpusCodec() {
    int error = OPUS_OK;
    encoder.reset(opus_encoder_create(SAMPLE_RATE, CHANNELS, OPUS_APPLICATION_VOIP, &error));
    if (!encoder || error != OPUS_OK) {
        encoder.reset(); fail(error == OPUS_OK ? OPUS_ALLOC_FAIL : error, "Opus encoder allocation failed."); return;
    }
    decoder.reset(opus_decoder_create(SAMPLE_RATE, CHANNELS, &error));
    if (!decoder || error != OPUS_OK) {
        decoder.reset(); encoder.reset();
        fail(error == OPUS_OK ? OPUS_ALLOC_FAIL : error, "Opus decoder allocation failed."); return;
    }
    // Persistent voice stream, hard CBR, no silent-frame suppression, no FEC.
    const std::array<int, 7> results = {
        opus_encoder_ctl(encoder.get(), OPUS_SET_BITRATE(static_cast<int>(bitrate))),
        opus_encoder_ctl(encoder.get(), OPUS_SET_VBR(0)),
        opus_encoder_ctl(encoder.get(), OPUS_SET_DTX(0)),
        opus_encoder_ctl(encoder.get(), OPUS_SET_INBAND_FEC(0)),
        opus_encoder_ctl(encoder.get(), OPUS_SET_PACKET_LOSS_PERC(0)),
        opus_encoder_ctl(encoder.get(), OPUS_SET_SIGNAL(OPUS_SIGNAL_VOICE)),
        opus_encoder_ctl(encoder.get(), OPUS_SET_COMPLEXITY(5))
    };
    for (int result : results) {
        if (result != OPUS_OK) {
            fail(result, String("Opus configuration failed: ") + opus_strerror(result));
            decoder.reset(); encoder.reset(); return;
        }
    }
}

bool LMSOpusCodec::set_bitrate(int64_t bits_per_second) {
    clear_error();
    if (!check_ready()) return false;
    if (bits_per_second < MIN_BITRATE || bits_per_second > MAX_BITRATE)
        return fail(ERROR_BITRATE, "Bitrate must be between 6000 and 64000 bits per second.");
    const int result = opus_encoder_ctl(encoder.get(), OPUS_SET_BITRATE(static_cast<int>(bits_per_second)));
    if (result != OPUS_OK) return fail(result, opus_strerror(result));
    bitrate = bits_per_second;
    return true;
}
int64_t LMSOpusCodec::get_bitrate() const { return bitrate; }

PackedByteArray LMSOpusCodec::encode_frame(const PackedFloat32Array &pcm) {
    clear_error();
    if (!check_ready()) return {};
    if (pcm.size() != FRAME_SAMPLES) {
        fail(ERROR_SAMPLE_COUNT, "Expected exactly 960 mono samples (20 ms at 48000 Hz)."); return {};
    }
    const float *samples = pcm.ptr();
    for (int i = 0; i < FRAME_SAMPLES; ++i) {
        if (!std::isfinite(samples[i]) || samples[i] < -1.0f || samples[i] > 1.0f) {
            fail(ERROR_SAMPLE_VALUE, "Every PCM sample must be finite and within [-1, 1]."); return {};
        }
    }
    std::array<unsigned char, MAX_PACKET_BYTES> data{};
    const int length = opus_encode_float(encoder.get(), samples, FRAME_SAMPLES, data.data(), MAX_PACKET_BYTES);
    if (length < 0) { fail(length, opus_strerror(length)); return {}; }
    if (length == 0 || length > MAX_PACKET_BYTES) {
        fail(ERROR_OUTPUT, "Encoder returned an invalid packet length."); return {};
    }
    PackedByteArray packet;
    if (packet.resize(length) != OK) { fail(OPUS_ALLOC_FAIL, "Cannot allocate packet output."); return {}; }
    std::memcpy(packet.ptrw(), data.data(), static_cast<size_t>(length));
    return packet;
}

bool LMSOpusCodec::validate_packet(const PackedByteArray &packet) {
    if (packet.is_empty() || packet.size() > MAX_PACKET_BYTES)
        return fail(ERROR_PACKET_SIZE, "Packet size must be 1..1275 bytes; use conceal_frame for loss.");
    const unsigned char *data = packet.ptr();
    const int length = static_cast<int>(packet.size());
    unsigned char toc = 0;
    const unsigned char *frames[48]{};
    opus_int16 sizes[48]{};
    int payload_offset = 0;
    const int parsed = opus_packet_parse(data, length, &toc, frames, sizes, &payload_offset);
    if (parsed < 0) return fail(parsed, opus_strerror(parsed));
    if (opus_packet_get_nb_channels(data) != CHANNELS)
        return fail(ERROR_PACKET_CHANNELS, "Only mono Opus packets are accepted.");
    const int samples = opus_packet_get_nb_samples(data, length, SAMPLE_RATE);
    if (samples < 0) return fail(samples, opus_strerror(samples));
    if (samples != FRAME_SAMPLES)
        return fail(ERROR_PACKET_DURATION, "Only packets totaling 20 ms (960 samples at 48000 Hz) are accepted.");
    return true;
}

PackedFloat32Array LMSOpusCodec::decode_internal(const unsigned char *data, int length) {
    std::array<float, FRAME_SAMPLES> samples{};
    const int count = opus_decode_float(decoder.get(), data, length, samples.data(), FRAME_SAMPLES, 0);
    if (count < 0) { fail(count, opus_strerror(count)); return {}; }
    if (count != FRAME_SAMPLES) {
        opus_decoder_ctl(decoder.get(), OPUS_RESET_STATE);
        fail(ERROR_OUTPUT, "Decoder returned an unexpected duration and was reset."); return {};
    }
    for (float sample : samples) {
        if (!std::isfinite(sample)) {
            opus_decoder_ctl(decoder.get(), OPUS_RESET_STATE);
            fail(ERROR_OUTPUT, "Decoder returned a nonfinite sample and was reset."); return {};
        }
    }
    PackedFloat32Array pcm;
    if (pcm.resize(FRAME_SAMPLES) != OK) { fail(OPUS_ALLOC_FAIL, "Cannot allocate PCM output."); return {}; }
    std::memcpy(pcm.ptrw(), samples.data(), sizeof(samples));
    return pcm;
}

bool LMSOpusCodec::validate_frame(const PackedByteArray &packet) {
    clear_error();
    // Structural inspection only. Neither codec history is touched, and no
    // decode or concealment operation is invoked, including on rejection.
    return check_ready() && validate_packet(packet);
}

PackedFloat32Array LMSOpusCodec::decode_frame(const PackedByteArray &packet) {
    clear_error();
    if (!check_ready() || !validate_packet(packet)) return {};
    return decode_internal(packet.ptr(), static_cast<int>(packet.size()));
}
PackedFloat32Array LMSOpusCodec::conceal_frame() {
    clear_error();
    if (!check_ready()) return {};
    return decode_internal(nullptr, 0);
}
bool LMSOpusCodec::reset_encoder() {
    clear_error();
    if (!check_ready()) return false;
    const int result = opus_encoder_ctl(encoder.get(), OPUS_RESET_STATE);
    return result == OPUS_OK || fail(result, opus_strerror(result));
}
bool LMSOpusCodec::reset_decoder() {
    clear_error();
    if (!check_ready()) return false;
    const int result = opus_decoder_ctl(decoder.get(), OPUS_RESET_STATE);
    return result == OPUS_OK || fail(result, opus_strerror(result));
}
int64_t LMSOpusCodec::get_lookahead_samples() {
    clear_error();
    if (!check_ready()) return -1;
    int samples = 0;
    const int result = opus_encoder_ctl(encoder.get(), OPUS_GET_LOOKAHEAD(&samples));
    if (result != OPUS_OK) { fail(result, opus_strerror(result)); return -1; }
    return samples;
}
String LMSOpusCodec::get_opus_version() const { return opus_get_version_string(); }
int64_t LMSOpusCodec::get_last_error() const { return last_error; }
String LMSOpusCodec::get_last_error_message() const { return last_error_message; }
}
