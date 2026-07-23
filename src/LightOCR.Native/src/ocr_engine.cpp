#include "ocr_engine.h"
#include "image_preprocessor.h"
#include "box_sorter.h"
#include "json_serializer.h"
#include <algorithm>
#include <sstream>
#include <chrono>
#include <cmath>
#include <cstring>
#include <stdexcept>

OcrEngine::OcrEngine() = default;
OcrEngine::~OcrEngine() = default;

int OcrEngine::Initialize(const std::string& config_json) {
    int rc = ParseOcrConfig(config_json, &config_, &last_error_);
    if (rc != 0) return rc;

    auto base_dir = config_.model_dir;
    auto det_model_path = base_dir + "/" + config_.det_model_onnx;
    detector_ = std::make_unique<TextDetector>();
    rc = detector_->Initialize(det_model_path, config_.cpu_threads, &last_error_);
    if (rc != 0) return rc;

    auto rec_model_path = base_dir + "/" + config_.rec_model_onnx;
    auto dict_path = base_dir + "/" + config_.dict_path;
    recognizer_ = std::make_unique<TextRecognizer>();
    rc = recognizer_->Initialize(
        rec_model_path, dict_path, config_.cpu_threads, &last_error_);
    if (rc != 0) return rc;

    initialized_ = true;
    return 0;
}

int OcrEngine::Recognize(const uint8_t* bgra_pixels, int width, int height, int stride,
                          std::string* out_json) {
    if (!initialized_) {
        last_error_ = "Engine not initialized";
        return 1002;
    }

    try {
        auto bgr = ImagePreprocessor::BgraToBgr(bgra_pixels, width, height, stride);
        *out_json = RunPipeline(bgr.data(), width, height);
        return 0;
    } catch (const std::exception& e) {
        last_error_ = std::string("Engine error: ") + e.what();
        return 1099;
    }
}

std::string OcrEngine::RunPipeline(const uint8_t* bgr, int w, int h) {
    auto t_start = std::chrono::high_resolution_clock::now();

    std::vector<std::vector<std::pair<int, int>>> boxes;
    int rc = detector_->Detect(bgr, w, h, &boxes, &last_error_);
    if (rc != 0)
        throw std::runtime_error("Detection failed: " + last_error_);
    auto t_det = std::chrono::high_resolution_clock::now();

    BoxSorter::SortByReadingOrder(&boxes);
    auto t_sort = std::chrono::high_resolution_clock::now();

    OcrResult result;
    result.image_width = w;
    result.image_height = h;

    for (size_t i = 0; i < boxes.size(); ++i) {
        auto& box = boxes[i];

        int min_x = w, max_x = 0, min_y = h, max_y = 0;
        for (auto& p : box) {
            min_x = std::min(min_x, p.first);
            max_x = std::max(max_x, p.first);
            min_y = std::min(min_y, p.second);
            max_y = std::max(max_y, p.second);
        }

        int box_w = max_x - min_x;
        int box_h = max_y - min_y;
        if (box_w < 3 || box_h < 3) continue;

        // Padding for recognition
        int pad_y = std::max(box_h / 2, 4);
        int pad_x = std::max(box_w / 20, 4);
        min_x = std::max(0, min_x - pad_x);
        max_x = std::min(w, max_x + pad_x);
        min_y = std::max(0, min_y - pad_y);
        max_y = std::min(h, max_y + pad_y);

        int crop_w = max_x - min_x;
        int crop_h = max_y - min_y;
        if (crop_w < 3 || crop_h < 3) continue;

        auto cropped = ImagePreprocessor::Crop(bgr, w, h, min_x, min_y, crop_w, crop_h);

        std::vector<std::string> texts;
        std::vector<float> confidences;
        rc = recognizer_->Recognize(
            cropped.data(), crop_w, crop_h, &texts, &confidences, &last_error_);
        if (rc != 0)
            throw std::runtime_error("Recognition failed: " + last_error_);

        if (!texts.empty() && !texts[0].empty()) {
            float conf = confidences.empty() ? 0.0f : confidences[0];
            if (conf >= config_.confidence_threshold) {
                OcrResult::Line line;
                line.order = static_cast<int>(i);
                line.text = texts[0];
                line.confidence = conf;
                line.polygon = box;
                result.lines.push_back(std::move(line));
            }
        }
    }

    auto t_rec = std::chrono::high_resolution_clock::now();

    for (auto& line : result.lines) {
        if (!result.full_text.empty()) result.full_text += "\r\n";
        result.full_text += line.text;
    }

    result.timing.detection_ms = std::chrono::duration_cast<std::chrono::milliseconds>(t_det - t_start).count();
    result.timing.recognition_ms = std::chrono::duration_cast<std::chrono::milliseconds>(t_rec - t_sort).count();

    return SerializeResultToJson(result);
}

std::string OcrEngine::GetLastError() const {
    return last_error_;
}
