using LightOCR.App.Interop;
using LightOCR.App.Models;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;

namespace LightOCR.App.Services;

public sealed class OcrCoordinator : IDisposable
{
    private readonly OcrEngine _engine;
    private readonly Channel<OcrRequest> _channel;
    private readonly CancellationTokenSource _cts = new();
    private bool _initialized;

    public OcrCoordinator()
    {
        _engine = new OcrEngine();
        _channel = Channel.CreateBounded<OcrRequest>(new BoundedChannelOptions(1)
        {
            SingleWriter = false,
            SingleReader = true
        });
    }

    public async Task InitializeAsync(string modelDir)
    {
        // Detect model file naming: subdirectory or flat
        string detOnnx, recOnnx;
        if (File.Exists(Path.Combine(modelDir, "det", "inference.onnx")))
        {
            detOnnx = "det/inference.onnx";
            recOnnx = "rec/inference.onnx";
        }
        else
        {
            detOnnx = "det_inference.onnx";
            recOnnx = "rec_inference.onnx";
        }

        var config = new
        {
            modelDir,
            detModelOnnx = detOnnx,
            recModelOnnx = recOnnx,
            dictPath = "ppocrv6_dict.txt",
            cpuThreads = 4,
            confidenceThreshold = 0.55
        };

        var json = JsonSerializer.Serialize(config);
        _engine.Create(json);
        _initialized = true;

        _ = ProcessQueueAsync(_cts.Token);
    }

    public async Task<OcrDocumentResult?> RecognizeAsync(NormalizedImage image)
    {
        if (!_initialized)
        {
            Log.Warning("OCR engine not initialized");
            return null;
        }

        var tcs = new TaskCompletionSource<OcrDocumentResult?>();
        await _channel.Writer.WriteAsync(new OcrRequest(image, tcs));
        return await tcs.Task;
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var result = RunOcr(request.Image);
                sw.Stop();

                if (result != null)
                {
                    request.Completion.TrySetResult(result with { Elapsed = sw.Elapsed });
                }
                else
                {
                    request.Completion.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "OCR processing failed");
                request.Completion.TrySetResult(null);
            }
        }
    }

    private unsafe OcrDocumentResult? RunOcr(NormalizedImage image)
    {
        fixed (byte* ptr = image.BgraBytes)
        {
            var resultBuf = default(NativeBuffer);
            var errorBuf = default(NativeBuffer);

            try
            {
                int rc = NativeOcrMethods.lightocr_recognize_bgra(
                    _engine.Handle,
                    (IntPtr)ptr,
                    image.Width,
                    image.Height,
                    image.Stride,
                    out resultBuf,
                    out errorBuf);

                if (rc != 0)
                {
                    var errMsg = NativeOcrMethods.ReadNativeBuffer(errorBuf);
                    Log.Error("OCR native error ({Code}): {Msg}", rc, errMsg);
                    return null;
                }

                var json = NativeOcrMethods.ReadNativeBuffer(resultBuf);
                return ParseResult(json, image);
            }
            finally
            {
                if (resultBuf.Data != IntPtr.Zero)
                    NativeOcrMethods.lightocr_free_buffer(resultBuf);
                if (errorBuf.Data != IntPtr.Zero)
                    NativeOcrMethods.lightocr_free_buffer(errorBuf);
            }
        }
    }

    private static OcrDocumentResult? ParseResult(string json, NormalizedImage image)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var fullText = root.GetProperty("fullText").GetString() ?? "";
            var lines = new List<OcrLineResult>();

            foreach (var line in root.GetProperty("lines").EnumerateArray())
            {
                var polygon = new List<int[]>();
                foreach (var pt in line.GetProperty("polygon").EnumerateArray())
                {
                    polygon.Add(new[] { pt[0].GetInt32(), pt[1].GetInt32() });
                }

                lines.Add(new OcrLineResult(
                    line.GetProperty("order").GetInt32(),
                    line.GetProperty("text").GetString() ?? "",
                    line.GetProperty("confidence").GetSingle(),
                    polygon.ToArray()));
            }

            return new OcrDocumentResult(fullText, lines, TimeSpan.Zero,
                image.Width, image.Height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse OCR result JSON");
            return null;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _engine.Dispose();
    }

    private sealed record OcrRequest(
        NormalizedImage Image,
        TaskCompletionSource<OcrDocumentResult?> Completion);
}
