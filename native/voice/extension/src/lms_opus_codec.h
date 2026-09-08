#pragma once

#include <godot_cpp/classes/ref_counted.hpp>
#include <godot_cpp/variant/packed_byte_array.hpp>
#include <godot_cpp/variant/packed_float32_array.hpp>
#include <godot_cpp/variant/string.hpp>
#include <opus.h>
#include <memory>

namespace godot {
class LMSOpusCodec : public RefCounted {
    GDCLASS(LMSOpusCodec, RefCounted)

    struct EncoderDeleter { void operator()(OpusEncoder *p) const { opus_encoder_destroy(p); } };
    struct DecoderDeleter { void operator()(OpusDecoder *p) const { opus_decoder_destroy(p); } };
    std::unique_ptr<OpusEncoder, EncoderDeleter> encoder;
    std::unique_ptr<OpusDecoder, DecoderDeleter> decoder;
    int64_t bitrate = 24000;
    int64_t last_error = 0;
    String last_error_message;
    void clear_error();
    bool fail(int64_t code, const String &message);
    bool check_ready();
    bool validate_packet(const PackedByteArray &packet);
    PackedFloat32Array decode_internal(const unsigned char *data, int length);

protected:
    static void _bind_methods();

public:
    enum Constants {
        SAMPLE_RATE = 48000, CHANNELS = 1, FRAME_SAMPLES = 960,
        FRAME_DURATION_MS = 20, MAX_PACKET_BYTES = 1275,
        MIN_BITRATE = 6000, MAX_BITRATE = 64000,
        ERROR_NOT_READY = -1000, ERROR_SAMPLE_COUNT = -1001,
        ERROR_SAMPLE_VALUE = -1002, ERROR_PACKET_SIZE = -1003,
        ERROR_PACKET_CHANNELS = -1004, ERROR_PACKET_DURATION = -1005,
        ERROR_BITRATE = -1006, ERROR_OUTPUT = -1007
    };
    LMSOpusCodec();
    ~LMSOpusCodec() override = default;
    bool is_ready() const;
    bool set_bitrate(int64_t bits_per_second);
    int64_t get_bitrate() const;
    PackedByteArray encode_frame(const PackedFloat32Array &pcm);
    bool validate_frame(const PackedByteArray &packet);
    PackedFloat32Array decode_frame(const PackedByteArray &packet);
    PackedFloat32Array conceal_frame();
    bool reset_encoder();
    bool reset_decoder();
    int64_t get_lookahead_samples();
    String get_opus_version() const;
    int64_t get_last_error() const;
    String get_last_error_message() const;
};
}
