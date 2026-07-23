# LightOCR：基于 PP-OCRv6 的 Windows 轻量 OCR 工具完整开发方案

> 文档用途：交给 AI 编程助手、Codex、Claude Code、Cursor 或其他代码生成代理，作为从零开发到发布的统一实施规范。  
> 文档版本：V1.0  
> 编写日期：2026-07-22  
> 工作项目名：`LightOCR`（可在正式立项时统一替换）  
> 目标平台：Windows 10 22H2 / Windows 11，x64  
> OCR 模型：`PP-OCRv6_small_det` + `PP-OCRv6_small_rec`  
> 默认推理：CPU、本地离线推理  
> 主技术栈：C# + WPF + C++20 + Paddle Inference + OpenCV

* * *

## 1\. 项目目标

开发一款 Windows 本地 OCR 工具，核心体验类似轻量截图工具：

1.  用户按下全局快捷键。
2.  软件立即截取当前虚拟桌面。
3.  用户拖动鼠标框选文字区域。
4.  软件调用本地 PP-OCRv6 模型识别文字。
5.  识别结果自动复制到系统剪贴板。
6.  屏幕角落显示“识别成功并已复制”的轻提示。
7.  用户也可以拖入、粘贴或打开图片进行 OCR。
8.  所有识别均在本机完成，默认不上传图片、文字或日志。

项目优先级排序：

```text
稳定性 > OCR 正确率 > 操作速度 > 资源占用 > 安装体积 > 视觉效果
```

* * *

## 2\. “轻量化”的项目定义

本项目中的轻量化不是简单要求安装包小于几十 MB，而是同时满足以下目标：

-   不携带 Python 运行环境。
-   不使用 Electron、Chromium 或 WebView 作为主界面。
-   默认不加载 CUDA、TensorRT 等 GPU 运行库。
-   软件可托盘常驻。
-   没有 OCR 任务时 CPU 接近空闲。
-   OCR 模型仅初始化一次，不重复加载。
-   截图、剪贴板、快捷键均直接调用 Windows 能力。
-   默认不保存截图和识别文本。
-   程序可提供免安装便携版。
-   依赖文件经过白名单清理，不打包开发期无用 DLL。
-   安装体积由 Paddle Inference、OpenCV 和模型主导，必须在阶段 0 和发布阶段进行实测审计。

如果 CPU 版 Paddle Inference 的最终体积或兼容性无法达到项目要求，允许在不修改 WPF 上层接口的前提下，将原生推理实现替换为 ONNX Runtime 或 OpenVINO。该替换属于预留能力，不是 MVP 首选方案。

* * *

## 3\. MVP 功能范围

### 3.1 必须实现

#### 截图 OCR

-   可注册全局快捷键。
-   默认快捷键建议为 `Alt + Shift + O`。
-   按快捷键后进入截图选择状态。
-   支持取消截图：`Esc` 或鼠标右键。
-   支持重新框选。
-   支持多显示器和不同 DPI 缩放。
-   支持显示框选区域尺寸。
-   框选完成后自动 OCR。
-   OCR 成功后自动复制文本。
-   可通过设置关闭自动复制。

#### 图片 OCR

-   点击按钮打开图片。
-   支持拖放图片到主窗口。
-   支持从剪贴板粘贴图片。
-   支持格式：PNG、JPG、JPEG、BMP、WEBP。
-   展示原图预览。
-   展示可编辑的识别结果。
-   提供“复制全文”按钮。

#### 系统能力

-   托盘常驻。
-   托盘菜单包含：
    -   截图识别
    -   打开图片
    -   显示主窗口
    -   设置
    -   退出
-   支持开机启动，可由用户开启。
-   支持自定义快捷键。
-   快捷键冲突时给出明确提示。
-   OCR 模型后台预加载。
-   模型加载失败时不允许静默失败。
-   支持免安装便携版和安装版。

#### 设置

-   自动复制识别结果。
-   识别完成后显示结果窗口。
-   开机启动。
-   启动后最小化到托盘。
-   全局快捷键。
-   置信度阈值。
-   OCR CPU 线程数。
-   是否保存识别历史，默认关闭。
-   日志等级，正式版默认 `Information`。

### 3.2 MVP 不实现

以下功能不进入第一版：

-   PDF 多页识别。
-   表格结构还原。
-   数学公式识别。
-   手写文字专项识别。
-   云端 OCR。
-   自动翻译。
-   连续视频 OCR。
-   录屏 OCR。
-   屏幕实时字幕。
-   图片批量目录监控。
-   账户系统。
-   在线同步。
-   自动上传错误日志。
-   GPU 推理。
-   跨平台版本。

这些能力可以在 V2 或 V3 中评估，不允许为了“看起来完整”而提前进入 MVP。

* * *

## 4\. 非功能要求

### 4.1 性能目标

以下为目标值，不可在未测试时写入宣传文案：

| 指标 | 目标 |
| --- | --- |
| 主界面冷启动 | ≤ 1.5 秒 |
| 托盘模式启动 | ≤ 1.5 秒 |
| 模型后台初始化 | 尽量 ≤ 5 秒 |
| 1080p 常规截图暖启动 OCR | P50 ≤ 600 ms |
| 1080p 常规截图暖启动 OCR | P95 ≤ 1200 ms |
| 无任务时 CPU | 平均 < 1% |
| 无模型时主程序内存 | 尽量 < 100 MB |
| 模型加载后总内存 | 实测并控制，目标 < 400 MB |
| 快捷键到遮罩出现 | ≤ 150 ms |
| 框选结束到“识别中”反馈 | ≤ 100 ms |

性能测试必须区分：

-   UI 启动时间。
-   模型加载时间。
-   图像编码时间。
-   OCR 检测时间。
-   文本框裁剪时间。
-   OCR 识别时间。
-   后处理时间。
-   剪贴板写入时间。

### 4.2 稳定性目标

-   连续执行 500 次截图 OCR 不崩溃。
-   连续运行 8 小时无持续内存增长。
-   模型初始化失败可恢复或明确提示。
-   剪贴板被占用时自动重试。
-   快捷键被其他软件占用时不崩溃。
-   用户取消截图后状态完全复位。
-   OCR 任务执行期间再次触发快捷键时有明确策略。
-   程序退出时正确注销全局快捷键并释放模型。

### 4.3 隐私和安全

-   OCR 默认完全离线。
-   默认不保存原始截图。
-   默认不保存识别文本。
-   日志中禁止记录完整识别文本。
-   日志中禁止写入图片像素或 Base64。
-   崩溃报告默认不自动上传。
-   配置文件不得保存敏感截图。
-   便携版的用户数据保存在程序目录的 `data` 下。
-   安装版的用户数据保存在 `%LocalAppData%\LightOCR`。
-   设置页明确说明“图片和文字在本地处理”。

* * *

## 5\. 技术栈

## 5.1 WPF 应用层

```text
语言：C#
框架：.NET 10
UI：WPF
架构：MVVM
目标平台：x64
发布模式：Self-contained
```

推荐 NuGet 依赖：

```text
CommunityToolkit.Mvvm
Serilog
Serilog.Sinks.File
```

尽量不引入大型 UI 组件库。界面使用原生 WPF 控件和项目内 ResourceDictionary 完成。

托盘图标优先使用：

```text
System.Windows.Forms.NotifyIcon
```

允许 WPF 项目启用 WinForms 互操作，但不要因此将主 UI 改为 WinForms。

## 5.2 原生 OCR 层

```text
语言：C++20
构建：CMake
编译器：MSVC x64
推理：Paddle Inference CPU
图像处理：OpenCV 4.x
模型：PP-OCRv6_small_det + PP-OCRv6_small_rec
接口：C ABI
上层调用：C# P/Invoke
```

原生层必须封装为单一业务入口 DLL：

```text
LightOCR.Native.dll
```

WPF 层禁止直接依赖 Paddle Inference 头文件、OpenCV 类型或模型内部类型。

## 5.3 默认版本策略

-   WPF 目标框架：`net10.0-windows`。
-   Windows 运行架构：仅 `win-x64`。
-   C++ Runtime：与最终选定的 Paddle Inference Windows 包保持 ABI 兼容。
-   OpenCV：使用官方 C++ OCR 部署文档支持的 4.x 版本范围，项目锁定具体版本。
-   PaddleOCR、Paddle Inference、模型下载地址和 SHA256 写入锁定文件，不使用“latest”下载地址构建正式版。

建议建立：

```text
eng/dependencies.lock.json
eng/models.lock.json
```

示例：

```json
{
  "paddleInference": {
    "version": "<locked-version>",
    "url": "<official-package-url>",
    "sha256": "<sha256>"
  },
  "opencv": {
    "version": "<locked-version>",
    "url": "<official-package-url>",
    "sha256": "<sha256>"
  }
}
```

* * *

## 6\. 总体架构

```text
┌──────────────────────────────────────────────┐
│                LightOCR.App                  │
│              C# / .NET / WPF                 │
│                                              │
│  Tray  Hotkey  Capture  Overlay  Result UI   │
│  Settings  Clipboard  Queue  Logging         │
└──────────────────────┬───────────────────────┘
                       │ P/Invoke / C ABI
                       │ UTF-8 JSON
┌──────────────────────▼───────────────────────┐
│             LightOCR.Native.dll              │
│                  C++20                       │
│                                              │
│  API Boundary   Engine   Pipeline   Sorting  │
│  Preprocess     Detection  Recognition       │
└───────────────┬───────────────────┬──────────┘
                │                   │
       ┌────────▼────────┐  ┌───────▼────────┐
       │ Paddle Inference│  │    OpenCV      │
       └────────┬────────┘  └────────────────┘
                │
       ┌────────▼─────────────────────────────┐
       │ PP-OCRv6_small_det / small_rec       │
       └──────────────────────────────────────┘
```

架构原则：

1.  UI 与 OCR 引擎解耦。
2.  WPF 层只认识 `IOcrEngine`。
3.  原生层只通过 C ABI 暴露能力。
4.  识别结果统一为 UTF-8 JSON。
5.  OCR 引擎为单例，进程生命周期内只初始化一次。
6.  OCR 任务默认串行执行。
7.  截图生成后直接传内存，避免写临时文件。
8.  所有原生错误必须转换为错误码和可读错误信息。
9.  不允许异常跨越 DLL ABI 边界。
10.  所有内存所有权必须明确。

* * *

## 7\. 推荐解决方案目录

```text
LightOCR/
├─ LightOCR.sln
├─ README.md
├─ LICENSES/
├─ docs/
│  ├─ architecture.md
│  ├─ native-api.md
│  ├─ release.md
│  └─ troubleshooting.md
├─ src/
│  ├─ LightOCR.App/
│  │  ├─ LightOCR.App.csproj
│  │  ├─ App.xaml
│  │  ├─ App.xaml.cs
│  │  ├─ app.manifest
│  │  ├─ Assets/
│  │  ├─ Models/
│  │  ├─ ViewModels/
│  │  ├─ Views/
│  │  │  ├─ MainWindow.xaml
│  │  │  ├─ SettingsWindow.xaml
│  │  │  ├─ ResultWindow.xaml
│  │  │  ├─ CaptureOverlayWindow.xaml
│  │  │  └─ ToastWindow.xaml
│  │  ├─ Services/
│  │  │  ├─ AppLifetimeService.cs
│  │  │  ├─ HotkeyService.cs
│  │  │  ├─ CaptureService.cs
│  │  │  ├─ CaptureSessionService.cs
│  │  │  ├─ ScreenTopologyService.cs
│  │  │  ├─ DpiService.cs
│  │  │  ├─ ClipboardService.cs
│  │  │  ├─ OcrCoordinator.cs
│  │  │  ├─ SettingsService.cs
│  │  │  ├─ StartupService.cs
│  │  │  ├─ TrayService.cs
│  │  │  └─ ToastService.cs
│  │  ├─ Interop/
│  │  │  ├─ NativeMethods.User32.cs
│  │  │  ├─ NativeMethods.Gdi32.cs
│  │  │  ├─ NativeMethods.Kernel32.cs
│  │  │  └─ NativeOcrMethods.cs
│  │  ├─ Infrastructure/
│  │  │  ├─ SingleInstanceGuard.cs
│  │  │  ├─ AsyncLock.cs
│  │  │  └─ Result.cs
│  │  └─ Resources/
│  │     ├─ Styles.xaml
│  │     └─ Strings.zh-CN.xaml
│  └─ LightOCR.Native/
│     ├─ CMakeLists.txt
│     ├─ include/
│     │  └─ lightocr_api.h
│     └─ src/
│        ├─ lightocr_api.cpp
│        ├─ ocr_engine.h
│        ├─ ocr_engine.cpp
│        ├─ ocr_config.h
│        ├─ ocr_config.cpp
│        ├─ ocr_pipeline.h
│        ├─ ocr_pipeline.cpp
│        ├─ text_detector.h
│        ├─ text_detector.cpp
│        ├─ text_recognizer.h
│        ├─ text_recognizer.cpp
│        ├─ image_preprocessor.h
│        ├─ image_preprocessor.cpp
│        ├─ box_sorter.h
│        ├─ box_sorter.cpp
│        ├─ json_serializer.h
│        └─ json_serializer.cpp
├─ tests/
│  ├─ LightOCR.App.Tests/
│  ├─ LightOCR.IntegrationTests/
│  ├─ LightOCR.Native.Tests/
│  └─ TestAssets/
├─ models/
│  ├─ README.md
│  ├─ det/
│  ├─ rec/
│  └─ dict/
├─ runtime/
│  ├─ README.md
│  ├─ paddle/
│  └─ opencv/
├─ eng/
│  ├─ dependencies.lock.json
│  ├─ models.lock.json
│  ├─ fetch-dependencies.ps1
│  ├─ fetch-models.ps1
│  ├─ build-native.ps1
│  ├─ build-app.ps1
│  ├─ test.ps1
│  └─ package.ps1
└─ installer/
   └─ LightOCR.iss
```

* * *

## 8\. 核心模块设计

## 8.1 应用生命周期

`AppLifetimeService` 负责：

-   单实例启动。
-   初始化日志。
-   加载设置。
-   初始化托盘。
-   创建隐藏消息窗口。
-   注册快捷键。
-   后台初始化 OCR。
-   处理第二实例传入的图片路径。
-   正常退出和资源释放。

启动顺序：

```text
进程启动
  → 建立单实例互斥锁
  → 初始化日志
  → 解析命令行
  → 加载配置
  → 创建托盘
  → 注册快捷键
  → 显示或隐藏主窗口
  → 后台初始化 OCR 引擎
```

退出顺序：

```text
停止接受新任务
  → 取消当前截图会话
  → 等待或取消 OCR 任务
  → 注销快捷键
  → 销毁托盘图标
  → ocr_destroy
  → 刷新日志
  → 释放互斥锁
```

## 8.2 单实例

使用命名 Mutex：

```text
Global\LightOCR.Desktop.Singleton
```

第二实例行为：

-   如果带图片路径，向第一实例传递路径并触发图片 OCR。
-   如果没有参数，唤醒第一实例主窗口。
-   IPC 可以使用 Named Pipe。
-   不允许第二实例直接退出而不通知第一实例。

## 8.3 全局快捷键

使用 Win32：

```text
RegisterHotKey
WM_HOTKEY
UnregisterHotKey
```

要求：

-   快捷键注册封装在 `IHotkeyService`。
-   注册失败时返回错误，不允许只写日志。
-   设置新快捷键时：
    1.  先验证组合。
    2.  临时注销旧快捷键。
    3.  注册新快捷键。
    4.  注册失败则恢复旧快捷键。
-   程序退出必须注销。
-   避免默认抢占系统截图快捷键。
-   快捷键消息只触发业务命令，不直接在消息回调中执行 OCR。

## 8.4 截屏

MVP 使用 GDI `BitBlt`。

截屏顺序必须是：

```text
隐藏 LightOCR 自身可见窗口
  → 等待一帧完成隐藏
  → 截取整个虚拟桌面
  → 创建截图会话
  → 显示透明遮罩窗口
```

不要先显示遮罩再截屏，否则遮罩可能进入截图。

虚拟桌面区域来自：

```text
SM_XVIRTUALSCREEN
SM_YVIRTUALSCREEN
SM_CXVIRTUALSCREEN
SM_CYVIRTUALSCREEN
```

截屏结果统一保存为：

```text
BGRA32
Top-down bitmap
物理像素坐标
```

不将截图写入磁盘。

## 8.5 多显示器与 DPI

应用清单必须启用：

```xml
<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
  PerMonitorV2
</dpiAwareness>
```

内部坐标规则：

-   屏幕、截图、OCR 输入统一使用物理像素。
-   WPF 布局使用 DIP。
-   任意边界转换必须通过 `DpiService`。
-   禁止在 ViewModel 中自行乘除缩放比例。
-   支持副屏位于主屏左侧或上方形成负坐标。
-   支持 100%、125%、150%、175%、200% 混合缩放。
-   支持横屏与竖屏组合。

推荐采用“每个显示器一个遮罩窗口”的方案：

-   每个窗口覆盖对应显示器。
-   所有窗口共享一个 `CaptureSessionService`。
-   鼠标局部坐标转换为全局物理坐标。
-   选区矩形在所有遮罩窗口同步绘制。
-   鼠标捕获后允许拖动越过显示器边界。
-   `Esc` 在任意遮罩窗口均取消整个会话。

## 8.6 截图选择状态机

状态定义：

```text
Idle
Preparing
Selecting
Selected
Recognizing
Completed
Cancelled
Failed
```

合法流转：

```text
Idle → Preparing → Selecting
Selecting → Selected
Selecting → Cancelled
Selected → Recognizing
Recognizing → Completed
Recognizing → Failed
Completed/Cancelled/Failed → Idle
```

要求：

-   任何异常都必须回到 `Idle`。
-   不允许遮罩窗口残留。
-   不允许鼠标捕获残留。
-   `Preparing` 和 `Recognizing` 阶段再次触发快捷键时，默认忽略并显示轻提示。
-   后续可增加“取消上一个任务并重新截图”，MVP 不实现。

## 8.7 OCR 任务协调

`OcrCoordinator` 负责：

-   检查 OCR 引擎状态。
-   等待后台模型初始化。
-   将图像传给原生层。
-   解析结果 JSON。
-   应用置信度过滤。
-   合并文本。
-   写入剪贴板。
-   显示结果或 Toast。
-   记录耗时指标。

OCR 队列：

```text
Channel<OcrRequest>
SingleReader = true
SingleWriter = false
```

默认串行推理，原因：

-   避免同一原生 Predictor 并发访问。
-   降低 CPU 突发占用。
-   减少模型上下文并发内存。
-   简化取消和释放逻辑。

不要假设原生 OCR 引擎是线程安全的。

## 8.8 剪贴板

使用 WPF：

```csharp
System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText);
```

剪贴板写入要求：

-   在 STA UI 线程执行。
-   最多重试 3 次。
-   建议退避：20 ms、50 ms、100 ms。
-   空文本不写入。
-   复制失败时仍展示识别结果，并给出“复制失败，点击重试”。
-   保留换行。
-   默认清理行尾多余空格。
-   不擅自删除文字内部空格。

## 8.9 图片导入

支持：

-   文件选择器。
-   拖放。
-   命令行参数。
-   剪贴板图片。

统一进入：

```text
IImageInputService → NormalizedImage
```

`NormalizedImage` 至少包含：

```text
Width
Height
Stride
PixelFormat
BGRA bytes
SourceName
```

限制策略：

-   单张图片最大尺寸和像素数必须限制。
-   建议最大 10000 × 10000。
-   建议最大 80 MP。
-   超限时提示用户缩小图片。
-   解码异常必须捕获。
-   禁止依赖文件扩展名判断真实格式。

## 8.10 结果展示

结果模型：

```csharp
public sealed record OcrDocumentResult(
    string FullText,
    IReadOnlyList<OcrLineResult> Lines,
    TimeSpan Elapsed,
    int ImageWidth,
    int ImageHeight);
```

每行至少包含：

```text
Text
Confidence
Polygon
Order
```

结果窗口功能：

-   可编辑文本框。
-   复制全文。
-   重新识别。
-   显示耗时。
-   可选显示置信度。
-   可选在原图上显示文字框。
-   默认不自动保存。

截图 OCR 默认行为：

```text
识别完成 → 自动复制 → Toast
```

当设置“显示结果窗口”开启时：

```text
识别完成 → 自动复制 → 打开结果窗口
```

* * *

## 9\. 原生 OCR DLL 设计

## 9.1 C ABI 原则

禁止直接导出 C++ 类。  
禁止导出 STL 类型。  
禁止让 C++ 异常跨 DLL 边界。  
禁止上层释放由未知 CRT 分配的内存。  
所有字符串使用 UTF-8。

建议接口：

```cpp
#pragma once

#include <cstddef>
#include <cstdint>

#ifdef LIGHTOCR_NATIVE_EXPORTS
#define LIGHTOCR_API __declspec(dllexport)
#else
#define LIGHTOCR_API __declspec(dllimport)
#endif

extern "C" {

typedef void* LightOcrHandle;

struct LightOcrBuffer {
    char* data;
    std::size_t length;
};

LIGHTOCR_API int lightocr_get_api_version();

LIGHTOCR_API int lightocr_create(
    const char* config_json_utf8,
    LightOcrHandle* out_handle,
    LightOcrBuffer* out_error);

LIGHTOCR_API int lightocr_recognize_bgra(
    LightOcrHandle handle,
    const std::uint8_t* pixels,
    int width,
    int height,
    int stride,
    LightOcrBuffer* out_result_json,
    LightOcrBuffer* out_error);

LIGHTOCR_API int lightocr_destroy(
    LightOcrHandle handle,
    LightOcrBuffer* out_error);

LIGHTOCR_API void lightocr_free_buffer(
    LightOcrBuffer buffer);
}
```

返回规则：

-   `0` 表示成功。
-   非 `0` 表示失败。
-   失败时 `out_error` 返回 UTF-8 JSON。
-   成功时 `out_result_json` 返回 UTF-8 JSON。
-   所有输出 Buffer 都由 `lightocr_free_buffer` 释放。
-   `destroy` 必须允许传入空句柄并安全返回。
-   C# 端始终使用 `try/finally` 释放 Buffer。

## 9.2 错误码

建议：

```text
0    Success
1001 InvalidArgument
1002 InvalidConfig
1003 ModelNotFound
1004 ModelLoadFailed
1005 RuntimeDependencyMissing
1006 UnsupportedCpu
1007 ImageFormatInvalid
1008 InferenceFailed
1009 DecodeFailed
1010 EngineBusy
1099 UnknownNativeError
```

错误 JSON：

```json
{
  "code": 1004,
  "message": "Failed to load recognition model.",
  "component": "TextRecognizer",
  "detail": "Internal diagnostic message without user data."
}
```

面向用户的中文提示由 C# 层映射，原生错误信息主要用于诊断。

## 9.3 配置 JSON

示例：

```json
{
  "detModelDir": "models/det",
  "recModelDir": "models/rec",
  "dictPath": "models/dict/ppocrv6_dict.txt",
  "cpuThreads": 4,
  "enableMkldnn": true,
  "confidenceThreshold": 0.55,
  "detLimitSideLen": 960,
  "detBoxThreshold": 0.6,
  "detUnclipRatio": 1.5,
  "useTextlineOrientation": false
}
```

要求：

-   所有路径在 C# 层解析为绝对路径。
-   原生层再次校验文件存在。
-   不在 C++ 代码中写死模型路径。
-   不在正式构建中自动下载模型。
-   不信任用户编辑后的 JSON。
-   数值必须进行范围校验。

## 9.4 OCR 流程

```text
BGRA 输入
  → 参数和边界校验
  → BGRA 转 BGR
  → 文本检测预处理
  → PP-OCRv6_small_det 推理
  → 检测后处理
  → 文本框过滤
  → 框排序
  → 透视裁剪
  → 识别预处理
  → PP-OCRv6_small_rec 推理
  → CTC/对应解码
  → 置信度过滤
  → 阅读顺序整理
  → JSON 输出
```

尽量复用 PaddleOCR 官方 C++ 推理代码中的：

-   预处理参数。
-   检测后处理。
-   透视裁剪。
-   文字框排序。
-   识别解码。
-   字典加载。

不要凭经验重新实现一套“看起来类似”的算法后处理。必须使用同一批测试图对照官方 Python 结果。

## 9.5 阅读顺序

MVP 主要处理屏幕截图和普通图片。

基础排序：

1.  根据框中心 Y 坐标分行。
2.  当两个框的垂直中心差低于行高阈值时视为同一行。
3.  同一行按 X 从左到右。
4.  行按 Y 从上到下。
5.  保留每一行文本。
6.  默认使用 `\r\n` 拼接。

对于双栏、复杂表格、海报和多区域布局，MVP 不承诺完美阅读顺序。

结果 JSON 示例：

```json
{
  "schemaVersion": 1,
  "fullText": "第一行\r\n第二行",
  "elapsedMs": 386,
  "image": {
    "width": 1200,
    "height": 800
  },
  "lines": [
    {
      "order": 0,
      "text": "第一行",
      "confidence": 0.987,
      "polygon": [[10, 20], [200, 20], [200, 55], [10, 55]]
    }
  ],
  "timing": {
    "preprocessMs": 8,
    "detectionMs": 121,
    "recognitionMs": 230,
    "postprocessMs": 27
  }
}
```

## 9.6 模型初始化

-   启动后后台初始化。
-   初始化任务只能运行一次。
-   初始化状态：
    -   `NotStarted`
    -   `Loading`
    -   `Ready`
    -   `Failed`
    -   `Disposed`
-   初始化失败后允许用户点击“重新加载模型”。
-   如果首次截图时模型仍在加载，显示“正在初始化 OCR”。
-   不要在每次识别时重新创建 Predictor。
-   不要在 UI 线程加载模型。
-   退出时等待正在执行的识别完成，超时后再进入强制关闭策略。

* * *

## 10\. PP-OCRv6 版本验证门槛

PP-OCRv6 属于当前模型代际，官方提供 small 检测与识别模型，并将 small 定位为桌面或移动场景。Windows C++ 本地部署文档提供了通用 OCR Pipeline 的构建方法。

但在实际开发中，不允许仅凭文档名称假设以下组合天然兼容：

```text
某个 PaddleOCR Git 提交
+ 某个 PP-OCRv6 模型包
+ 某个 Paddle Inference Windows 包
+ 某个 MSVC Runtime
+ 某个 OpenCV 版本
```

阶段 0 必须完成兼容性 POC：

1.  锁定 PaddleOCR Git commit。
2.  锁定 PP-OCRv6 small 模型。
3.  锁定 Paddle Inference Windows CPU 包。
4.  锁定 OpenCV 版本。
5.  用官方示例图片跑通 C++。
6.  用中、英、日、土耳其语、越南语截图验证。
7.  与 Python 官方 Pipeline 的输出进行对照。
8.  连续执行至少 100 次。
9.  记录内存、耗时、DLL 列表和包体积。
10.  只有 POC 通过后才进入正式 WPF 集成。

如果 POC 无法通过：

-   第一优先：调整 PaddleOCR commit 和 Paddle Inference 版本组合。
-   第二优先：从官方 C++ 代码抽取 PP-OCRv6 对应前后处理。
-   第三优先：将同一模型转为 ONNX，原生层切换 ONNX Runtime。
-   不允许回退到将 Python 打包进正式产品，除非用户重新确认产品方向。

* * *

## 11\. C# 与 C++ 互操作

## 11.1 P/Invoke

示意：

```csharp
internal static partial class NativeOcrMethods
{
    private const string DllName = "LightOCR.Native.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_get_api_version();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_create(
        IntPtr configJsonUtf8,
        out IntPtr handle,
        out NativeBuffer error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_recognize_bgra(
        IntPtr handle,
        IntPtr pixels,
        int width,
        int height,
        int stride,
        out NativeBuffer result,
        out NativeBuffer error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_destroy(
        IntPtr handle,
        out NativeBuffer error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lightocr_free_buffer(
        NativeBuffer buffer);
}
```

要求：

-   使用 `SafeHandle` 封装 OCR Handle。
-   使用专用类型封装 NativeBuffer。
-   禁止业务代码直接操作 IntPtr。
-   对 DLL 缺失、入口点缺失、架构不匹配分别提示。
-   启动时验证 API Version。
-   C# 和 C++ 同为 x64。
-   发布前使用依赖分析工具检查缺失 DLL。

## 11.2 图像内存

推荐步骤：

1.  截屏得到 BGRA `byte[]` 或非托管 Buffer。
2.  确保生命周期覆盖整个 P/Invoke。
3.  Pin 内存。
4.  调用原生 OCR。
5.  立即解除 Pin。
6.  解析 UTF-8 JSON。
7.  释放原生输出 Buffer。

必须验证：

-   `stride >= width * 4`。
-   `height > 0`。
-   `bufferLength >= stride * height`。
-   整数乘法溢出。
-   图片最大尺寸。
-   空指针。

* * *

## 12\. 配置文件

安装版：

```text
%LocalAppData%\LightOCR\settings.json
```

便携版：

```text
<app>\data\settings.json
```

示例：

```json
{
  "schemaVersion": 1,
  "hotkey": {
    "modifiers": ["Alt", "Shift"],
    "key": "O"
  },
  "ocr": {
    "autoCopy": true,
    "showResultWindow": false,
    "confidenceThreshold": 0.55,
    "cpuThreads": 4,
    "preloadModel": true
  },
  "application": {
    "startWithWindows": false,
    "startMinimized": true,
    "portableMode": false,
    "saveHistory": false
  }
}
```

规则：

-   使用原子写入：先写临时文件，再替换。
-   配置损坏时备份为 `.broken-时间戳.json`。
-   自动恢复默认配置。
-   每次升级通过 `schemaVersion` 迁移。
-   配置缺字段使用默认值。
-   配置超范围时回退并记录警告。

* * *

## 13\. 日志与诊断

日志路径：

```text
%LocalAppData%\LightOCR\logs\lightocr-.log
```

建议：

-   单文件最大 10 MB。
-   保留 7 天。
-   默认 `Information`。
-   Debug 构建可使用 `Debug`。
-   不记录图片。
-   不记录完整 OCR 文本。
-   可以记录：
    -   图片尺寸。
    -   文字框数量。
    -   识别字符数量。
    -   耗时。
    -   错误码。
    -   依赖版本。
    -   CPU 线程数。
-   文本只允许记录长度和哈希，且哈希也非必需。

设置页提供“打开日志目录”。

诊断信息页展示：

```text
App Version
Native API Version
Paddle Inference Version
OpenCV Version
Model Version
Model SHA256
OS Version
CPU Architecture
DPI Mode
```

* * *

## 14\. UI 与交互规范

## 14.1 主窗口

建议尺寸约 `860 × 600`，包括：

-   顶部：打开图片、粘贴图片、截图识别。
-   左侧或上部：图片预览。
-   右侧或下部：识别文本。
-   底部：复制全文、重新识别、设置。
-   状态栏：模型状态和耗时。

不要做复杂主页、卡片动画或大面积品牌插画。

## 14.2 截图遮罩

-   半透明黑色遮罩。
-   鼠标十字光标。
-   选区内部显示原始截图。
-   选区边框清晰。
-   显示宽高。
-   支持从任意方向拖动。
-   选区太小，例如小于 5 × 5 像素时取消。
-   `Esc` 取消。
-   鼠标右键取消。
-   框选完成后立即隐藏遮罩，再执行 OCR。
-   OCR 期间不阻塞桌面操作。

## 14.3 Toast

成功：

```text
已识别并复制 128 个字符
```

无文字：

```text
未识别到文字
```

失败：

```text
识别失败，点击查看详情
```

要求：

-   不抢焦点。
-   2～3 秒自动消失。
-   不写入截图范围。
-   多次触发时更新现有 Toast，而不是无限叠加。

## 14.4 设置窗口

设置保存策略：

-   普通开关立即保存。
-   快捷键必须点击“应用”后生效。
-   模型配置改动需要重新初始化时，明确提示。
-   不允许用户编辑模型内部文件名。

* * *

## 15\. 开机启动

推荐使用当前用户注册表：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

要求：

-   只在用户主动开启时写入。
-   关闭设置时删除。
-   路径必须带引号。
-   参数建议 `--background`。
-   便携版移动目录后应检测失效路径。
-   安装卸载时清理启动项。
-   不请求管理员权限。

* * *

## 16\. 构建与依赖管理

## 16.1 开发环境

建议安装：

```text
Visual Studio / Build Tools
.NET 10 SDK
Desktop development with .NET
Desktop development with C++
MSVC x64 toolset
Windows SDK
CMake
Git
PowerShell 7
```

开发机必须能执行：

```powershell
dotnet --info
cmake --version
git --version
```

## 16.2 构建脚本

统一入口：

```powershell
./eng/fetch-dependencies.ps1
./eng/fetch-models.ps1
./eng/build-native.ps1 -Configuration Release
./eng/build-app.ps1 -Configuration Release
./eng/test.ps1
./eng/package.ps1 -Version 1.0.0
```

所有脚本要求：

-   `$ErrorActionPreference = "Stop"`。
-   输出清晰阶段名称。
-   检查路径和哈希。
-   失败返回非零退出码。
-   不依赖开发者手动复制 DLL。
-   可重复执行。
-   不修改系统级环境变量。

## 16.3 模型管理

模型不建议直接提交普通 Git 历史。

可选方案：

1.  Git LFS。
2.  构建脚本从官方地址下载。
3.  内部制品库。

无论哪种方案，都必须：

-   锁定 URL。
-   锁定 SHA256。
-   记录模型名称。
-   记录模型版本或来源 commit。
-   记录许可证。
-   打包前校验完整性。
-   运行时校验关键文件存在。
-   不在软件运行时从网络自动下载模型，除非未来明确增加模型管理功能。

* * *

## 17\. 打包与发布

## 17.1 发布目录

建议：

```text
publish/
├─ LightOCR.exe
├─ LightOCR.dll
├─ LightOCR.Native.dll
├─ *.NET runtime files
├─ runtime/
│  ├─ Paddle Inference DLLs
│  └─ OpenCV DLLs
├─ models/
│  ├─ det/
│  ├─ rec/
│  └─ dict/
├─ licenses/
└─ README.txt
```

不允许把以下内容打入正式包：

-   Python。
-   CUDA。
-   TensorRT。
-   开发头文件。
-   `.lib` 静态导入库。
-   CMake 缓存。
-   PDB，除非放入独立 symbols 包。
-   测试图片。
-   临时日志。
-   多余语言包。
-   未使用的 OpenCV 模块 DLL。
-   未使用的 Paddle 训练依赖。

## 17.2 便携版

-   ZIP 解压即用。
-   使用 `portable.flag` 判断便携模式。
-   配置和日志写入 `data`。
-   不写注册表，除非用户主动开启开机启动。
-   删除目录即可卸载。

## 17.3 安装版

建议 Inno Setup。

安装内容：

-   当前用户安装，尽量无需管理员权限。
-   开始菜单快捷方式可选。
-   桌面快捷方式可选。
-   开机启动可选，默认关闭。
-   卸载时询问是否保留设置。
-   升级覆盖程序文件，保留用户配置。
-   安装前检查正在运行的旧实例。

## 17.4 代码签名

正式分发建议使用 Windows 代码签名证书：

-   签名 EXE。
-   签名原生 DLL。
-   签名安装包。
-   使用时间戳。
-   CI 中通过安全 Secret 调用签名。
-   不将证书私钥放入仓库。

* * *

## 18\. 测试方案

## 18.1 单元测试

C#：

-   配置迁移。
-   快捷键解析。
-   坐标转换。
-   选区矩形归一化。
-   阅读结果映射。
-   剪贴板重试策略。
-   文件类型验证。
-   错误码到用户提示映射。

C++：

-   配置 JSON 校验。
-   空指针。
-   非法尺寸。
-   Stride 校验。
-   UTF-8 输出。
-   Buffer 释放。
-   文本框排序。
-   模型文件缺失。
-   重复 destroy。
-   异常转换为错误码。

## 18.2 集成测试

-   WPF 调用原生 DLL。
-   API version 检查。
-   读取真实模型。
-   识别固定测试图片。
-   输出 JSON Schema。
-   原生内存释放。
-   进程退出无泄漏。
-   模型路径含中文和空格。
-   程序目录含中文。
-   Windows 用户名含中文。

## 18.3 OCR 测试集

至少包含：

-   简体中文 UI。
-   繁体中文。
-   英文。
-   日文。
-   土耳其语。
-   越南语。
-   数字和货币。
-   小字号。
-   白底黑字。
-   黑底白字。
-   彩色背景。
-   半透明 UI。
-   模糊截图。
-   2K 和 4K 截图。
-   旋转少量角度的图片。
-   无文字图片。
-   超长单行文字。
-   多行段落。
-   双栏截图。
-   图标与文字混排。

建议维护人工标注真值，并计算：

```text
Character Error Rate
Word Error Rate
Line Recall
False Positive Count
End-to-end latency
```

## 18.4 多显示器测试矩阵

| 主屏 | 副屏 | 缩放 | 位置 | 必测 |
| --- | --- | --- | --- | --- |
| 1920×1080 | 无 | 100% | \- | 是 |
| 2560×1440 | 无 | 125% | \- | 是 |
| 3840×2160 | 无 | 150% | \- | 是 |
| 1920×1080 | 1920×1080 | 100%/100% | 左右 | 是 |
| 2560×1440 | 1920×1080 | 125%/100% | 左右 | 是 |
| 3840×2160 | 1920×1080 | 150%/100% | 副屏在左 | 是 |
| 1920×1080 | 1080×1920 | 100%/125% | 竖屏 | 是 |
| 2560×1440 | 1920×1080 | 125%/100% | 副屏在上 | 是 |

## 18.5 稳定性测试

测试人员执行：

-   连续 OCR 100 次。
-   连续 OCR 500 次。
-   连续打开/取消遮罩 500 次。
-   切换显示器缩放。
-   远程桌面连接后 OCR。
-   睡眠唤醒后 OCR。
-   锁屏再解锁后 OCR。
-   Windows Explorer 重启后托盘状态。
-   剪贴板被占用。
-   快捷键被占用。
-   模型目录被删除。
-   原生 DLL 被删除。
-   磁盘只读。
-   配置文件损坏。

内存测试：

-   记录进程 Private Bytes。
-   记录 Working Set。
-   记录原生 Heap。
-   识别 500 次后与基线比较。
-   允许缓存稳定增长，但不允许线性增长。

* * *

## 19\. 验收标准

## 19.1 功能验收

-   \[ \] 图片可以打开、拖放、粘贴并识别。
-   \[ \] 全局快捷键在主窗口隐藏时有效。
-   \[ \] 截图框选正常。
-   \[ \] `Esc` 正常取消。
-   \[ \] 识别完成自动复制。
-   \[ \] 复制失败有明确提示。
-   \[ \] 无文字图片有明确提示。
-   \[ \] 自定义快捷键可保存并生效。
-   \[ \] 快捷键冲突可恢复旧快捷键。
-   \[ \] 托盘菜单完整。
-   \[ \] 开机启动设置有效。
-   \[ \] 模型只加载一次。
-   \[ \] 退出后无残留进程。
-   \[ \] 多显示器和混合 DPI 不偏移。
-   \[ \] 程序目录含中文时可运行。
-   \[ \] 完全断网时可运行。

## 19.2 质量验收

-   \[ \] 无未捕获 UI 异常。
-   \[ \] 无异常跨 C ABI。
-   \[ \] Native Buffer 全部释放。
-   \[ \] 连续 500 次 OCR 无崩溃。
-   \[ \] 连续 500 次截图取消无遮罩残留。
-   \[ \] 日志不包含识别全文。
-   \[ \] 正式包不包含 Python/CUDA/测试数据。
-   \[ \] 所有依赖版本锁定。
-   \[ \] 所有第三方许可证纳入发布包。
-   \[ \] 构建脚本可在干净环境复现。

## 19.3 发布门槛

只有以下条件全部满足才可发布：

```text
Phase 0 模型与 C++ POC 通过
+ MVP 功能验收通过
+ 多显示器测试通过
+ 500 次稳定性测试通过
+ 依赖和许可证审计通过
+ 安装版/便携版安装测试通过
+ Windows Defender 扫描无异常
```

* * *

## 20\. 分阶段开发计划

## 阶段 0：技术 POC

### 目标

先证明 PP-OCRv6 small 可以在选定 Windows C++ 环境稳定运行。

### 任务

1.  下载并锁定 PaddleOCR 源码 commit。
2.  下载 PP-OCRv6 small det/rec 模型。
3.  下载 Paddle Inference CPU Windows 包。
4.  编译 OpenCV 或选择兼容构建。
5.  跑通官方 C++ OCR 示例。
6.  创建最小 `LightOCR.Native.dll`。
7.  创建最小 C# Console P/Invoke 测试。
8.  输入 BGRA 内存并获得 JSON。
9.  连续识别 100 次。
10.  记录：
     -   平均耗时。
     -   P95。
     -   内存。
     -   输出准确率。
     -   DLL 数量。
     -   发布目录大小。
     -   CPU 指令集要求。

### 交付物

```text
docs/poc-report.md
src/LightOCR.Native
tests/LightOCR.Native.Tests
eng/dependencies.lock.json
eng/models.lock.json
```

### 完成定义

-   C++ 能稳定加载 PP-OCRv6 small。
-   C# 能通过 P/Invoke 调用。
-   中英日和拉丁语样本可识别。
-   连续 100 次无崩溃和明显泄漏。
-   版本组合已锁定。

* * *

## 阶段 1：WPF 基础骨架

### 目标

建立可运行、可托盘、单实例的 WPF 应用。

### 任务

-   创建解决方案。
-   配置 `net10.0-windows`、x64。
-   建立 MVVM。
-   建立日志。
-   建立配置服务。
-   建立单实例。
-   建立主窗口。
-   建立托盘。
-   建立应用退出流程。
-   接入 Native API version 检查。

### 完成定义

-   程序可启动和退出。
-   可托盘常驻。
-   第二实例能唤醒第一实例。
-   配置损坏可自动恢复。
-   Native DLL 缺失时显示可理解错误。

* * *

## 阶段 2：图片 OCR

### 目标

完成非截图场景的端到端 OCR。

### 任务

-   打开图片。
-   拖放图片。
-   粘贴图片。
-   图像解码和像素归一化。
-   调用 OCR。
-   展示结果。
-   复制结果。
-   错误处理。
-   性能埋点。

### 完成定义

-   所有目标图片格式可识别。
-   不写临时图片。
-   OCR 成功结果可编辑和复制。
-   无文字、损坏图片、超大图片提示正确。

* * *

## 阶段 3：快捷键和截图

### 目标

完成核心截图 OCR 体验。

### 任务

-   隐藏消息窗口。
-   RegisterHotKey。
-   虚拟桌面 BitBlt。
-   每屏遮罩窗口。
-   共享选区状态。
-   DPI 坐标转换。
-   取消和错误复位。
-   截图完成后 OCR。
-   自动复制。
-   Toast。

### 完成定义

-   快捷键到遮罩响应达标。
-   多显示器无偏移。
-   混合 DPI 无偏移。
-   取消后无残留。
-   识别完成自动复制。

* * *

## 阶段 4：设置和产品化

### 目标

让应用达到日常可用状态。

### 任务

-   自定义快捷键。
-   自动复制开关。
-   结果窗口开关。
-   CPU 线程数。
-   置信度阈值。
-   开机启动。
-   启动最小化。
-   模型状态。
-   日志目录。
-   诊断信息。
-   中文界面文案统一。

### 完成定义

-   设置重启后保留。
-   快捷键切换安全。
-   配置升级安全。
-   用户可查看模型和依赖状态。

* * *

## 阶段 5：稳定性与性能

### 目标

完成发布前质量门槛。

### 任务

-   500 次 OCR 测试。
-   500 次截图取消测试。
-   8 小时运行测试。
-   内存分析。
-   性能分析。
-   DLL 依赖白名单。
-   包体积审计。
-   CPU 线程调优。
-   MKL-DNN 开关对比。
-   不同尺寸图片对比。
-   错误日志审计。

### 完成定义

-   无线性内存泄漏。
-   无高频崩溃。
-   达到主要性能目标。
-   不必要依赖已删除。
-   形成 `docs/performance-report.md`。

* * *

## 阶段 6：打包发布

### 目标

产出可分发版本。

### 任务

-   自包含 x64 发布。
-   便携 ZIP。
-   Inno Setup 安装包。
-   许可证目录。
-   用户 README。
-   故障排查文档。
-   代码签名。
-   干净 Windows 虚拟机测试。
-   Windows Defender 扫描。
-   升级和卸载测试。

### 交付物

```text
LightOCR-1.0.0-win-x64-portable.zip
LightOCR-1.0.0-win-x64-setup.exe
LightOCR-1.0.0-symbols.zip
checksums.txt
release-notes.md
```

* * *

## 21\. AI 编程助手执行规则

下面内容属于强制规则，交给 AI 开发时必须保留。

### 21.1 工作方式

AI 必须：

1.  先阅读本开发方案。
2.  开始每个阶段前列出本阶段文件变更。
3.  每次只实施一个可验收的子任务。
4.  每次修改后实际构建。
5.  关键功能必须实际运行测试。
6.  不允许声称“应该可以”而不验证。
7.  遇到依赖版本不确定时，先做最小 POC。
8.  不得未经说明替换技术栈。
9.  不得用 Python 进程作为正式 OCR 后端。
10.  不得跳过多显示器和 DPI 设计。
11.  不得将图片保存到临时目录来规避内存传递。
12.  不得捕获异常后静默忽略。
13.  不得留下无法编译的伪代码。
14.  不得用 TODO 代替核心实现。
15.  不得在正式代码中写死本机绝对路径。
16.  不得提交模型、DLL 或证书前先检查仓库策略。
17.  不得把用户 OCR 文本写入日志。
18.  不得在没有测试的情况下优化或重构官方 OCR 后处理。

### 21.2 每个任务的输出格式

AI 每次提交结果时应说明：

```text
完成内容
修改文件
关键设计决定
执行的构建命令
执行的测试
测试结果
已知问题
下一步
```

### 21.3 构建失败处理

如果构建失败，AI 必须：

1.  保留完整错误核心信息。
2.  定位第一个真实错误。
3.  不通过删除功能规避错误。
4.  不随意降级依赖。
5.  修复后重新执行完整构建。
6.  说明失败原因和修复方式。

### 21.4 原生依赖处理

AI 不得：

-   猜测 Paddle Inference DLL 名称。
-   猜测 PP-OCRv6 模型内部文件结构。
-   混用不同 MSVC ABI 的预编译库。
-   将 Debug C++ Runtime 打进 Release。
-   在系统 PATH 中依赖开发机 DLL。
-   通过复制整个 Paddle 目录解决缺 DLL。
-   未审计就打包所有 OpenCV DLL。

必须通过工具确认依赖关系，并建立发布白名单。

* * *

## 22\. 建议的首批开发任务清单

按以下顺序执行，不建议调整：

```text
T001  建立仓库和解决方案
T002  建立依赖锁定文件
T003  跑通 PP-OCRv6 C++ 官方样例
T004  实现最小 C ABI
T005  实现 C# Console P/Invoke
T006  形成 POC 报告
T007  建立 WPF App 和日志
T008  实现设置服务
T009  实现单实例和 Named Pipe
T010  实现托盘
T011  实现 Native SafeHandle
T012  实现图片打开
T013  实现图片拖放和粘贴
T014  实现 OCR 结果页
T015  实现剪贴板重试
T016  实现 RegisterHotKey
T017  实现虚拟桌面 BitBlt
T018  实现单屏选区
T019  实现多屏共享选区
T020  修复混合 DPI 坐标
T021  实现自动复制和 Toast
T022  实现快捷键设置
T023  实现开机启动
T024  稳定性和内存测试
T025  依赖白名单和包体积审计
T026  便携版
T027  安装包
T028  干净系统发布测试
```

* * *

## 23\. 风险清单

| 风险 | 影响 | 处理 |
| --- | --- | --- |
| PP-OCRv6 与选定 C++ 部署代码不完全匹配 | 高 | 阶段 0 锁定 commit 和版本，先 POC |
| Paddle Inference 包体积较大 | 中/高 | 只发 CPU x64，清理依赖；必要时评估 ONNX/OpenVINO |
| Paddle DLL 与 MSVC Runtime 不兼容 | 高 | 按官方构建工具链，干净机验证 |
| 多显示器混合 DPI 坐标偏移 | 高 | PerMonitorV2 + 物理像素统一 + 测试矩阵 |
| GDI 截取部分硬件加速内容异常 | 中 | MVP 接受；V2 评估 Windows.Graphics.Capture |
| OCR 引擎非线程安全 | 高 | 单工作线程串行推理 |
| 剪贴板被占用 | 低 | STA 调用和退避重试 |
| 识别结果阅读顺序错误 | 中 | 常规行排序；复杂布局不作为 MVP 承诺 |
| 日志泄漏敏感文本 | 高 | 禁止记录全文，发布前日志审计 |
| 老 CPU 指令集不支持 | 中 | POC 确认要求，启动自检，明确最低配置 |
| 杀毒软件误报 | 中 | 代码签名、稳定安装器、避免自解压和可疑行为 |
| 模型或 DLL 被用户删除 | 中 | 启动完整性检查和修复提示 |
| Windows 10 已结束常规支持 | 中 | 实测 Win10 22H2；正式推荐 Windows 11，记录兼容目标 |

* * *

## 24\. 后续版本路线

### V1.1

-   OCR 历史，默认关闭。
-   结果框叠加预览。
-   选区放大镜。
-   快捷键直接识别剪贴板图片。
-   自定义文本拼接规则。
-   自动检查更新。

### V1.5

-   指定窗口 OCR。
-   Windows.Graphics.Capture 后端。
-   更强图片预处理。
-   图片旋转。
-   竖排文字模式。
-   多语言 UI。

### V2.0

-   批量图片。
-   PDF。
-   表格 OCR。
-   翻译。
-   可插拔推理后端。
-   OpenVINO / ONNX Runtime。
-   可选 NVIDIA GPU 包。
-   OCR API 本地服务模式。

* * *

## 25\. 官方参考资料

> 开发时应再次核对最新版本，并将最终使用的页面、Git commit 和下载包写入锁定文件。

-   PP-OCRv6 介绍：  
    [https://www.paddleocr.ai/latest/en/version3.x/algorithm/PP-OCRv6/PP-OCRv6.html](https://www.paddleocr.ai/latest/en/version3.x/algorithm/PP-OCRv6/PP-OCRv6.html)
    
-   PP-OCRv6 文本检测模块：  
    [https://www.paddleocr.ai/main/en/version3.x/module\_usage/text\_detection.html](https://www.paddleocr.ai/main/en/version3.x/module_usage/text_detection.html)
    
-   PP-OCRv6 文本识别模块：  
    [https://www.paddleocr.ai/main/en/version3.x/module\_usage/text\_recognition.html](https://www.paddleocr.ai/main/en/version3.x/module_usage/text_recognition.html)
    
-   PaddleOCR Windows C++ 本地部署：  
    [https://www.paddleocr.ai/v3.3.1/en/version3.x/deployment/cpp/OCR\_windows.html](https://www.paddleocr.ai/v3.3.1/en/version3.x/deployment/cpp/OCR_windows.html)
    
-   Paddle Inference C++ 安装：  
    [https://www.paddlepaddle.org.cn/inference/v3.0/guides/install/cpp\_install.html](https://www.paddlepaddle.org.cn/inference/v3.0/guides/install/cpp_install.html)
    
-   WPF 官方文档：  
    [https://learn.microsoft.com/en-us/dotnet/desktop/wpf/](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
    
-   RegisterHotKey：  
    [https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
    
-   WM\_HOTKEY：  
    [https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey)
    
-   BitBlt：  
    [https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt)
    
-   Windows 高 DPI 桌面应用：  
    [https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
    
-   Per-Monitor V2：  
    [https://learn.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context](https://learn.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context)
    
-   WPF Clipboard：  
    [https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard.settext](https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard.settext)
    
-   .NET 支持周期：  
    [https://learn.microsoft.com/en-us/dotnet/core/releases-and-support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
    

* * *

## 26\. 最终技术决策摘要

```text
产品：
Windows 本地轻量 OCR 工具

主程序：
C# + .NET 10 + WPF + MVVM

系统交互：
Win32 RegisterHotKey
GDI BitBlt
PerMonitorV2
WPF Clipboard
NotifyIcon

OCR：
PP-OCRv6_small_det
PP-OCRv6_small_rec
C++20
Paddle Inference CPU
OpenCV 4.x

通信：
C ABI
P/Invoke
UTF-8 JSON
SafeHandle

运行策略：
模型后台预加载
OCR 串行队列
图片内存传递
默认自动复制
默认不保存历史
默认完全离线

发布：
win-x64 Self-contained
便携 ZIP
Inno Setup 安装包
CPU-only
依赖和模型版本锁定
```

本方案的关键不是先把所有功能堆出来，而是先通过阶段 0 验证 PP-OCRv6、Paddle Inference、Windows C++ 工具链和 C# P/Invoke 的完整链路。只有原生 OCR 内核稳定之后，才进入截图、托盘和产品 UI 开发。