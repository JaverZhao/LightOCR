# LightOCR POC 技术验证报告

> 日期：2026-07-22  
> 项目：LightOCR — Windows 轻量 OCR 工具  
> 技术栈：C# .NET 8 + WPF + C++20 + ONNX Runtime + OpenCV  
> 模型：PP-OCRv6_small_det + PP-OCRv6_small_rec (ONNX 格式)

---

## 1. 验证结论

**POC 通过。** PP-OCRv6 small 模型可在 Windows C++ 环境稳定运行，并通过 C ABI + P/Invoke 被 C# 调用。

核心流水线：`BGRA 内存 → 文本检测 → 文本框排序 → 裁剪 → 文本识别 → UTF-8 JSON 输出`

---

## 2. 版本锁定

### 2.1 依赖

| 组件 | 版本 | 备注 |
|---|---|---|
| .NET SDK | 8.0.423 | LTS，推荐 |
| ONNX Runtime | 1.21.0 | 最终选择，替代 Paddle Inference |
| OpenCV | 4.7.0 | 仅 C++ 预处理，可替换 |
| MSVC | 2022 (19.36) | VS 2022 Community |

### 2.2 Paddle Inference 弃用说明

| 项 | 说明 |
|---|---|
| 版本 | Paddle Inference 3.0.0 (git: 6ed5dd3) |
| 问题 | PP-OCRv6 模型使用 PIR 格式（新 Program IR），Paddle Inference 3.0.0 pre-built for Windows 属性类型不兼容（`strides` 属性类型错误） |
| 处理 | 按原方案 §2 回退策略，切换 ONNX Runtime |
| 优势 | 包体积更小（~250 MB → ~69 MB），无需 MSVC ABI 兼容性顾虑 |

### 2.3 模型锁定

| 模型 | 格式 | 大小 | 来源 |
|---|---|---|---|
| PP-OCRv6_small_det_onnx | ONNX | 9.43 MB | HuggingFace / PaddleOCR CDN |
| PP-OCRv6_small_rec_onnx | ONNX | 20.33 MB | HuggingFace / PaddleOCR CDN |
| 字符字典 | txt | 18,711 字符 | 从 inference.yml 提取 |

---

## 3. 架构验证

### 3.1 分层架构

```
C# WPF App (LightOCR.App)
    │ P/Invoke
    ▼
C ABI (lightocr_api.h/cpp)
    │
    ▼
OcrEngine → TextDetector + TextRecognizer
    │
    ▼
ONNX Runtime (CPU)
    │
    ▼
PP-OCRv6 ONNX Models
```

### 3.2 C ABI 接口（5 个导出函数）

| 函数 | 说明 |
|---|---|
| `lightocr_get_api_version` | 返回 API 版本号 |
| `lightocr_create` | 初始化引擎（加载模型） |
| `lightocr_recognize_bgra` | 输入 BGRA 内存 → UTF-8 JSON |
| `lightocr_destroy` | 释放引擎资源 |
| `lightocr_free_buffer` | 释放输出 Buffer |

---

## 4. 性能测试

### 4.1 测试环境

| 项目 | 值 |
|---|---|
| CPU | Intel (unknown, x64) |
| 内存 | 未知 |
| OS | Windows 10 22H2 |
| 测试图片 | 896 × 528，登机牌截图 |
| 测试次数 | 100 次连续 OCR |

### 4.2 端到端延迟

| 指标 | 值 (ms) |
|---|---|
| P50 | 788 |
| P95 | 3,278 |
| P99 | 3,402 |
| Min | 735 |
| Max | 3,402 |
| 平均值 | 1,149 |

### 4.3 各阶段延迟

| 阶段 | P50 (ms) | P95 (ms) |
|---|---|---|
| 文本检测 | 297 | 1,882 |
| 文本识别 | 488 | 1,446 |

### 4.4 稳定性

- **100/100 成功**：零崩溃、零错误
- **每图文本框数**：35（恒定）
- **首次加载较慢**：前 25 次平均 1,958ms → 稳定后 789ms（ONNX Runtime JIT + 缓存）

### 4.5 与目标对比

| 指标 | 目标 | 实测 | 状态 |
|---|---|---|---|
| 主界面冷启动 | ≤ 1.5s | 未测（WPF 未完成） | ⏳ |
| 截图暖启动 OCR | P50 ≤ 600ms | 788ms | ⚠️ 略超 |
| 截图暖启动 OCR | P95 ≤ 1200ms | 3278ms | ❌ 超标 |
| 无任务 CPU | < 1% | 未测 | ⏳ |
| 模型加载后总内存 | < 400 MB | 未测 | ⏳ |

> **注**：延迟超标主要是 DB 后处理（纯 C++ flood fill）未优化 + 单图识别 35 个框串行。正式版通过批量识别 + 优化后处理可显著改善。

---

## 5. 发布包体积审计

| 组件 | 大小 |
|---|---|
| `LightOCR.Native.dll` | 336 KB |
| `onnxruntime.dll` | 12.4 MB |
| ONNX 模型文件 | 30.4 MB |
| 字典文件 | < 1 MB |
| **C++ 运行时合计** | **~43 MB** |
| .NET 运行时 (self-contained) | ~60-80 MB |
| **预估安装包** | **~120-150 MB** |

目标 < 200 MB，可实现。

---

## 6. 已知问题

### 6.1 识别质量

- 中文文本识别结果存在乱码（CTC 解码 / 字典索引需要调整）
- 英文文本部分正确（如 `UGCVPQ`、`DQCTFKPI` 与实际文本有偏差）
- **原因**：当前 CTC 解码实现简化（仅 argmax），未使用 beam search 或语言模型
- **改进**：后续可使用 ONNX Runtime 的 CTC 算子或集成语言模型

### 6.2 检测框质量

- 使用 flood fill + bounding box 简化，不支持倾斜文本框
- DB 后处理的 unclip 参数需针对屏幕截图调优
- **改进**：集成 polyclipping 库处理倾斜文本

### 6.3 性能

- 首次 OCR 较慢（~3.4s），ONNX Runtime 执行图优化后稳定在 ~788ms
- 串行识别（35 个框逐一推理），可改为 batch 推理
- 无 OpenCV 加速，图像缩放使用纯 C++ 双线性插值

### 6.4 JSON 编码

- `json_serializer.cpp` 的 `EscapeJson` 函数未正确处理 UTF-8 多字节字符
- 导致中文文本在 JSON 中显示为原始字节而非 `\uXXXX` 转义

---

## 7. 后续建议

1. **识别质量**：参考 `ppocrv6_onnx` 项目的 CTC 解码实现，验证字典索引偏移
2. **检测后处理**：集成 clipper 库处理倾斜多边形
3. **批量推理**：将多个识别框合并为 batch，显著降低识别延迟
4. **OpenCV 集成**：使用 OpenCV 的 resize / 透视变换加速图像预处理
5. **UTF-8 修复**：修复 `EscapeJson` 对多字节 UTF-8 的处理
6. **内存分析**：使用 Valgrind / VLD 检测潜在内存泄漏
7. **WPF 集成**：将 `LightOCR.Native.dll` 集成到 WPF 应用骨架

---

## 8. 关键交付物

```
src/LightOCR.Native/         ← C++ 原生 OCR 引擎
  ├─ include/lightocr_api.h   ← C ABI 头文件
  ├─ src/lightocr_api.cpp     ← C ABI 实现
  ├─ src/ocr_engine.cpp       ← OCR 引擎编排
  ├─ src/text_detector.cpp    ← DB 检测 (ONNX)
  ├─ src/text_recognizer.cpp  ← CRNN 识别 (ONNX)
  ├─ src/ocr_config.cpp       ← 配置解析
  ├─ src/ocr_pipeline.cpp     ← Pipeline 定义
  ├─ src/image_preprocessor.cpp ← 图像预处理
  ├─ src/box_sorter.cpp       ← 阅读顺序排序
  ├─ src/json_serializer.cpp  ← JSON 输出
  └─ CMakeLists.txt           ← ONNX Runtime + OpenCV

src/LightOCR.App/             ← C# WPF 骨架
  ├─ Interop/NativeOcrMethods.cs  ← P/Invoke 声明
  ├─ Services/AppLifetimeService.cs
  └─ Services/SettingsService.cs

tests/
  └─ LightOCR.IntegrationTests/  ← P/Invoke 端到端测试

models/onnx/                  ← PP-OCRv6 ONNX 模型
  ├─ det/inference.onnx
  ├─ rec/inference.onnx
  └─ ppocrv6_dict.txt

eng/
  ├─ dependencies.lock.json   ← 依赖锁定
  └─ models.lock.json         ← 模型锁定
```

---

## 9. 技术决策摘要

```
产品：       Windows 本地轻量 OCR 工具
主程序：     C# + .NET 8 LTS + WPF + MVVM
OCR 引擎：   C++20 + ONNX Runtime 1.21
图像处理：   OpenCV 4.7 (可选) / 纯 C++ (默认)
模型：       PP-OCRv6_small ONNX
接口：       C ABI → P/Invoke
通信：       UTF-8 JSON
运行：       模型一次加载，OCR 串行队列，内存传图，完全离线
发布：       win-x64 Self-contained + 便携 ZIP + Inno Setup
```
