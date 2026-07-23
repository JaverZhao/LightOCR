#pragma once

#include <cstddef>
#include <cstdint>

#ifdef LIGHTOCR_NATIVE_EXPORTS
#define LIGHTOCR_API __declspec(dllexport)
#else
#define LIGHTOCR_API __declspec(dllimport)
#endif

extern "C" {

typedef void* LightOcrHandle;

struct LightOcrBuffer {
    char* data;
    std::size_t length;
};

LIGHTOCR_API int lightocr_get_api_version();

LIGHTOCR_API int lightocr_create(
    const char* config_json_utf8,
    LightOcrHandle* out_handle,
    LightOcrBuffer* out_error);

LIGHTOCR_API int lightocr_recognize_bgra(
    LightOcrHandle handle,
    const std::uint8_t* pixels,
    int width,
    int height,
    int stride,
    LightOcrBuffer* out_result_json,
    LightOcrBuffer* out_error);

LIGHTOCR_API int lightocr_destroy(
    LightOcrHandle handle,
    LightOcrBuffer* out_error);

LIGHTOCR_API void lightocr_free_buffer(
    LightOcrBuffer buffer);
}
