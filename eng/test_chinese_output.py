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

config = json.dumps({"modelDir": r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium', "detModelOnnx": "det/inference.onnx", "recModelOnnx": "rec/inference.onnx", "dictPath": "ppocrv6_dict.txt", "cpuThreads": 4, "confidenceThreshold": 0.3, "detLimitSideLen": 960, "detThreshold": 0.2, "detBoxThreshold": 0.45, "detUnclipRatio": 1.4, "detMaxCandidates": 3000}).encode('utf-8')
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

    expected_lines = [
        "一个意大利美女坐在清吧的酒吧里，她面容精致，深色长发微卷，眼神沉静而若有所思。她穿着一件剪裁合",
        "体的上衣，姿态放松地坐在吧台前的高脚凳上，一只手轻轻搭在台面上。吧台表面有隐约的木质纹理，背景",
        "是酒柜上排列整齐的酒瓶和暖黄色调的室内灯光，光线柔和地勾勒出她的侧脸轮廓。清吧内环境安静，有几",
        "张空置的小圆桌和吧台椅，整体氛围慵懒而略带朦胧，画面采用中景构图，焦点集中在人物与吧台区域的互",
        "动，保持自然真实的生活感。",
    ]
    actual_lines = [line["text"] for line in data.get("lines", [])]
    if actual_lines != expected_lines:
        raise AssertionError(
            "PP-OCRv6 Chinese accuracy regression:\n"
            f"expected={expected_lines!r}\nactual={actual_lines!r}"
        )
    
    for line in data.get('lines', []):
        print(f"[{line['order']}] conf={line['confidence']:.3f}: {line['text']}")
    print(f"\nFull text:\n{data.get('fullText', '')}")
    dll.lightocr_free_buffer(res)

dll.lightocr_destroy(h, ctypes.byref(Buf()))
