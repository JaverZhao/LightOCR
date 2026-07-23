# LightOCR

轻量级本地 OCR 工具，基于官方 PP-OCRv6 medium 检测与识别模型和 ONNX Runtime，所有识别在本地 CPU 完成，图片不会离开你的电脑。

## 特性

- 🖼️ 支持打开图片、粘贴图片、拖入图片
- 📷 截图识别
- ⚡ CPU 本地推理，无需 GPU
- 📋 自动复制识别结果
- 🖥️ 托盘常驻，全局快捷键截图

## 快速开始

从 [Releases](../../releases) 下载最新便携版，解压后运行 `LightOCR.exe` 即可。

## 开发

```powershell
# 构建
.\eng\build-app.ps1

# 打包
.\eng\package.ps1
```

### 依赖

- .NET 8 SDK
- ONNX Runtime (自动通过 runtime/ 配置)
- OpenCV (自动通过 runtime/ 配置)

## 技术栈

- PP-OCRv6_medium_det + PP-OCRv6_medium_rec
- ONNX Runtime CPU 推理
- .NET 8 / WPF
- CommunityToolkit.Mvvm
