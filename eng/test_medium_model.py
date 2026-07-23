"""Test the official PP-OCRv6 medium ONNX model with Chinese"""
import cv2, numpy as np, onnxruntime as ort

# Load official medium rec model
rec = ort.InferenceSession(r'C:\Users\Administrator\.paddlex\official_models\PP-OCRv6_medium_rec_onnx\inference.onnx')
rec_inp = rec.get_inputs()[0].name
rec_out = rec.get_outputs()[0].name

print(f"Medium rec model inputs: {[(i.name, i.shape) for i in rec.get_inputs()]}")
print(f"Medium rec model outputs: {[(o.name, o.shape) for o in rec.get_outputs()]}")

# Load dict from official model's yml
import yaml
with open(r'C:\Users\Administrator\.paddlex\official_models\PP-OCRv6_medium_rec_onnx\inference.yml', 'r', encoding='utf-8') as f:
    config = yaml.safe_load(f)
chars = config['PostProcess']['character_dict']
print(f"Dict: {len(chars)} chars")
print(f"每 at index {chars.index('每')}")

# Load Chinese test image
img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
crop = img[17:42, 20:960]

# Preprocess
h, w = crop.shape[:2]
target_h = 48
ratio = target_h / h
target_w = max(16, int(w * ratio))
resized = cv2.resize(crop, (target_w, target_h), interpolation=cv2.INTER_LINEAR)
inp = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]

# Run
out = rec.run([rec_out], {rec_inp: inp})[0][0]
print(f"\nOutput shape: {out.shape}")

# CTC decode
blank_id = out.shape[1] - 1
prev = -1
text = ''
for t in range(out.shape[0]):
    mi = int(np.argmax(out[t]))
    if mi > 0 and mi != blank_id and mi < len(chars) and mi != prev:
        text += chars[mi]
    prev = mi if mi not in (0, blank_id) else -1

print(f"\nMedium model result ({len(text)} chars):")
print(text[:100])
