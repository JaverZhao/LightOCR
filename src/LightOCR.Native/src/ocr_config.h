#pragma once

#include <string>

struct OcrConfig {
    std::string model_dir = "models/onnx";
    std::string det_model_onnx = "det/inference.onnx";
    std::string rec_model_onnx = "rec/inference.onnx";
    std::string dict_path = "dict/ppocrv6_dict.txt";
    int cpu_threads = 4;
    bool enable_mkldnn = true;
    float confidence_threshold = 0.55f;
    int det_limit_side_len = 960;
    float det_box_threshold = 0.45f;
    float det_unclip_ratio = 1.4f;
    bool use_textline_orientation = false;
};

int ParseOcrConfig(const std::string& json, OcrConfig* out_config, std::string* out_error);
