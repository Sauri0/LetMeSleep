cmake_minimum_required(VERSION 3.22)
project(lms_opus VERSION 0.1.0 LANGUAGES C CXX)

if(NOT WIN32 OR NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "This bootstrap targets Windows x64 only.")
endif()
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(LMS_OPUS_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../deps/opus" CACHE PATH "Pinned Opus source")
set(LMS_GODOT_CPP_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../deps/godot-cpp" CACHE PATH "Pinned godot-cpp source")
foreach(dep IN ITEMS LMS_OPUS_SOURCE_DIR LMS_GODOT_CPP_SOURCE_DIR)
    if(NOT EXISTS "${${dep}}/CMakeLists.txt")
        message(FATAL_ERROR "${dep} does not contain CMakeLists.txt: ${${dep}}")
    endif()
endforeach()

set(BUILD_SHARED_LIBS OFF CACHE BOOL "Static dependencies" FORCE)
set(BUILD_TESTING OFF CACHE BOOL "No dependency test programs" FORCE)
set(OPUS_BUILD_SHARED_LIBRARY OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_TESTING OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_PROGRAMS OFF CACHE BOOL "" FORCE)
set(OPUS_ENABLE_FLOAT_API ON CACHE BOOL "" FORCE)
set(OPUS_FIXED_POINT OFF CACHE BOOL "" FORCE)
set(OPUS_DRED OFF CACHE BOOL "" FORCE)
set(OPUS_OSCE OFF CACHE BOOL "" FORCE)
set(OPUS_HARDENING ON CACHE BOOL "" FORCE)
set(OPUS_INSTALL_PKG_CONFIG_MODULE OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_CMAKE_CONFIG_MODULE OFF CACHE BOOL "" FORCE)
set(OPUS_STATIC_RUNTIME ON CACHE BOOL "" FORCE)
set(GODOTCPP_TARGET template_release CACHE STRING "Godot binding target")
set(GODOTCPP_BUILD_PROFILE "${CMAKE_CURRENT_SOURCE_DIR}/build_profile.json" CACHE FILEPATH "Minimal binding profile")
set(GODOTCPP_USE_HOT_RELOAD OFF CACHE BOOL "" FORCE)
set(GODOTCPP_USE_STATIC_CPP ON CACHE BOOL "" FORCE)
set(GODOTCPP_ENABLE_TESTING OFF CACHE BOOL "" FORCE)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>" CACHE STRING "Static MSVC runtime")

add_subdirectory("${LMS_OPUS_SOURCE_DIR}" "${CMAKE_CURRENT_BINARY_DIR}/opus" EXCLUDE_FROM_ALL)
add_subdirectory("${LMS_GODOT_CPP_SOURCE_DIR}" "${CMAKE_CURRENT_BINARY_DIR}/godot-cpp" EXCLUDE_FROM_ALL)
add_library(lms_opus SHARED src/lms_opus_codec.cpp src/register_types.cpp)
target_link_libraries(lms_opus PRIVATE godot::cpp opus)
target_include_directories(lms_opus PRIVATE "${LMS_OPUS_SOURCE_DIR}/include")
set_target_properties(lms_opus PROPERTIES
    PREFIX "" OUTPUT_NAME "lms_opus.windows.x86_64"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/smoke/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/smoke/bin"
    CXX_VISIBILITY_PRESET hidden VISIBILITY_INLINES_HIDDEN YES)
foreach(config IN ITEMS DEBUG RELEASE RELWITHDEBINFO MINSIZEREL)
    set_target_properties(lms_opus PROPERTIES RUNTIME_OUTPUT_DIRECTORY_${config} "${CMAKE_CURRENT_SOURCE_DIR}/smoke/bin")
endforeach()
if(MINGW AND CMAKE_CXX_COMPILER_ID MATCHES "Clang")
    # Pinned godot-cpp godot.cpp calls global realloc/free without stdlib.h.
    # libc++ no longer provides those declarations transitively. Supply the
    # missing standard header only to that upstream translation unit, without
    # changing the pinned dependency source tree.
    set_property(SOURCE "${LMS_GODOT_CPP_SOURCE_DIR}/src/godot.cpp"
        TARGET_DIRECTORY godot-cpp APPEND PROPERTY COMPILE_OPTIONS -include stdlib.h)
    # godot-cpp 4.5 adds -lstdc++ for Clang. LLVM-MinGW uses libc++;
    # let the compiler driver select its own C++ runtime and link it statically.
    set_property(TARGET godot-cpp PROPERTY INTERFACE_LINK_OPTIONS "")
    target_link_options(lms_opus PRIVATE -static -Wl,--no-undefined)
endif()
if(NOT MSVC)
    # Keep RTTI compatible with godot-cpp: Wrapped::_postinitialize uses
    # dynamic_cast on extension instances. Disabling it only here crashes.
    target_compile_options(lms_opus PRIVATE -Wall -Wextra -Wpedantic -fno-exceptions)
endif()
