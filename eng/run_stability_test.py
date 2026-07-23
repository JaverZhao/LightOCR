"""LightOCR POC Stability Test - 100 consecutive OCR runs"""
import ctypes
import json
import os
import time
import subprocess

os.add_dll_directory(r"J:\Javer_Workplace\dev\LightOCR\src\LightOCR.Native\build\Debug")
os.add_dll_directory(r"J:\Javer_Workplace\dev\LightOCR\runtime\onnxruntime\onnxruntime-win-x64-1.21.0\lib")

dll = ctypes.CDLL(r"J:\Javer_Workplace\dev\LightOCR\src\LightOCR.Native\build\Debug\LightOCR.Native.dll")
models_dir = r"J:\Javer_Workplace\dev\LightOCR\models\onnx"
test_img = r"J:\Javer_Workplace\dev\LightOCR\tests\TestAssets\test_ocr.png"

class NativeBuffer(ctypes.Structure):
    _fields_ = [("data", ctypes.c_char_p), ("length", ctypes.c_size_t)]

dll.lightocr_get_api_version.restype = ctypes.c_int
dll.lightocr_create.restype = ctypes.c_int
dll.lightocr_create.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(NativeBuffer)]
dll.lightocr_recognize_bgra.restype = ctypes.c_int
dll.lightocr_recognize_bgra.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint8), ctypes.c_int,
    ctypes.c_int, ctypes.c_int, ctypes.POINTER(NativeBuffer), ctypes.POINTER(NativeBuffer)]
dll.lightocr_destroy.restype = ctypes.c_int
dll.lightocr_destroy.argtypes = [ctypes.c_void_p, ctypes.POINTER(NativeBuffer)]
dll.lightocr_free_buffer.argtypes = [NativeBuffer]

import cv2
img = cv2.imread(test_img, cv2.IMREAD_COLOR)
h, w = img.shape[:2]
bgra = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
pixels = bgra.ctypes.data_as(ctypes.POINTER(ctypes.c_uint8))

config = json.dumps({
    "modelDir": models_dir,
    "detModelOnnx": "det/inference.onnx",
    "recModelOnnx": "rec/inference.onnx",
    "dictPath": "ppocrv6_dict.txt",
    "cpuThreads": 4,
    "confidenceThreshold": 0.3
}).encode('utf-8')

# Initialize once
handle = ctypes.c_void_p()
err = NativeBuffer()
rc = dll.lightocr_create(config, ctypes.byref(handle), ctypes.byref(err))
assert rc == 0, f"Create failed: {err.data.decode() if err.data else ''}"
print(f"Engine initialized, handle={hex(handle.value)}")
print(f"Test image: {w}x{h}")

# Warm-up run
res = NativeBuffer()
dll.lightocr_recognize_bgra(handle, pixels, w, h, w * 4, ctypes.byref(res), ctypes.byref(NativeBuffer()))
if res.data:
    dll.lightocr_free_buffer(res)

# Stability test: 100 iterations
N = 100
times = []
det_times = []
rec_times = []
line_counts = []
errors = []

print(f"\nRunning {N} consecutive OCR iterations...")
t0 = time.time()

for i in range(N):
    t_start = time.time()
    res = NativeBuffer()
    err2 = NativeBuffer()
    rc = dll.lightocr_recognize_bgra(handle, pixels, w, h, w * 4,
        ctypes.byref(res), ctypes.byref(err2))

    if rc == 0 and res.data and res.length > 0:
        raw = ctypes.string_at(res.data, res.length)
        text = raw.decode('utf-8', errors='replace')
        data = json.loads(text)
        elapsed = (time.time() - t_start) * 1000
        det_ms = data.get('timing', {}).get('detectionMs', 0)
        rec_ms = data.get('timing', {}).get('recognitionMs', 0)
        n_lines = len(data.get('lines', []))
        times.append(elapsed)
        det_times.append(det_ms)
        rec_times.append(rec_ms)
        line_counts.append(n_lines)
        dll.lightocr_free_buffer(res)
    else:
        err_msg = err2.data.decode('utf-8', errors='replace') if err2.data else 'unknown'
        errors.append((i, rc, err_msg))
        print(f"  Iteration {i}: FAILED (rc={rc}, err={err_msg[:100]})")

    if (i + 1) % 25 == 0:
        avg = sum(times[-25:]) / min(25, len(times))
        print(f"  {i+1}/{N}: avg={avg:.0f}ms, total_lines={sum(line_counts[-25:]):.0f}")

t_total = time.time() - t0

# Cleanup
dll.lightocr_destroy(handle, ctypes.byref(err))

# Results
print(f"\n=== Stability Test Results ({N} iterations) ===")
print(f"Total time: {t_total:.1f}s")
print(f"Average per iteration: {t_total/N*1000:.0f}ms")
print(f"Errors: {len(errors)}")

if times:
    times.sort()
    print(f"\nEnd-to-end latency (ms):")
    print(f"  P50:  {times[len(times)//2]:.0f}")
    print(f"  P95:  {times[int(len(times)*0.95)]:.0f}")
    print(f"  P99:  {times[int(len(times)*0.99)]:.0f}")
    print(f"  Min:  {min(times):.0f}")
    print(f"  Max:  {max(times):.0f}")

if det_times:
    det_times.sort()
    print(f"\nDetection latency (ms):")
    print(f"  P50: {det_times[len(det_times)//2]:.0f}")
    print(f"  P95: {det_times[int(len(det_times)*0.95)]:.0f}")
    print(f"  P99: {det_times[int(len(det_times)*0.99)]:.0f}")

if rec_times:
    rec_times.sort()
    print(f"\nRecognition latency (ms):")
    print(f"  P50: {rec_times[len(rec_times)//2]:.0f}")
    print(f"  P95: {rec_times[int(len(rec_times)*0.95)]:.0f}")

if line_counts:
    det_times.sort()
    print(f"\nDetected lines per image:")
    print(f"  Min: {min(line_counts)}, Max: {max(line_counts)}, Avg: {sum(line_counts)/len(line_counts):.0f}")

# DLL size
dll_size = os.path.getsize(r"J:\Javer_Workplace\dev\LightOCR\src\LightOCR.Native\build\Debug\LightOCR.Native.dll")
ort_size = os.path.getsize(r"J:\Javer_Workplace\dev\LightOCR\runtime\onnxruntime\onnxruntime-win-x64-1.21.0\lib\onnxruntime.dll")
print(f"\nBinary sizes:")
print(f"  LightOCR.Native.dll: {dll_size/1024:.0f} KB")
print(f"  onnxruntime.dll:     {ort_size/1024:.0f} KB")

# Package size estimate
models = 0
for f in ["det/inference.onnx", "rec/inference.onnx", "ppocrv6_dict.txt"]:
    path = os.path.join(models_dir, f)
    if os.path.exists(path):
        models += os.path.getsize(path)
print(f"  ONNX models total:   {models/1024:.0f} KB")

print(f"\n{'='*50}")
print(f"{'SUCCESS' if len(errors)==0 else f'{len(errors)} FAILURES'}")
print(f"{'='*50}")
