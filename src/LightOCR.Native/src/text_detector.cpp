#include "text_detector.h"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <windows.h>

int TextDetector::Initialize(const std::string& model_path, int cpu_threads,
                             int limit_side_len, float det_thresh, float box_thresh,
                             float unclip_ratio, int max_candidates,
                             std::string* out_error) {
    try {
        limit_side_len_ = std::max(32, limit_side_len);
        det_thresh_ = std::clamp(det_thresh, 0.0f, 1.0f);
        box_thresh_ = std::clamp(box_thresh, 0.0f, 1.0f);
        unclip_ratio_ = std::max(0.0f, unclip_ratio);
        max_candidates_ = std::max(1, max_candidates);
        Ort::SessionOptions opts;
        opts.SetIntraOpNumThreads(std::clamp(cpu_threads, 1, 64));
        opts.SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_ENABLE_ALL);
        // Convert UTF-8 path to wide string for Windows (ORTCHAR_T = wchar_t)
        int wlen = MultiByteToWideChar(CP_UTF8, 0, model_path.c_str(), -1, nullptr, 0);
        std::wstring wpath(wlen, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, model_path.c_str(), -1, &wpath[0], wlen);
        session_ = std::make_unique<Ort::Session>(env_, wpath.c_str(), opts);
        initialized_ = true;
        return 0;
    } catch (const Ort::Exception& e) {
        if (out_error) *out_error = std::string("ONNX init failed: ") + e.what();
        return 1008;
    }
}

std::vector<uint8_t> TextDetector::Preprocess(const uint8_t* bgr, int w, int h,
                                               int* out_w, int* out_h,
                                               float* out_scale_x, float* out_scale_y) {
    int new_w = w, new_h = h;
    float ratio = 1.0f;
    if (std::max(w, h) > limit_side_len_) {
        if (w >= h) {
            ratio = static_cast<float>(limit_side_len_) / w;
            new_w = limit_side_len_;
            new_h = static_cast<int>(h * ratio);
        } else {
            ratio = static_cast<float>(limit_side_len_) / h;
            new_h = limit_side_len_;
            new_w = static_cast<int>(w * ratio);
        }
    }

    // PaddleOCR DetResizeForTest rounds each dimension independently.
    // Flooring a 191 px image to 160 px noticeably distorts small Chinese text.
    new_h = std::max(static_cast<int>(std::round(new_h / 32.0f)) * 32, 32);
    new_w = std::max(static_cast<int>(std::round(new_w / 32.0f)) * 32, 32);
    *out_w = new_w;
    *out_h = new_h;
    *out_scale_x = static_cast<float>(w) / new_w;
    *out_scale_y = static_cast<float>(h) / new_h;

    std::vector<uint8_t> resized(new_w * new_h * 3);
    for (int y = 0; y < new_h; ++y) {
        const float src_y = (y + 0.5f) * h / new_h - 0.5f;
        const int y0 = std::clamp(static_cast<int>(std::floor(src_y)), 0, h - 1);
        const int y1 = std::min(y0 + 1, h - 1);
        const float fy = std::clamp(src_y - std::floor(src_y), 0.0f, 1.0f);
        for (int x = 0; x < new_w; ++x) {
            const float src_x = (x + 0.5f) * w / new_w - 0.5f;
            const int x0 = std::clamp(static_cast<int>(std::floor(src_x)), 0, w - 1);
            const int x1 = std::min(x0 + 1, w - 1);
            const float fx = std::clamp(src_x - std::floor(src_x), 0.0f, 1.0f);
            int dst_idx = (y * new_w + x) * 3;
            for (int c = 0; c < 3; ++c) {
                const float top =
                    bgr[(y0 * w + x0) * 3 + c] * (1.0f - fx) +
                    bgr[(y0 * w + x1) * 3 + c] * fx;
                const float bottom =
                    bgr[(y1 * w + x0) * 3 + c] * (1.0f - fx) +
                    bgr[(y1 * w + x1) * 3 + c] * fx;
                resized[dst_idx + c] = static_cast<uint8_t>(
                    std::clamp(top * (1.0f - fy) + bottom * fy, 0.0f, 255.0f));
            }
        }
    }
    return resized;
}

int TextDetector::Detect(const uint8_t* bgr_pixels, int width, int height,
                         std::vector<std::vector<std::pair<int, int>>>* out_boxes,
                         std::string* out_error) {
    if (!initialized_) {
        if (out_error) *out_error = "Detector not initialized";
        return 1002;
    }

    int resized_w, resized_h;
    float scale_x, scale_y;
    auto resized = Preprocess(
        bgr_pixels, width, height, &resized_w, &resized_h, &scale_x, &scale_y);

    std::vector<int64_t> input_shape = {1, 3, resized_h, resized_w};
    std::vector<float> input_tensor(3 * resized_h * resized_w);

    for (int y = 0; y < resized_h; ++y) {
        for (int x = 0; x < resized_w; ++x) {
            int src_idx = (y * resized_w + x) * 3;
            int dst_c0 = 0 * resized_h * resized_w + y * resized_w + x;
            int dst_c1 = 1 * resized_h * resized_w + y * resized_w + x;
            int dst_c2 = 2 * resized_h * resized_w + y * resized_w + x;
            input_tensor[dst_c0] = (resized[src_idx] / 255.0f - mean_[0]) / std_[0];
            input_tensor[dst_c1] = (resized[src_idx + 1] / 255.0f - mean_[1]) / std_[1];
            input_tensor[dst_c2] = (resized[src_idx + 2] / 255.0f - mean_[2]) / std_[2];
        }
    }

    try {
        auto mem_info = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
        Ort::Value input_val = Ort::Value::CreateTensor<float>(
            mem_info, input_tensor.data(), input_tensor.size(),
            input_shape.data(), input_shape.size());

        const char* input_names[] = {"x"};
        const char* output_names[] = {"fetch_name_0"};
        Ort::RunOptions run_opts;

        auto outputs = session_->Run(run_opts, input_names, &input_val, 1, output_names, 1);

        float* output_data = outputs[0].GetTensorMutableData<float>();
        auto out_shape = outputs[0].GetTensorTypeAndShapeInfo().GetShape();
        int out_h = static_cast<int>(out_shape[2]);
        int out_w = static_cast<int>(out_shape[3]);

        *out_boxes = Postprocess(
            output_data, out_h, out_w, width, height, scale_x, scale_y);
        return 0;
    } catch (const Ort::Exception& e) {
        if (out_error) *out_error = std::string("ONNX inference failed: ") + e.what();
        return 1008;
    }
}

std::vector<std::vector<std::pair<int, int>>> TextDetector::Postprocess(
    const float* prob_map, int map_h, int map_w,
    int orig_w, int orig_h, float scale_x, float scale_y) {
    std::vector<std::vector<std::pair<int, int>>> boxes;
    std::vector<uint8_t> bitmap(map_h * map_w);
    for (int i = 0; i < map_h * map_w; ++i)
        bitmap[i] = (prob_map[i] > det_thresh_) ? 255 : 0;

    std::vector<int> labels(map_h * map_w, 0);
    int label_count = 0;

    for (int y = 1; y < map_h - 1; ++y) {
        for (int x = 1; x < map_w - 1; ++x) {
            int idx = y * map_w + x;
            if (bitmap[idx] != 255 || labels[idx] != 0) continue;
            ++label_count;
            if (label_count > max_candidates_) break;

            std::vector<std::pair<int, int>> stack;
            stack.emplace_back(x, y);
            labels[idx] = label_count;
            int min_x = x, max_x = x, min_y = y, max_y = y, pixel_count = 0;

            while (!stack.empty()) {
                auto [cx, cy] = stack.back();
                stack.pop_back();
                ++pixel_count;
                min_x = std::min(min_x, cx); max_x = std::max(max_x, cx);
                min_y = std::min(min_y, cy); max_y = std::max(max_y, cy);
                for (int d = 0; d < 8; ++d) {
                    static const int dx[] = {-1, 1, 0, 0, -1, -1, 1, 1};
                    static const int dy[] = {0, 0, -1, 1, -1, 1, -1, 1};
                    int nx = cx + dx[d], ny = cy + dy[d];
                    if (nx < 0 || nx >= map_w || ny < 0 || ny >= map_h) continue;
                    int nidx = ny * map_w + nx;
                    if (bitmap[nidx] == 255 && labels[nidx] == 0) {
                        labels[nidx] = label_count;
                        stack.emplace_back(nx, ny);
                    }
                }
            }

            float sum_prob = 0.0f;
            int score_count = 0;
            for (int sy = min_y; sy <= max_y; ++sy) {
                for (int sx = min_x; sx <= max_x; ++sx) {
                    sum_prob += prob_map[sy * map_w + sx];
                    ++score_count;
                }
            }
            float avg_prob = score_count == 0 ? 0.0f : sum_prob / score_count;
            if (avg_prob < box_thresh_) continue;

            int bbx0 = map_w, bbx1 = 0, bby0 = map_h, bby1 = 0;
            for (int ly = min_y; ly <= max_y; ++ly)
                for (int lx = min_x; lx <= max_x; ++lx)
                    if (labels[ly * map_w + lx] == label_count) {
                        bbx0 = std::min(bbx0, lx); bbx1 = std::max(bbx1, lx);
                        bby0 = std::min(bby0, ly); bby1 = std::max(bby1, ly);
                    }

            const float bw = static_cast<float>(bbx1 - bbx0 + 1);
            const float bh = static_cast<float>(bby1 - bby0 + 1);
            // DBPostProcess expands a polygon by area * ratio / perimeter.
            const int distance = static_cast<int>(
                std::ceil((bw * bh * unclip_ratio_) / (2.0f * (bw + bh))));
            int ox0 = std::max(0, static_cast<int>((bbx0 - distance) * scale_x));
            int ox1 = std::min(orig_w - 1, static_cast<int>((bbx1 + distance) * scale_x));
            int oy0 = std::max(0, static_cast<int>((bby0 - distance) * scale_y));
            int oy1 = std::min(orig_h - 1, static_cast<int>((bby1 + distance) * scale_y));
            if (ox1 - ox0 < 5 || oy1 - oy0 < 5) continue;

            std::vector<std::pair<int, int>> box;
            box.emplace_back(ox0, oy0); box.emplace_back(ox1, oy0);
            box.emplace_back(ox1, oy1); box.emplace_back(ox0, oy1);
            boxes.push_back(std::move(box));
        }
    }
    return boxes;
}
