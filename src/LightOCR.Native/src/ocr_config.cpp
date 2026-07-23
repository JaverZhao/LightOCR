#include "ocr_config.h"
#include <sstream>

int ParseOcrConfig(const std::string& json, OcrConfig* out_config, std::string* out_error) {
    // Minimal JSON parser for config fields
    auto find_string = [&](const std::string& key, const std::string& default_val) -> std::string {
        auto pos = json.find("\"" + key + "\"");
        if (pos == std::string::npos) return default_val;
        auto val_start = json.find('"', pos + key.size() + 2);
        if (val_start == std::string::npos) return default_val;
        auto val_end = json.find('"', val_start + 1);
        if (val_end == std::string::npos) return default_val;
        return json.substr(val_start + 1, val_end - val_start - 1);
    };

    auto find_float = [&](const std::string& key, float default_val) -> float {
        auto pos = json.find("\"" + key + "\"");
        if (pos == std::string::npos) return default_val;
        auto val_start = json.find(':', pos + key.size() + 2);
        if (val_start == std::string::npos) return default_val;
        auto val_end = json.find_first_of(",}\n\r", val_start + 1);
        if (val_end == std::string::npos) return default_val;
        try { return std::stof(json.substr(val_start + 1, val_end - val_start - 1)); }
        catch (...) { return default_val; }
    };

    auto find_int = [&](const std::string& key, int default_val) -> int {
        return static_cast<int>(find_float(key, static_cast<float>(default_val)));
    };

    out_config->model_dir = find_string("modelDir", out_config->model_dir);
    out_config->det_model_onnx = find_string("detModelOnnx", out_config->det_model_onnx);
    out_config->rec_model_onnx = find_string("recModelOnnx", out_config->rec_model_onnx);
    out_config->dict_path = find_string("dictPath", out_config->dict_path);
    out_config->cpu_threads = find_int("cpuThreads", out_config->cpu_threads);
    out_config->confidence_threshold = find_float("confidenceThreshold", out_config->confidence_threshold);
    out_config->det_limit_side_len = find_int("detLimitSideLen", out_config->det_limit_side_len);
    out_config->det_box_threshold = find_float("detBoxThreshold", out_config->det_box_threshold);
    out_config->det_unclip_ratio = find_float("detUnclipRatio", out_config->det_unclip_ratio);
    out_config->det_threshold = find_float("detThreshold", out_config->det_threshold);
    out_config->det_max_candidates = find_int("detMaxCandidates", out_config->det_max_candidates);

    return 0;
}
