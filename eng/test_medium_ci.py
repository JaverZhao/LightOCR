"""Test medium model + upscaled Chinese image"""
import cv2, numpy as np, onnxruntime as ort

# Load models
det = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\det\inference.onnx')
rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\rec\inference.onnx')

# Load dict
import yaml
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\rec\inference.yml', 'rb') as f:
    data = yaml.safe_load(f.read().decode('utf-8', errors='replace'))
chars = data['PostProcess']['character_dict']

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h, w = img.shape[:2]

# Upscale 2x for better detection
img = cv2.resize(img, (w*2, h*2), interpolation=cv2.INTER_CUBIC)
h, w = img.shape[:2]
print(f"Upscaled: {w}x{h}")

# Detect
det_inp = det.get_inputs()[0].name
det_out = det.get_outputs()[0].name

limit = 960
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

prob = det.run([det_out], {det_inp: inp})[0][0, 0]

# Postprocess
scale = w / nw
binary = (prob > 0.2).astype(np.uint8)
num_labels, labels = cv2.connectedComponents(binary)

boxes = []
for i in range(1, num_labels):
    ys, xs = np.where(labels == i)
    if len(ys) < 3:
        continue
    avg = prob[ys, xs].mean()
    if avg < 0.45:
        continue
    x0, x1 = int(xs.min() * scale), int(xs.max() * scale)
    y0, y1 = int(ys.min() * scale), int(ys.max() * scale)
    pad = (y1 - y0) // 2
    y0 = max(0, y0 - pad)
    y1 = min(h, y1 + pad)
    x0, x1 = max(0, x0), min(w, x1)
    if x1 - x0 < 5 or y1 - y0 < 5:
        continue
    boxes.append((x0, y0, x1, y1))

boxes.sort(key=lambda b: (b[1], b[0]))  # sort by Y then X
print(f"Detected {len(boxes)} text regions")

# Recognize each
rec_inp = rec.get_inputs()[0].name
rec_out = rec.get_outputs()[0].name
blank_id = 18709  # medium model: num_classes - 1

for i, (x0, y0, x1, y1) in enumerate(boxes[:7]):
    crop = img[y0:y1, x0:x1]
    if crop.size == 0: continue
    ch, cw = crop.shape[:2]
    th = 48
    tw = max(16, int(cw * th / ch))
    resized = cv2.resize(crop, (tw, th), interpolation=cv2.INTER_LINEAR)
    inp2 = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
    inp2 = np.transpose(inp2, (2, 0, 1))[None, :, :, :]
    out = rec.run([rec_out], {rec_inp: inp2})[0][0]
    
    prev = -1
    text = ''
    for t in range(out.shape[0]):
        mi = int(np.argmax(out[t]))
        if mi != 0 and mi != blank_id and mi < len(chars) and mi != prev:
            text += chars[mi]
        prev = mi if mi not in (0, blank_id) else -1
    
    try:
        print(f"[{i}] ({x0},{y0})-{cw}x{ch}: {text[:100]}")
    except:
        print(f"[{i}] ({x0},{y0})-{cw}x{ch}: [{len(text)} chars]")
