"""Try PP-OCR keys dict for Chinese recognition"""
import cv2, numpy as np, onnxruntime as ort

# Try the official ppocr_keys_v1.txt
with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocr_keys_v1.txt', 'r', encoding='utf-8') as f:
    chars = f.read()
print(f"Dict: {len(chars)} chars")

# Image: manually crop line 1
img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
crop = img[17:42, 20:960]

rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
rec_inp = rec.get_inputs()[0].name
rec_out = rec.get_outputs()[0].name

def preprocess(crop):
    h, w = crop.shape[:2]
    target_h = 48
    ratio = target_h / h
    target_w = max(16, int(w * ratio))
    resized = cv2.resize(crop, (target_w, target_h), interpolation=cv2.INTER_LINEAR)
    inp = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
    inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
    return inp

inp = preprocess(crop)
out = rec.run([rec_out], {rec_inp: inp})[0][0]

blank_id = out.shape[1] - 1
print(f"Output shape: {out.shape}")

# Try with different blank positions
for blank_pos, desc in [(0, "blank first"), (out.shape[1]-1, "blank last")]:
    prev = -1
    text = ""
    for t in range(out.shape[0]):
        max_idx = int(np.argmax(out[t]))
        if blank_pos == 0:
            if max_idx > 0 and max_idx <= len(chars) and max_idx != prev:
                text += chars[max_idx - 1]
            prev = max_idx if max_idx != 0 else -1
        else:
            if max_idx != blank_pos and max_idx < len(chars) and max_idx != prev and max_idx > 0:
                text += chars[max_idx]
            prev = max_idx if max_idx not in (0, blank_pos) else -1
    print(f"\n{desc}:")
    print(f"  First 50 chars: {text[:50]}")

# Also try with the ONNX yml dict
import re
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.yml', 'r', encoding='utf-8') as f:
    content = f.read()
match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content, re.DOTALL)
onnx_chars = []
for line in match.group(1).split('\n'):
    line = line.strip()
    if line.startswith('- '):
        ch = line[2:].strip("'\" \t")
        if ch:
            onnx_chars.append(ch)
onnx_str = ''.join(onnx_chars)

prev = -1
text = ""
for t in range(out.shape[0]):
    max_idx = int(np.argmax(out[t]))
    if max_idx != blank_id and max_idx < len(onnx_str) and max_idx != prev and max_idx > 0:
        text += onnx_str[max_idx]
    prev = max_idx if max_idx not in (0, blank_id) else -1
print(f"\nONNX yml dict:")
print(f"  First 50 chars: {text[:50]}")
