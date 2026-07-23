"""Debug Chinese character recognition - check dict content"""
import onnxruntime as ort
import numpy as np
import cv2, os

# Load our dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt', 'r', encoding='utf-8') as f:
    chars = f.read()
print(f"Dictionary: {len(chars)} chars")

# Check if common Chinese chars are present
test_chars = '每完成一个任务输出修改文件清单关键设计决定构建命令及结果测试已知问题下一步'
for c in test_chars:
    idx = chars.find(c)
    print(f"  '{c}': index {idx}" + (" (OK)" if idx >= 0 else " (NOT FOUND)"))

# Load the test image and run recognition with ONNX Runtime directly
rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
rec_input = rec.get_inputs()[0].name
rec_output = rec.get_outputs()[0].name

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h, w = img.shape[:2]
print(f"\nImage: {w}x{h}")

# Run detection
det = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\det\inference.onnx')
det_inp = det.get_inputs()[0].name
det_out = det.get_outputs()[0].name

# Preprocess for det
def det_preprocess(img, limit=960):
    h, w = img.shape[:2]
    ratio = 1.0
    if max(w, h) > limit:
        ratio = limit / max(w, h)
    nw = max(32, int(w * ratio / 32) * 32)
    nh = max(32, int(h * ratio / 32) * 32)
    resized = cv2.resize(img, (nw, nh))
    mean = np.array([0.485, 0.456, 0.406], dtype=np.float32)
    std = np.array([0.229, 0.224, 0.225], dtype=np.float32)
    inp = (resized.astype(np.float32) / 255.0 - mean) / std
    inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
    scale = w / nw
    return inp, scale, nw, nh

inp, scale, nw, nh = det_preprocess(img)
prob = det.run([det_out], {det_inp: inp})[0][0, 0]

# Simple postprocess - find non-zero regions
binary = (prob > 0.2).astype(np.uint8)
num_labels, labels = cv2.connectedComponents(binary)

boxes = []
for i in range(1, num_labels):
    ys, xs = np.where(labels == i)
    if len(ys) < 3:
        continue
    avg_prob = prob[ys, xs].mean()
    if avg_prob < 0.45:
        continue
    x0, x1 = int(xs.min() * scale), int(xs.max() * scale)
    y0, y1 = int(ys.min() * scale), int(ys.max() * scale)
    x0, x1 = max(0, x0), min(w, x1)
    y0, y1 = max(0, y0), min(h, y1)
    if x1 - x0 < 5 or y1 - y0 < 5:
        continue
    boxes.append((x0, y0, x1, y1))

print(f"Detected {len(boxes)} text regions")

# Recognize each box
blank_id = 18713  # Last class

def rec_preprocess(crop):
    h, w = crop.shape[:2]
    target_h = 48
    ratio = target_h / h
    target_w = max(16, int(w * ratio))
    resized = cv2.resize(crop, (target_w, target_h), interpolation=cv2.INTER_LINEAR)
    inp = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
    inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
    return inp

print(f"\nTop-5 results:")
for i, (x0, y0, x1, y1) in enumerate(boxes[:5]):
    crop = img[y0:y1, x0:x1]
    if crop.size == 0:
        continue
    
    inp = rec_preprocess(crop)
    out = rec.run([rec_output], {rec_input: inp})[0][0]
    
    # CTC decode (blank last, filter class 0)
    prev = -1
    text = ""
    for t in range(out.shape[0]):
        max_idx = int(np.argmax(out[t]))
        if max_idx > 0 and max_idx != blank_id and max_idx < len(chars) and max_idx != prev:
            text += chars[max_idx]
        prev = max_idx if max_idx not in (0, blank_id) else -1
    
    print(f"  [{i}] ({x0},{y0})-({x1},{y1}): ", end="")
    try:
        print(text[:80])
    except:
        print(f"[{len(text)} chars]")
    
    # Show raw class indices
    if i == 0:  # Show detail for first box
        print(f"       Raw indices per timestep:")
        for t in range(min(out.shape[0], 50)):
            max_idx = int(np.argmax(out[t]))
            ch = chars[max_idx] if 0 <= max_idx < len(chars) else f'[{max_idx}]'
            is_kept = max_idx > 0 and max_idx != blank_id and max_idx < len(chars)
            marker = 'K' if is_kept else ' '
            print(f"       t={t:2d}: idx={max_idx:5d} ch={ch} {marker}")
