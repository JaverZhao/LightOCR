#include "image_preprocessor.h"
#include <cstring>
#include <algorithm>

std::vector<std::uint8_t> ImagePreprocessor::BgraToBgr(
    const std::uint8_t* bgra, int width, int height, int stride) {
    std::vector<std::uint8_t> bgr(width * height * 3);
    for (int y = 0; y < height; ++y) {
        const auto* src = bgra + y * stride;
        auto* dst = bgr.data() + y * width * 3;
        for (int x = 0; x < width; ++x) {
            dst[x * 3 + 0] = src[x * 4 + 0];
            dst[x * 3 + 1] = src[x * 4 + 1];
            dst[x * 3 + 2] = src[x * 4 + 2];
        }
    }
    return bgr;
}

std::vector<std::uint8_t> ImagePreprocessor::Crop(
    const std::uint8_t* bgr, int width, int height,
    int x, int y, int crop_w, int crop_h) {
    x = std::max(0, x);
    y = std::max(0, y);
    crop_w = std::min(crop_w, width - x);
    crop_h = std::min(crop_h, height - y);

    if (crop_w <= 0 || crop_h <= 0) return {};

    std::vector<std::uint8_t> cropped(crop_w * crop_h * 3);
    for (int row = 0; row < crop_h; ++row) {
        const auto* src = bgr + (y + row) * width * 3 + x * 3;
        auto* dst = cropped.data() + row * crop_w * 3;
        std::memcpy(dst, src, crop_w * 3);
    }
    return cropped;
}
