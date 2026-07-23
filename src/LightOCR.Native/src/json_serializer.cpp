#include "json_serializer.h"
#include <sstream>

static std::string EscapeJson(const std::string& s) {
    std::string out;
    out.reserve(s.size());
    for (char c : s) {
        switch (c) {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\b': out += "\\b"; break;
            case '\f': out += "\\f"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default: out += c;
        }
    }
    return out;
}

std::string JsonSerialize(const OcrResult& result) {
    std::ostringstream json;
    json << "{\n";
    json << "  \"schemaVersion\": 1,\n";
    json << "  \"fullText\": \"" << EscapeJson(result.full_text) << "\",\n";
    json << "  \"elapsedMs\": " << result.elapsed_ms << ",\n";
    json << "  \"image\": {\n";
    json << "    \"width\": " << result.image_width << ",\n";
    json << "    \"height\": " << result.image_height << "\n";
    json << "  },\n";
    json << "  \"lines\": [\n";

    for (size_t i = 0; i < result.lines.size(); ++i) {
        auto& line = result.lines[i];
        json << "    {\n";
        json << "      \"order\": " << line.order << ",\n";
        json << "      \"text\": \"" << EscapeJson(line.text) << "\",\n";
        json << "      \"confidence\": " << line.confidence << ",\n";
        json << "      \"polygon\": [\n";
        for (size_t j = 0; j < line.polygon.size(); ++j) {
            json << "        [" << line.polygon[j].first << ", "
                 << line.polygon[j].second << "]";
            if (j < line.polygon.size() - 1) json << ",";
            json << "\n";
        }
        json << "      ]\n";
        json << "    }";
        if (i < result.lines.size() - 1) json << ",";
        json << "\n";
    }

    json << "  ],\n";
    json << "  \"timing\": {\n";
    json << "    \"preprocessMs\": " << result.timing.preprocess_ms << ",\n";
    json << "    \"detectionMs\": " << result.timing.detection_ms << ",\n";
    json << "    \"recognitionMs\": " << result.timing.recognition_ms << ",\n";
    json << "    \"postprocessMs\": " << result.timing.postprocess_ms << "\n";
    json << "  }\n";
    json << "}\n";

    return json.str();
}

std::string JsonError(int code, const std::string& message,
                       const std::string& component, const std::string& detail) {
    std::ostringstream json;
    json << "{\n";
    json << "  \"code\": " << code << ",\n";
    json << "  \"message\": \"" << EscapeJson(message) << "\",\n";
    json << "  \"component\": \"" << EscapeJson(component) << "\",\n";
    json << "  \"detail\": \"" << EscapeJson(detail) << "\"\n";
    json << "}\n";
    return json.str();
}
