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
    private readonly Channel<OcrRequest> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _engineGate = new(1, 1);
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private readonly Task _worker;
    private OcrEngine? _engine;
    private volatile bool _initialized;
    private bool _disposed;

    public OcrCoordinator()
    {
        _channel = Channel.CreateBounded<OcrRequest>(new BoundedChannelOptions(1)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _worker = Task.Run(() => ProcessQueueAsync(_cts.Token));
    }

    public async Task InitializeAsync(string modelDir, OcrConfig? settings = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _initializeGate.WaitAsync(_cts.Token).ConfigureAwait(false);
        try
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
                cpuThreads = Math.Clamp(settings?.CpuThreads ?? 4, 1, 16),
                confidenceThreshold = Math.Clamp(
                    settings?.ConfidenceThreshold ?? 0.55f, 0.0f, 1.0f),
                detLimitSideLen = 960,
                detThreshold = 0.2f,
                detBoxThreshold = 0.45f,
                detUnclipRatio = 1.4f,
                detMaxCandidates = 3000
            };

            var json = JsonSerializer.Serialize(config);
            var replacement = new OcrEngine();
            try
            {
                await Task.Run(() => replacement.Create(json), _cts.Token).ConfigureAwait(false);
                await _engineGate.WaitAsync(_cts.Token).ConfigureAwait(false);
                try
                {
                    var previous = _engine;
                    _engine = replacement;
                    replacement = null!;
                    _initialized = true;
                    previous?.Dispose();
                }
                finally
                {
                    _engineGate.Release();
                }
            }
            finally
            {
                replacement?.Dispose();
            }
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task<OcrDocumentResult?> RecognizeAsync(NormalizedImage image)
    {
        if (!_initialized)
        {
            Log.Warning("OCR engine not initialized");
            return null;
        }

        var tcs = new TaskCompletionSource<OcrDocumentResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _channel.Writer.WriteAsync(new OcrRequest(image, tcs), _cts.Token);
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    await _engineGate.WaitAsync(ct).ConfigureAwait(false);
                    OcrDocumentResult? result;
                    try
                    {
                        result = RunOcr(request.Image);
                    }
                    finally
                    {
                        _engineGate.Release();
                    }
                    sw.Stop();

                    request.Completion.TrySetResult(
                        result is null ? null : result with { Elapsed = sw.Elapsed });
                }
                catch (OperationCanceledException)
                {
                    request.Completion.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "OCR processing failed");
                    request.Completion.TrySetResult(null);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            while (_channel.Reader.TryRead(out var pending))
                pending.Completion.TrySetResult(null);
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
                    _engine?.Handle ?? IntPtr.Zero,
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
        if (_disposed) return;
        _disposed = true;
        _initialized = false;
        _channel.Writer.TryComplete();
        _cts.Cancel();
        _initializeGate.Wait();
        try { _worker.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        _engine?.Dispose();
        _engine = null;
        _engineGate.Dispose();
        _initializeGate.Release();
        _initializeGate.Dispose();
        _cts.Dispose();
    }

    private sealed record OcrRequest(
        NormalizedImage Image,
        TaskCompletionSource<OcrDocumentResult?> Completion);
}
