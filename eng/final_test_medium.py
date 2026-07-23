"""Test PP-OCRv6 medium model with exact PaddleOCR preprocessing"""
import cv2, numpy as np, onnxruntime as ort, re, math, yaml

# Load medium model and dict
rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\rec\inference.onnx')
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\rec\inference.yml', 'rb') as f:
    data = yaml.safe_load(f.read().decode('utf-8', errors='replace'))
chars = data['PostProcess']['character_dict']

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
det = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\det\inference.onnx')
det_inp = det.get_inputs()[0].name
det_out = det.get_outputs()[0].name

# Detection preprocessing (exact PaddleOCR style)
h, w = img.shape[:2]
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
scale = w / nw

# Post-processing: find text boxes with generous padding
binary = (prob > 0.2).astype(np.uint8)
num_labels, labels = cv2.connectedComponents(binary)

boxes = []
for i in range(1, num_labels):
    ys, xs = np.where(labels == i)
    if len(ys) < 5: continue
    avg = prob[ys, xs].mean()
    if avg < 0.4: continue
    x0 = int(xs.min() * scale)
    x1 = int(xs.max() * scale)
    y0 = int(ys.min() * scale)
    y1 = int(ys.max() * scale)
    pad_y = max(4, (y1 - y0) // 2)
    pad_x = max(4, (x1 - x0) // 40)
    y0 = max(0, y0 - pad_y)
    y1 = min(h, y1 + pad_y)
    x0 = max(0, x0 - pad_x)
    x1 = min(w, x1 + pad_x)
    if x1 - x0 < 10 or y1 - y0 < 5: continue
    boxes.append((x0, y0, x1, y1))

# Sort by reading order
boxes.sort(key=lambda b: (b[1], b[0]))

# Recognize each box
rec_inp_name = rec.get_inputs()[0].name
rec_out_name = rec.get_outputs()[0].name
blank_id = len(chars)
imgH, max_imgW = 48, 3200

for i, (x0, y0, x1, y1) in enumerate(boxes):
    crop = img[y0:y1, x0:x1]
    if crop.size == 0: continue
    ch, cw = crop.shape[:2]
    wh_ratio = cw / ch
    max_wh = max(320/48, wh_ratio)
    target_w = int(imgH * max_wh)
    if target_w > max_imgW: target_w = max_imgW
    ratio2 = cw / ch
    if math.ceil(imgH * ratio2) > target_w:
        resized_w = target_w
    else:
        resized_w = int(math.ceil(imgH * ratio2))
    resized = cv2.resize(crop, (min(resized_w, max_imgW), imgH))
    resized_w = resized.shape[1]
    inp2 = resized.astype(np.float32).transpose((2, 0, 1)) / 255
    inp2 -= 0.5; inp2 /= 0.5
    padding = np.zeros((3, imgH, target_w), dtype=np.float32)
    padding[:, :, 0:resized_w] = inp2
    inp2 = padding[None, :, :, :]
    
    out = rec.run([rec_out_name], {rec_inp_name: inp2})[0][0]
    prev = -1
    text = ''
    for t in range(out.shape[0]):
        mi = int(np.argmax(out[t]))
        if mi > 0 and mi != blank_id and mi < len(chars) and mi != prev:
            text += chars[mi]
        prev = mi if mi not in (0, blank_id) else -1
    
    # Count Chinese chars
    cn_count = sum(1 for c in text if ord(c) > 0x4E00)
    print(f"[{i}] ({x0},{y0})-({x1},{y1}) {cw}x{ch}: {text[:60]} ({cn_count} CJK chars)")

# Also try manual crop of each line with medium model
print("\n=== Manual crops (medium model) ===")
for label, y0, y1 in [("L1", 18, 39), ("L2", 53, 74), ("L3", 88, 109), ("L4", 123, 144)]:
    crop = img[y0:y1, 20:950]
    ch, cw = crop.shape[:2]
    wh_ratio = cw / ch
    max_wh = max(320/48, wh_ratio)
    target_w = int(imgH * max_wh)
    if target_w > max_imgW: target_w = max_imgW
    ratio2 = cw / ch
    if math.ceil(imgH * ratio2) > target_w: resized_w = target_w
    else: resized_w = int(math.ceil(imgH * ratio2))
    resized = cv2.resize(crop, (min(resized_w, max_imgW), imgH))
    resized_w = resized.shape[1]
    inp2 = resized.astype(np.float32).transpose((2, 0, 1)) / 255
    inp2 -= 0.5; inp2 /= 0.5
    padding = np.zeros((3, imgH, target_w), dtype=np.float32)
    padding[:, :, 0:resized_w] = inp2
    inp2 = padding[None, :, :, :]
    out = rec.run([rec_out_name], {rec_inp_name: inp2})[0][0]
    prev = -1; text = ''
    for t in range(out.shape[0]):
        mi = int(np.argmax(out[t]))
        if mi > 0 and mi != blank_id and mi < len(chars) and mi != prev:
            text += chars[mi]
        prev = mi if mi not in (0, blank_id) else -1
    found_mei = text.find('每')
    found_que = text.find('修')
    safe = ''.join(c if ord(c) < 128 else '?' for c in text[:40])
    print(f"  {label}: '{safe}' (每={found_mei}, 修={found_que})")
