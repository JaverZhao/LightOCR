#pragma once

#include <string>
#include <cstdint>
#include <memory>
#include "text_detector.h"
#include "text_recognizer.h"
#include "ocr_pipeline.h"

class OcrEngine {
public:
    OcrEngine();
    ~OcrEngine();

    int Initialize(const std::string& config_json);
    int Recognize(const std::uint8_t* bgra_pixels, int width, int height, int stride,
                  std::string* out_json);
    std::string GetLastError() const;

private:
    std::unique_ptr<TextDetector> detector_;
    std::unique_ptr<TextRecognizer> recognizer_;
    bool initialized_ = false;
    std::string last_error_;
    OcrConfig config_;

    std::string RunPipeline(const uint8_t* bgr, int w, int h);
};
