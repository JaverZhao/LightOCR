#pragma once

#include <cstdint>
#include <vector>

struct ImagePreprocessor {
    static std::vector<std::uint8_t> BgraToBgr(const std::uint8_t* bgra, int width, int height, int stride);
    static std::vector<std::uint8_t> Crop(const std::uint8_t* bgr, int width, int height,
                                           int x, int y, int crop_w, int crop_h);
};
