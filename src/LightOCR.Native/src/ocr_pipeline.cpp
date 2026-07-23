#include "ocr_pipeline.h"
#include "image_preprocessor.h"
#include "json_serializer.h"

int RunOcrPipeline(
    const std::uint8_t* bgra_pixels,
    int width,
    int height,
    int stride,
    OcrResult* out_result,
    std::string* out_error) {
    // This is a fallback - the actual pipeline runs in OcrEngine
    (void)bgra_pixels;
    (void)stride;
    (void)out_error;
    *out_result = OcrResult();
    out_result->image_width = width;
    out_result->image_height = height;
    return 0;
}

std::string SerializeResultToJson(const OcrResult& result) {
    return JsonSerialize(result);
}
