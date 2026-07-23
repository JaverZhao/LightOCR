"""Debug Chinese character recognition"""
import ctypes, json, os, sys

os.add_dll_directory(r'J:\Javer_Workplace\dev\LightOCR\src\LightOCR.Native\build\Debug')
os.add_dll_directory(r'J:\Javer_Workplace\dev\LightOCR\runtime\onnxruntime\onnxruntime-win-x64-1.21.0\lib')
os.add_dll_directory(r'J:\Javer_Workplace\dev\LightOCR\runtime\opencv\opencv\build\x64\vc16\bin')

dll = ctypes.CDLL(r'J:\Javer_Workplace\dev\LightOCR\src\LightOCR.Native\build\Debug\LightOCR.Native.dll')

class NativeBuffer(ctypes.Structure):
    _fields_ = [("data", ctypes.c_char_p), ("length", ctypes.c_size_t)]

dll.lightocr_get_api_version.restype = ctypes.c_int
dll.lightocr_create.restype = ctypes.c_int
dll.lightocr_create.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(NativeBuffer)]
dll.lightocr_recognize_bgra.restype = ctypes.c_int
dll.lightocr_recognize_bgra.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint8), ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(NativeBuffer), ctypes.POINTER(NativeBuffer)]
dll.lightocr_destroy.restype = ctypes.c_int
dll.lightocr_destroy.argtypes = [ctypes.c_void_p, ctypes.POINTER(NativeBuffer)]
dll.lightocr_free_buffer.argtypes = [NativeBuffer]

models_dir = r'J:\Javer_Workplace\dev\LightOCR\models\onnx'
test_img = r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png'

config = json.dumps({
    "modelDir": models_dir, "detModelOnnx": "det/inference.onnx",
    "recModelOnnx": "rec/inference.onnx", "dictPath": "ppocrv6_dict.txt",
    "cpuThreads": 4, "confidenceThreshold": 0.3
}).encode('utf-8')

handle = ctypes.c_void_p()
err = NativeBuffer()
rc = dll.lightocr_create(config, ctypes.byref(handle), ctypes.byref(err))
assert rc == 0, f"Create failed: {err.data.decode() if err.data else ''}"

import cv2
img = cv2.imread(test_img, cv2.IMREAD_COLOR)
h, w = img.shape[:2]
bgra = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
pixels = bgra.ctypes.data_as(ctypes.POINTER(ctypes.c_uint8))

res = NativeBuffer()
err2 = NativeBuffer()
rc = dll.lightocr_recognize_bgra(handle, pixels, w, h, w * 4, ctypes.byref(res), ctypes.byref(err2))

if rc == 0 and res.data and res.length > 0:
    raw = ctypes.string_at(res.data, res.length)
    text = raw.decode('utf-8', errors='replace')
    data = json.loads(text)
    lines = data.get('lines', [])
    det = data.get('timing', {}).get('detectionMs', 0)
    rec = data.get('timing', {}).get('recognitionMs', 0)
    print(f"Lines: {len(lines)} (det={det}ms, rec={rec}ms)")
    for line in lines:
        t = line['text']
        print(f"  [{line['order']}] conf={line['confidence']:.3f}: ", end='')
        # Show printable version
        try:
            printable = t.encode(sys.stdout.encoding, errors='replace').decode(sys.stdout.encoding)
            print(printable)
        except:
            print(f"[{len(t)} chars]")
    dll.lightocr_free_buffer(res)

dll.lightocr_destroy(handle, ctypes.byref(err))
