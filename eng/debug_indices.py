"""Show raw model output class indices"""
import onnxruntime as ort
import numpy as np
import cv2, re

rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
rec_input = rec.get_inputs()[0].name
rec_output = rec.get_outputs()[0].name

# Load dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt', 'r', encoding='utf-8') as f:
    chars = f.read()

# Create a synthetic text image (white background, black text)
img = np.ones((60, 400, 3), dtype=np.uint8) * 255
cv2.putText(img, "BOARDING", (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 0), 2)

# Preprocess for recognition
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

inp = rec_preprocess(img)
out = rec.run([rec_output], {rec_input: inp})[0]
seq = out[0]

print(f"Model output: {seq.shape}")
print(f"Blank at index {seq.shape[1] - 1}")

# Show raw class indices per timestep
blank_id = seq.shape[1] - 1
print(f"\nTop-3 classes per timestep:")
for t in range(min(20, seq.shape[0])):
    top3 = np.argsort(seq[t])[-3:][::-1]
    top3_vals = seq[t, top3]
    print(f"  t={t}: ", end="")
    for idx, val in zip(top3, top3_vals):
        ch = chars[idx] if 0 <= idx < len(chars) else f'[{idx}]'
        if idx == blank_id:
            print(f"  [BLANK]({val:.3f})", end="")
        else:
            print(f"  '{ch}'(idx={idx},{val:.3f})", end="")
    print()

# CTC decode with blank at last position
prev = -1
text = ""
for t in range(seq.shape[0]):
    max_idx = int(np.argmax(seq[t]))
    if max_idx != blank_id and max_idx < len(chars) and max_idx != prev:
        text += chars[max_idx]
    prev = max_idx if max_idx != blank_id else -1

print(f"\nCTC decoded: '{text}'")
