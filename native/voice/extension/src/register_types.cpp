#include "lms_opus_codec.h"
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/godot.hpp>

using namespace godot;
static void initialize_lms_opus(ModuleInitializationLevel level) {
    if (level == MODULE_INITIALIZATION_LEVEL_SCENE) ClassDB::register_class<LMSOpusCodec>();
}
static void uninitialize_lms_opus(ModuleInitializationLevel) {}

extern "C" {
GDExtensionBool GDE_EXPORT lms_opus_library_init(
    GDExtensionInterfaceGetProcAddress get_proc_address,
    GDExtensionClassLibraryPtr library,
    GDExtensionInitialization *initialization) {
    GDExtensionBinding::InitObject init(get_proc_address, library, initialization);
    init.register_initializer(initialize_lms_opus);
    init.register_terminator(uninitialize_lms_opus);
    init.set_minimum_library_initialization_level(MODULE_INITIALIZATION_LEVEL_SCENE);
    return init.init();
}
}
