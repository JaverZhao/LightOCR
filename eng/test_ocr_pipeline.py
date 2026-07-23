"""Verify the LightOCR native DLL with a real image using ctypes"""
import ctypes
import json
import os
import sys

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

print(f"API version: {dll.lightocr_get_api_version()}")

config = json.dumps({
    "modelDir": models_dir,
    "detModelOnnx": "det/inference.onnx",
    "recModelOnnx": "rec/inference.onnx",
    "dictPath": "ppocrv6_dict.txt",
    "cpuThreads": 4,
    "confidenceThreshold": 0.3
}).encode('utf-8')

handle = ctypes.c_void_p()
err = NativeBuffer()
rc = dll.lightocr_create(config, ctypes.byref(handle), ctypes.byref(err))
if rc != 0:
    print(f"Create failed ({rc}): {err.data.decode('utf-8', errors='replace') if err.data else 'unknown'}")
    sys.exit(1)
print(f"Engine created, handle={hex(handle.value)}")

import cv2
img = cv2.imread(test_img, cv2.IMREAD_COLOR)
h, w = img.shape[:2]
bgra = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
pixels = bgra.ctypes.data_as(ctypes.POINTER(ctypes.c_uint8))

res = NativeBuffer()
err2 = NativeBuffer()
rc = dll.lightocr_recognize_bgra(handle, pixels, w, h, w * 4, ctypes.byref(res), ctypes.byref(err2))

print(f"Result buffer: data={res.data}, length={res.length}, rc={rc}")
if rc == 0 and res.data and res.length > 0:
    raw = ctypes.string_at(res.data, res.length)
    # Save result to file
    with open(r"J:\Javer_Workplace\dev\LightOCR\tests\TestAssets\result.json", "wb") as f:
        f.write(raw)
    print(f"Output saved ({len(raw)} bytes)")
    # Decode with error handling
    text = raw.decode('utf-8', errors='replace')
    result = json.loads(text)
    lines = result.get('lines', [])
    det = result.get('timing', {}).get('detectionMs', 0)
    rec = result.get('timing', {}).get('recognitionMs', 0)
    print(f"\n=== OCR Result: {len(lines)} lines (det={det}ms, rec={rec}ms) ===")
    for line in lines[:5]:
        t = line['text'].encode('ascii', 'replace').decode()
        print(f"  [{line['order']}] \"{t}\" (conf={line['confidence']:.3f})")
    if len(lines) > 5:
        print(f"  ... and {len(lines)-5} more lines")
    if lines:
        ft = result['fullText'].encode('ascii', 'replace').decode()
        print(f"Full text ({len(ft)} chars): {ft[:200]}...")
    dll.lightocr_free_buffer(res)
else:
    err_msg = err2.data.decode('utf-8', errors='replace') if err2.data else 'unknown'
    print(f"OCR failed ({rc}): {err_msg}")

dll.lightocr_destroy(handle, ctypes.byref(err))
print("\nDone")
