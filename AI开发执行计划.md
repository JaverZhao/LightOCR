# LightOCR AI 开发执行计划

> 技术栈：C# + .NET 8 LTS + WPF + MVVM / C++20 + Paddle Inference CPU + OpenCV 4.x
> 目标平台：Windows 10 22H2 / Windows 11，x64
> 模型：PP-OCRv6_small_det + PP-OCRv6_small_rec
> 总阶段数：5（阶段 0～4）

---

## 阶段 0：技术 POC

> 目标：验证 Paddle Inference + PP-OCRv6 + C# P/Invoke 全链路可用

| # | 任务 | 具体内容 | 验证方式 |
|---|---|---|---|
| 0.1 | 创建仓库骨架 | `LightOCR.sln`、`src/LightOCR.Native/`、`src/LightOCR.App/`、目录结构 | `dotnet build` |
| 0.2 | 建立依赖锁定 | `eng/dependencies.lock.json`、`eng/models.lock.json`，锁定 Paddle Inference / OpenCV / 模型版本和 SHA256 | 文件存在且格式正确 |
| 0.3 | 下载依赖脚本 | `eng/fetch-dependencies.ps1` + `eng/fetch-models.ps1`，自动下载并校验 SHA256 | 运行后目录结构完整 |
| 0.4 | CMake 构建 C++ DLL | `CMakeLists.txt` + `lightocr_api.h/cpp` + 最小 `ocr_engine.h/cpp`，跑通 Paddle Inference 官方 C++ OCR Pipeline | 编译通过 |
| 0.5 | 最小 C ABI | 实现 `lightocr_create`、`lightocr_recognize_bgra`、`lightocr_destroy`、`lightocr_free_buffer`，输入 BGRA → 输出 UTF-8 JSON | 用官方测试图跑通 |
| 0.6 | C# Console P/Invoke | `NativeOcrMethods.cs` + Console 测试项目，加载 DLL → 识别 → 释放 | 控制台输出 JSON |
| 0.7 | 稳定性验证 | 连续 100 次识别无崩溃、内存无泄漏 | 记录结果到 `docs/poc-report.md` |

**POC 通过门槛**：C++ 稳定推理、C# 可调用、中英日识别 OK、100 次无崩溃

---

## 阶段 1：WPF 骨架 + 图片 OCR

> 目标：可运行的桌面应用，具备非截图 OCR 能力

| # | 任务 | 具体内容 |
|---|---|---|
| 1.1 | WPF MVVM 骨架 | `App.xaml`、`MainWindow.xaml`、`ViewModel` 基类、`CommunityToolkit.Mvvm` |
| 1.2 | Serilog 日志 | `AppLifetimeService` 初始化日志，按方案 §13 规则配置 |
| 1.3 | 配置服务 | `SettingsService`、读写 JSON、原子写入、损坏恢复、Schema 迁移 |
| 1.4 | 单实例 | Named Mutex + Named Pipe IPC，第二实例传参/唤醒 |
| 1.5 | 托盘 | `TrayService` + `NotifyIcon`，菜单：截图/打开/主窗口/退出 |
| 1.6 | 原生层 SafeHandle | `SafeOcrHandle` + `OcrEngine` 类封装 `NativeOcrMethods`，启动时验证 API Version |
| 1.7 | 图片导入 | `ImageInputService` — 文件选择器、拖放、剪贴板粘贴 → `NormalizedImage` |
| 1.8 | OCR 协调 | `OcrCoordinator` — Channel 队列、调用原生层、JSON 解析、置信度过滤 |
| 1.9 | 结果展示 | `ResultWindow` — 可编辑文本框、复制全文、显示耗时、重新识别 |
| 1.10 | 剪贴板服务 | `ClipboardService` — STA 线程 + 退避重试（20/50/100ms） |

**完成定义**：打开图片 → 识别 → 展示结果 → 复制，端到端跑通

---

## 阶段 2：截图 OCR 核心体验

> 目标：快捷键截图 → 框选 → OCR → 自动复制的完整闭环

| # | 任务 | 具体内容 |
|---|---|---|
| 2.1 | 全局快捷键 | `HotkeyService` — `RegisterHotKey` + 隐藏消息窗口 + 错误反馈 |
| 2.2 | 虚拟桌面截图 | `CaptureService` — `BitBlt` + 虚拟桌面坐标（`SM_*VIRTUALSCREEN`），隐藏自身窗口后再截 |
| 2.3 | 单屏遮罩 | `CaptureOverlayWindow` — 半透明遮罩、十字光标、选区绘制、显示宽高 |
| 2.4 | 选区状态机 | `CaptureSessionService` — Idle→Preparing→Selecting→Selected→Recognizing→Completed/Cancelled/Failed→Idle |
| 2.5 | 多显示器支持 | 每屏一个遮罩窗口、`ScreenTopologyService` + `DpiService`、跨屏选区 |
| 2.6 | 截图→OCR 串联 | 框选完成 → 裁剪 BGRA → `OcrCoordinator` → 自动复制 |
| 2.7 | Toast 通知 | `ToastWindow` — 不抢焦点、2-3s 消失、多次触发不叠加 |

**完成定义**：按 `Alt+Shift+O` → 截图框选 → 识别 → Toast "已识别并复制 N 个字符"

---

## 阶段 3：设置 + 产品化

> 目标：用户可配置、可开机启动、可诊断

| # | 任务 | 具体内容 |
|---|---|---|
| 3.1 | 设置窗口 | `SettingsWindow` — 快捷键/自动复制/置信度/CPU线程数/开机启动 |
| 3.2 | 自定义快捷键 | 验证→注销旧→注册新→失败则恢复旧 |
| 3.3 | 开机启动 | HKCU Run 注册表写入/删除 |
| 3.4 | 诊断信息页 | 版本/Paddle版本/模型SHA256/OS/CPU架构/DPI模式 |
| 3.5 | 模型状态 UI | 初始化状态展示 + 重新加载按钮 |
| 3.6 | 中文界面 | 统一 UI 字符串资源、错误提示中文化 |

---

## 阶段 4：稳定性 + 打包

> 目标：达到发布质量

| # | 任务 | 具体内容 |
|---|---|---|
| 4.1 | 稳定性测试 | 500 次 OCR、500 次截图取消、8h 运行 |
| 4.2 | 内存分析 | Private Bytes / Working Set / 500 次后 vs 基线 |
| 4.3 | 性能调优 | MKL-DNN 开关对比、不同尺寸图片、CPU 线程数 |
| 4.4 | 依赖白名单 | DLL 依赖审计，删除无用 OpenCV/Paddle DLL |
| 4.5 | 日志审计 | 确认无识别全文泄露 |
| 4.6 | 便携版 | ZIP 打包、`portable.flag`、`data/` 目录 |
| 4.7 | 安装包 | Inno Setup 脚本、当前用户安装、清理旧实例 |
| 4.8 | 干净机验证 | Win10/Win11 虚拟机验证、Defender 扫描 |

---

## 依赖关系图

```
0.1~0.3 (准备)
    ↓
0.4~0.6 (编译 + P/Invoke 验证)
    ↓  [阻塞 — 必须通过]
1.1~1.6 (WPF 骨架 + 原生集成)
    ↓
1.7~1.10 (图片 OCR) ──── 独立于截图
    ↓
2.1~2.7 (截图 OCR)  ──── 依赖 1.5 + 1.6
    ↓
3.1~3.6 (设置页)   ──── 依赖 1.3
    ↓
4.1~4.8 (稳定性 + 打包)
```

## 执行顺序

严格按阶段顺序执行，每个阶段内部按编号顺序执行。阶段 0 未通过不进入阶段 1。

每完成一个任务输出：
- 修改文件清单
- 关键设计决定
- 构建命令及结果
- 测试结果
- 已知问题
- 下一步任务
