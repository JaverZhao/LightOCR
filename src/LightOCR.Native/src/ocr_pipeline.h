#pragma once

#include <string>
#include <cstdint>
#include <vector>
#include <utility>
#include "ocr_config.h"

struct OcrResult {
    std::string full_text;
    int image_width = 0;
    int image_height = 0;
    int64_t elapsed_ms = 0;

    struct Line {
        int order = 0;
        std::string text;
        float confidence = 0.0f;
        std::vector<std::pair<int, int>> polygon;
    };
    std::vector<Line> lines;

    struct Timing {
        int64_t preprocess_ms = 0;
        int64_t detection_ms = 0;
        int64_t recognition_ms = 0;
        int64_t postprocess_ms = 0;
    };
    Timing timing;
};

int RunOcrPipeline(
    const std::uint8_t* bgra_pixels,
    int width,
    int height,
    int stride,
    OcrResult* out_result,
    std::string* out_error);

std::string SerializeResultToJson(const OcrResult& result);
