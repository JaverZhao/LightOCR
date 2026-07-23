#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <memory>
#include <onnxruntime_cxx_api.h>

struct TextDetector {
    TextDetector() = default;
    ~TextDetector() = default;

    int Initialize(const std::string& model_path, int cpu_threads,
                   int limit_side_len, float det_thresh, float box_thresh,
                   float unclip_ratio, int max_candidates,
                   std::string* out_error);
    int Detect(const std::uint8_t* bgr_pixels, int width, int height,
               std::vector<std::vector<std::pair<int, int>>>* out_boxes,
               std::string* out_error);

private:
    Ort::Env env_{OrtLoggingLevel::ORT_LOGGING_LEVEL_WARNING, "detector"};
    std::unique_ptr<Ort::Session> session_;
    bool initialized_ = false;

    int limit_side_len_ = 960;
    float box_thresh_ = 0.45f;
    float unclip_ratio_ = 1.4f;
    float det_thresh_ = 0.2f;
    int max_candidates_ = 3000;

    std::vector<float> mean_ = {0.485f, 0.456f, 0.406f};
    std::vector<float> std_ = {0.229f, 0.224f, 0.225f};

    std::vector<uint8_t> Preprocess(const uint8_t* bgr, int w, int h,
                                    int* out_w, int* out_h,
                                    float* scale_x, float* scale_y);
    std::vector<std::vector<std::pair<int, int>>> Postprocess(
        const float* prob_map, int map_h, int map_w,
        int orig_w, int orig_h, float scale_x, float scale_y);
};
