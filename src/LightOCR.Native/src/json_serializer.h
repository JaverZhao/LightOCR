#pragma once

#include <string>
#include "ocr_pipeline.h"

std::string JsonSerialize(const OcrResult& result);
std::string JsonError(int code, const std::string& message,
                       const std::string& component, const std::string& detail);
