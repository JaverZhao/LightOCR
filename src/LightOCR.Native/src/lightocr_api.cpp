#include "lightocr_api.h"
#include "ocr_engine.h"
#include <cstring>
#include <new>

static constexpr int CURRENT_API_VERSION = 1;

LIGHTOCR_API int lightocr_get_api_version()
{
    return CURRENT_API_VERSION;
}

LIGHTOCR_API int lightocr_create(
    const char* config_json_utf8,
    LightOcrHandle* out_handle,
    LightOcrBuffer* out_error)
{
    if (!config_json_utf8 || !out_handle || !out_error)
        return 1001;

    try {
        auto* engine = new OcrEngine();
        int rc = engine->Initialize(config_json_utf8);
        if (rc != 0) {
            auto err = engine->GetLastError();
            out_error->data = static_cast<char*>(std::malloc(err.size() + 1));
            std::memcpy(out_error->data, err.c_str(), err.size() + 1);
            out_error->length = err.size();
            delete engine;
            return rc;
        }
        *out_handle = engine;
        out_error->data = nullptr;
        out_error->length = 0;
        return 0;
    }
    catch (const std::bad_alloc&) {
        return 1099;
    }
}

LIGHTOCR_API int lightocr_recognize_bgra(
    LightOcrHandle handle,
    const std::uint8_t* pixels,
    int width,
    int height,
    int stride,
    LightOcrBuffer* out_result_json,
    LightOcrBuffer* out_error)
{
    if (!handle || !pixels || width <= 0 || height <= 0 || stride < width * 4 || !out_result_json || !out_error)
        return 1001;

    try {
        auto* engine = static_cast<OcrEngine*>(handle);
        std::string result;
        int rc = engine->Recognize(pixels, width, height, stride, &result);
        if (rc != 0) {
            auto err = engine->GetLastError();
            out_error->data = static_cast<char*>(std::malloc(err.size() + 1));
            std::memcpy(out_error->data, err.c_str(), err.size() + 1);
            out_error->length = err.size();
            out_result_json->data = nullptr;
            out_result_json->length = 0;
            return rc;
        }
        out_result_json->data = static_cast<char*>(std::malloc(result.size() + 1));
        std::memcpy(out_result_json->data, result.c_str(), result.size() + 1);
        out_result_json->length = result.size();
        out_error->data = nullptr;
        out_error->length = 0;
        return 0;
    }
    catch (const std::bad_alloc&) {
        return 1099;
    }
}

LIGHTOCR_API int lightocr_destroy(
    LightOcrHandle handle,
    LightOcrBuffer* out_error)
{
    if (!handle)
        return 0;

    try {
        delete static_cast<OcrEngine*>(handle);
        if (out_error) {
            out_error->data = nullptr;
            out_error->length = 0;
        }
        return 0;
    }
    catch (...) {
        return 1099;
    }
}

LIGHTOCR_API void lightocr_free_buffer(
    LightOcrBuffer buffer)
{
    if (buffer.data)
        std::free(buffer.data);
}
