#include "text_recognizer.h"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <fstream>
#include <windows.h>

int TextRecognizer::Initialize(const std::string& model_path, const std::string& dict_path,
                                int cpu_threads,
                                std::string* out_error) {
    std::ifstream dict_file(dict_path, std::ios::binary);
    if (!dict_file.is_open()) {
        if (out_error) *out_error = "Cannot open dict file: " + dict_path;
        return 1003;
    }
    std::string content((std::istreambuf_iterator<char>(dict_file)),
                         std::istreambuf_iterator<char>());
    // The PP-OCRv6 dictionary is stored as one concatenated UTF-8 string.
    // Keep each Unicode code point as one class label. The exported model also
    // enables use_space_char, so space is the final label in the CTC alphabet.
    for (size_t i = 0; i < content.size();) {
        unsigned char lead = static_cast<unsigned char>(content[i]);
        int seq_len = 1;
        if ((lead & 0x80) == 0) seq_len = 1;       // ASCII
        else if ((lead & 0xE0) == 0xC0) seq_len = 2;  // 2-byte
        else if ((lead & 0xF0) == 0xE0) seq_len = 3;  // 3-byte (Chinese)
        else if ((lead & 0xF8) == 0xF0) seq_len = 4;  // 4-byte
        if (i + seq_len > content.size()) break;
        char_dict_.push_back(content.substr(i, seq_len));
        i += seq_len;
    }
    char_dict_.push_back(" ");
    if (char_dict_.empty()) {
        if (out_error) *out_error = "Empty dictionary";
        return 1007;
    }

    try {
        Ort::SessionOptions opts;
        opts.SetIntraOpNumThreads(std::clamp(cpu_threads, 1, 64));
        opts.SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_ENABLE_ALL);
        int wlen = MultiByteToWideChar(CP_UTF8, 0, model_path.c_str(), -1, nullptr, 0);
        std::wstring wpath(wlen, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, model_path.c_str(), -1, &wpath[0], wlen);
        session_ = std::make_unique<Ort::Session>(env_, wpath.c_str(), opts);

        const auto output_shape = session_->GetOutputTypeInfo(0)
                                      .GetTensorTypeAndShapeInfo()
                                      .GetShape();
        const int64_t model_class_count = output_shape.empty() ? -1 : output_shape.back();
        const int64_t expected_class_count = static_cast<int64_t>(char_dict_.size()) + 1;
        if (model_class_count > 0 && model_class_count != expected_class_count) {
            if (out_error) {
                *out_error = "Recognition dictionary/model mismatch: model has " +
                             std::to_string(model_class_count) + " classes, expected " +
                             std::to_string(expected_class_count) +
                             " (dictionary labels plus CTC blank)";
            }
            session_.reset();
            return 1007;
        }

        initialized_ = true;
        return 0;
    } catch (const Ort::Exception& e) {
        if (out_error) *out_error = std::string("ONNX init failed: ") + e.what();
        return 1008;
    }
}

int TextRecognizer::Recognize(const uint8_t* bgr_pixels, int width, int height,
                               std::vector<std::string>* out_texts,
                               std::vector<float>* out_confidences,
                               std::string* out_error) {
    if (!initialized_) {
        if (out_error) *out_error = "Recognizer not initialized";
        return 1002;
    }

    out_texts->clear();
    out_confidences->clear();

    // PaddleOCR-style preprocessing: resize to 48px height, right-pad
    const int imgH = 48;
    const int max_imgW = 3200;

    float wh_ratio = static_cast<float>(width) / height;
    float max_wh_ratio = std::max(320.0f / 48.0f, wh_ratio);
    int target_w = static_cast<int>(imgH * max_wh_ratio);
    if (target_w > max_imgW) target_w = max_imgW;

    float ratio = static_cast<float>(width) / height;
    int resized_w = static_cast<int>(std::ceil(imgH * ratio));
    if (resized_w > target_w) resized_w = target_w;
    if (resized_w < 16) resized_w = 16;

    // Resize to (resized_w, imgH)
    std::vector<uint8_t> resized(resized_w * imgH * 3);
    for (int y = 0; y < imgH; ++y) {
        float src_y = static_cast<float>(y) * height / imgH;
        int src_y0 = std::min(static_cast<int>(src_y), height - 1);
        for (int x = 0; x < resized_w; ++x) {
            float src_x = static_cast<float>(x) * width / resized_w;
            int src_x0 = std::min(static_cast<int>(src_x), width - 1);
            int src_idx = (src_y0 * width + src_x0) * 3;
            int dst_idx = (y * resized_w + x) * 3;
            resized[dst_idx] = bgr_pixels[src_idx];
            resized[dst_idx + 1] = bgr_pixels[src_idx + 1];
            resized[dst_idx + 2] = bgr_pixels[src_idx + 2];
        }
    }

    // Normalize: (x/255 - 0.5) / 0.5, producing values in [-1, 1].
    std::vector<float> input_tensor(3 * imgH * target_w);
    for (int y = 0; y < imgH; ++y) {
        for (int x = 0; x < resized_w; ++x) {
            int src_idx = (y * resized_w + x) * 3;
            int dst_c0 = y * target_w + x;
            int dst_c1 = 1 * imgH * target_w + y * target_w + x;
            int dst_c2 = 2 * imgH * target_w + y * target_w + x;
            input_tensor[dst_c0] = (resized[src_idx] / 255.0f - 0.5f) / 0.5f;
            input_tensor[dst_c1] = (resized[src_idx + 1] / 255.0f - 0.5f) / 0.5f;
            input_tensor[dst_c2] = (resized[src_idx + 2] / 255.0f - 0.5f) / 0.5f;
        }
        // Right-padding area stays 0 (already zero-initialized)
    }

    std::vector<int64_t> input_shape = {1, 3, imgH, target_w};

    try {
        auto mem_info = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
        Ort::Value input = Ort::Value::CreateTensor<float>(
            mem_info, input_tensor.data(), input_tensor.size(),
            input_shape.data(), input_shape.size());

        const char* input_names[] = {"x"};
        const char* output_names[] = {"fetch_name_0"};
        Ort::RunOptions run_opts;
        auto outputs = session_->Run(run_opts, input_names, &input, 1, output_names, 1);

        float* output_data = outputs[0].GetTensorMutableData<float>();
        auto shape_info = outputs[0].GetTensorTypeAndShapeInfo();
        auto out_shape = shape_info.GetShape();

        int seq_len = static_cast<int>(out_shape[1]);
        int num_classes = static_cast<int>(out_shape[2]);

        std::string text = CTCDecode(output_data, seq_len, num_classes);

        float confidence_sum = 0.0f;
        int confidence_count = 0;
        int previous_char_id = -1;
        for (int t = 0; t < seq_len; ++t) {
            int max_idx = 0;
            float max_val = -1e10f;
            for (int c = 0; c < num_classes; ++c) {
                float val = output_data[t * num_classes + c];
                if (val > max_val) { max_val = val; max_idx = c; }
            }
            if (max_idx > 0 &&
                max_idx <= static_cast<int>(char_dict_.size()) &&
                max_idx != previous_char_id) {
                // The exported graph already applies softmax.
                confidence_sum += std::clamp(max_val, 0.0f, 1.0f);
                ++confidence_count;
            }
            previous_char_id = (max_idx == 0) ? -1 : max_idx;
        }

        out_texts->push_back(text);
        out_confidences->push_back(
            confidence_count == 0 ? 0.0f : confidence_sum / confidence_count);
        return 0;
    } catch (const Ort::Exception& e) {
        if (out_error) *out_error = std::string("ONNX inference failed: ") + e.what();
        return 1008;
    }
}

std::string TextRecognizer::CTCDecode(const float* probs, int seq_len, int num_classes) {
    std::string result;
    int prev_char_id = -1;
    int dict_size = static_cast<int>(char_dict_.size());

    for (int t = 0; t < seq_len; ++t) {
        int max_idx = -1;
        float max_val = -1e10f;
        for (int c = 0; c < num_classes; ++c) {
            if (probs[t * num_classes + c] > max_val) {
                max_val = probs[t * num_classes + c];
                max_idx = c;
            }
        }
        // PaddleOCR prepends the CTC blank at class 0. Therefore class 1 maps
        // to dictionary entry 0, class 2 to entry 1, and so on.
        if (max_idx > 0 && max_idx <= dict_size && max_idx != prev_char_id) {
            result += char_dict_[max_idx - 1];
        }
        prev_char_id = (max_idx == 0) ? -1 : max_idx;
    }
    return result;
}
