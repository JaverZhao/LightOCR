#pragma once

#include <vector>
#include <utility>
#include <algorithm>

struct BoxSorter {
    static void SortByReadingOrder(
        std::vector<std::vector<std::pair<int, int>>>* boxes) {

        if (!boxes || boxes->empty()) return;

        // Sort by Y center first (rows), then X center (columns)
        std::sort(boxes->begin(), boxes->end(),
            [](const std::vector<std::pair<int, int>>& a,
               const std::vector<std::pair<int, int>>& b) {

                // Calculate center Y and X for each box
                int ay_sum = 0, ax_sum = 0;
                for (auto& p : a) { ay_sum += p.second; ax_sum += p.first; }
                int ay = ay_sum / static_cast<int>(a.size());
                int by_sum = 0, bx_sum = 0;
                for (auto& p : b) { by_sum += p.second; bx_sum += p.first; }
                int by = by_sum / static_cast<int>(b.size());

                // If Y centers are close (same row), sort by X
                int height_a = 0, height_b = 0;
                for (auto& p : a) height_a = std::max(height_a, p.second);
                for (auto& p : b) height_b = std::max(height_b, p.second);
                int min_a = a[0].second;
                for (auto& p : a) min_a = std::min(min_a, p.second);
                int min_b = b[0].second;
                for (auto& p : b) min_b = std::min(min_b, p.second);

                int y_dist = std::abs(ay - by);
                int avg_height = (height_a - min_a + height_b - min_b) / 2;
                int row_threshold = std::max(avg_height / 2, 5);

                if (y_dist < row_threshold) {
                    return ay < by || (ay == by && ax_sum < bx_sum);
                }
                return ay < by;
            });
    }
};
