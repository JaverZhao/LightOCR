"""Test Chinese recognition and save output for inspection"""
import ctypes, json, os

os.add_dll_directory(r'J:\Javer_Workplace\dev\LightOCR\src\LightOCR.Native\build\Debug')
os.add_dll_directory(r'J:\Javer_Workplace\dev\LightOCR\runtime\onnxruntime\onnxruntime-win-x64-1.21.0\lib')
dll = ctypes.CDLL(r'J:\Javer_Workplace\dev\LightOCR\src\LightOCR.Native\build\Debug\LightOCR.Native.dll')

class Buf(ctypes.Structure):
    _fields_ = [("data", ctypes.c_char_p), ("length", ctypes.c_size_t)]

dll.lightocr_get_api_version.restype = ctypes.c_int
dll.lightocr_create.restype = ctypes.c_int
dll.lightocr_create.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(Buf)]
dll.lightocr_recognize_bgra.restype = ctypes.c_int
dll.lightocr_recognize_bgra.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint8), ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(Buf), ctypes.POINTER(Buf)]
dll.lightocr_destroy.restype = ctypes.c_int

config = json.dumps({"modelDir": r'J:\Javer_Workplace\dev\LightOCR\models\onnx', "detModelOnnx": "det/inference.onnx", "recModelOnnx": "rec/inference.onnx", "dictPath": "ppocrv6_dict.txt", "cpuThreads": 4, "confidenceThreshold": 0.3}).encode('utf-8')
h = ctypes.c_void_p()
dll.lightocr_create(config, ctypes.byref(h), ctypes.byref(Buf()))

import cv2
img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h2, w = img.shape[:2]
bgra = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
px = bgra.ctypes.data_as(ctypes.POINTER(ctypes.c_uint8))
res = Buf()
dll.lightocr_recognize_bgra(h, px, w, h2, w*4, ctypes.byref(res), ctypes.byref(Buf()))

if res.data and res.length > 0:
    raw = ctypes.string_at(res.data, res.length)
    # Save raw result
    with open(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\result.json', 'wb') as f:
        f.write(raw)
    data = json.loads(raw.decode('utf-8', errors='replace'))
    with open(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\result_pretty.json', 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    
    for line in data.get('lines', []):
        print(f"[{line['order']}] conf={line['confidence']:.3f}: {line['text']}")
    print(f"\nFull text:\n{data.get('fullText', '')}")
    dll.lightocr_free_buffer(res)

dll.lightocr_destroy(h, ctypes.byref(Buf()))
