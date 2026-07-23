"""Verify PP-OCRv6 ONNX model recognition with real image"""
import onnxruntime as ort
import numpy as np
import cv2

# Load models
det = ort.InferenceSession(r"J:\Javer_Workplace\dev\LightOCR\models\onnx\det\inference.onnx")
rec = ort.InferenceSession(r"J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx")

# Load dict
with open(r"J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt", "r", encoding="utf-8") as f:
    chars = f.read()
print(f"Dictionary: {len(chars)} chars")
print(f"First 20: {repr(chars[:20])}")

# Load test image
img = cv2.imread(r"J:\Javer_Workplace\dev\LightOCR\tests\TestAssets\test_ocr.png")
h, w = img.shape[:2]
print(f"Image: {w}x{h}")

# Run detection
input_name = det.get_inputs()[0].name
output_name = det.get_outputs()[0].name

# Preprocess
def det_preprocess(img, limit=960):
    h, w = img.shape[:2]
    ratio = 1.0
    if max(w, h) > limit:
        ratio = limit / max(w, h)
        new_w = int(w * ratio / 32) * 32
        new_h = int(h * ratio / 32) * 32
    else:
        new_w = (w // 32) * 32
        new_h = (h // 32) * 32
    new_w = max(32, new_w)
    new_h = max(32, new_h)
    
    resized = cv2.resize(img, (new_w, new_h), interpolation=cv2.INTER_LINEAR)
    mean = np.array([0.485, 0.456, 0.406], dtype=np.float32)
    std = np.array([0.229, 0.224, 0.225], dtype=np.float32)
    inp = (resized.astype(np.float32) / 255.0 - mean) / std
    inp = np.transpose(inp, (2, 0, 1))  # HWC -> CHW
    inp = np.expand_dims(inp, 0)  # add batch
    scale = w / new_w
    return inp, scale, new_w, new_h

inp, scale, nw, nh = det_preprocess(img)
prob_map = det.run([output_name], {input_name: inp})[0]
print(f"Detection output shape: {prob_map.shape}")

# Simple postprocessing
prob = prob_map[0, 0]  # [H, W]
thresh = 0.2
binary = (prob > thresh).astype(np.uint8) * 255

# Find connected components
import cv2
num_labels, labels = cv2.connectedComponents(binary)

boxes = []
for i in range(1, num_labels):
    ys, xs = np.where(labels == i)
    if len(ys) < 3:
        continue
    avg_prob = prob[ys, xs].mean()
    if avg_prob < 0.45:
        continue
    
    x0, x1 = xs.min(), xs.max()
    y0, y1 = ys.min(), ys.max()
    
    # Scale back
    ox0 = int(x0 * scale)
    ox1 = int(x1 * scale)
    oy0 = int(y0 * scale)
    oy1 = int(y1 * scale)
    ox0 = max(0, ox0); ox1 = min(w, ox1)
    oy0 = max(0, oy0); oy1 = min(h, oy1)
    
    if ox1 - ox0 < 5 or oy1 - oy0 < 5:
        continue
    boxes.append((ox0, oy0, ox1, oy1))

print(f"Detected {len(boxes)} text regions")

# Recognize each box
def rec_preprocess(crop):
    h, w = crop.shape[:2]
    target_h = 48
    ratio = target_h / h
    target_w = max(16, int(w * ratio))
    resized = cv2.resize(crop, (target_w, target_h), interpolation=cv2.INTER_LINEAR)
    inp = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
    inp = np.transpose(inp, (2, 0, 1))
    inp = np.expand_dims(inp, 0)
    return inp

rec_input = rec.get_inputs()[0].name
rec_output = rec.get_outputs()[0].name

print(f"\n=== Recognition Results ===")
for i, (x0, y0, x1, y1) in enumerate(boxes[:10]):
    crop = img[y0:y1, x0:x1]
    if crop.size == 0:
        continue
    
    inp = rec_preprocess(crop)
    out = rec.run([rec_output], {rec_input: inp})[0]
    
    # CTC decode - blank is LAST class (index = num_classes - 1)
    seq = out[0]  # [seq_len, num_classes]
    blank_id = seq.shape[1] - 1
    prev = -1
    text1 = ""  # blank first (idx 0)
    text2 = ""  # blank last (idx N-1)
    for t in range(seq.shape[0]):
        max_idx = int(np.argmax(seq[t]))
        max_val = float(seq[t, max_idx])
        # Version 1: blank first
        if max_idx > 0 and max_idx <= len(chars) and max_idx != prev:
            text1 += chars[max_idx - 1]
        # Version 2: blank last
        if max_idx < len(chars) and max_idx != prev:
            text2 += chars[max_idx]
        prev_cached = max_idx
        
    # Try both and show the one with readable ASCII content
    safe1 = text1.encode('ascii', 'replace').decode()
    safe2 = text2.encode('ascii', 'replace').decode()
    print(f"  [{i}] v1(blank0): {safe1[:60]}")
    print(f"       v2(blankN): {safe2[:60]}")
    if text2 and any(ord(c) > 127 for c in text2):
        print(f"       actual:    {text2[:60]}")
    
    avg_conf = np.mean(confs) if confs else 0
    safe_text = text.encode('ascii', 'replace').decode()
    print(f"  [{i}] ({x0},{y0})-({x1},{y1}) conf={avg_conf:.3f}: {safe_text[:60]}")
    if text and any(ord(c) > 127 for c in text):
        print(f"       Actual: {text[:60]}")
