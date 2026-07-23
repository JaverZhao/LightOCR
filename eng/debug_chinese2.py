"""Deep debug of Chinese recognition - check raw model output"""
import cv2, numpy as np, onnxruntime as ort

# Load dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt', 'r', encoding='utf-8') as f:
    chars = f.read()

# Load image and extract a line of text manually
img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h, w = img.shape[:2]

# Manually crop a line (use edges to find text bounds)
# Line 1 is at approximately y=19 to y=38 (from edge histogram)
crop = img[17:40, 20:960]  # Add padding
print(f"Manual crop: {crop.shape[1]}x{crop.shape[0]}")

# Save to see
cv2.imwrite(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\manual_crop.png', crop)

# Run recognition
rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
rec_inp = rec.get_inputs()[0].name
rec_out = rec.get_outputs()[0].name

def rec_preprocess(crop):
    h, w = crop.shape[:2]
    target_h = 48
    ratio = target_h / h
    target_w = max(16, int(w * ratio))
    resized = cv2.resize(crop, (target_w, target_h), interpolation=cv2.INTER_LINEAR)
    inp = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
    inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
    return inp

inp = rec_preprocess(crop)
out = rec.run([rec_out], {rec_inp: inp})[0][0]

print(f"Model output shape: {out.shape}")
blank_id = out.shape[1] - 1

# CTC decode
prev = -1
text = ""
for t in range(out.shape[0]):
    max_idx = int(np.argmax(out[t]))
    max_val = float(out[t, max_idx])
    if max_idx > 0 and max_idx != blank_id and max_idx < len(chars) and max_idx != prev:
        text += chars[max_idx]
    prev = max_idx if max_idx not in (0, blank_id) else -1

print(f"\nCTC result ({len(text)} chars):")
for i, c in enumerate(text):
    try:
        print(f"  [{i}] U+{ord(c):04X} '{c}' (index {chars.find(c)})")
    except:
        print(f"  [{i}] U+{ord(c):04X} (can't print)")

# Show top-3 at each timestep for first 30 frames
print(f"\nTop-3 per timestep (first 30):")
for t in range(min(30, out.shape[0])):
    top3 = np.argsort(out[t])[-3:][::-1]
    top3_vals = out[t, top3]
    items = []
    for idx, val in zip(top3, top3_vals):
        if idx == 0:
            items.append("NULL")
        elif idx == blank_id:
            items.append("BLANK")
        elif idx < len(chars):
            c = chars[idx].encode('ascii', 'replace').decode()
            items.append(f"{c}({idx})")
        else:
            items.append(f"[{idx}]")
    print(f"  t={t:2d}: {' '.join(items)}")
