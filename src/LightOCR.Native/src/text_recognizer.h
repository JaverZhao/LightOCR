#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <memory>
#include <onnxruntime_cxx_api.h>

struct TextRecognizer {
    TextRecognizer() = default;
    ~TextRecognizer() = default;

    int Initialize(const std::string& model_path, const std::string& dict_path,
                   std::string* out_error);
    int Recognize(const std::uint8_t* bgr_pixels, int width, int height,
                  std::vector<std::string>* out_texts,
                  std::vector<float>* out_confidences,
                  std::string* out_error);

private:
    Ort::Env env_{OrtLoggingLevel::ORT_LOGGING_LEVEL_WARNING, "recognizer"};
    std::unique_ptr<Ort::Session> session_;
    bool initialized_ = false;

    std::vector<std::string> char_dict_;
    int rec_image_height_ = 48;

    // Find the max width for the rec model
    static constexpr int kTargetWidth = 320;

    std::string CTCDecode(const float* probs, int seq_len, int num_classes);
};
