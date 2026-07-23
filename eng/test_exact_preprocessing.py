"""Use EXACT same preprocessing as PaddleOCR's RecResizeImg"""
import cv2, numpy as np, onnxruntime as ort, re, math

# Load dict from ONNX yml
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.yml', 'r', encoding='utf-8') as f:
    content = f.read()
match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content, re.DOTALL)
chars = []
for line in match.group(1).split('\n'):
    line = line.strip()
    if line.startswith('- '):
        ch = line[2:].strip("'\" \t")
        if ch:
            chars.append(ch)

rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
rec_inp = rec.get_inputs()[0].name
rec_out = rec.get_outputs()[0].name

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h, w = img.shape[:2]

# Up 2x
img2 = cv2.resize(img, (w*2, h*2), interpolation=cv2.INTER_CUBIC)

# Crop first line: original y=20-37 -> upscaled y=40-74
crop = img2[40:76, 40:w*2-40]
print(f"Crop: {crop.shape[1]}x{crop.shape[0]}")

# ===== EXACT PaddleOCR RecResizeImg preprocessing =====
imgC, imgH, imgW = 3, 48, 320  # rec_image_shape
max_imgW = 3200

h, w = crop.shape[:2]
wh_ratio = w / h
max_wh_ratio = max(imgW / imgH, wh_ratio)

# Step 1: calculate target width
target_w = int(imgH * max_wh_ratio)
if target_w > max_imgW:
    resized_image = cv2.resize(crop, (max_imgW, imgH))
    resized_w = max_imgW
    target_w = max_imgW
else:
    ratio = w / h
    if math.ceil(imgH * ratio) > target_w:
        resized_w = target_w
    else:
        resized_w = int(math.ceil(imgH * ratio))
    resized_image = cv2.resize(crop, (resized_w, imgH))

print(f"  target_w={target_w}, resized_w={resized_w}")

# Step 2: normalize
resized_image = resized_image.astype(np.float32)
resized_image = resized_image.transpose((2, 0, 1)) / 255.0
resized_image -= 0.5
resized_image /= 0.5

# Step 3: right padding
padding_im = np.zeros((imgC, imgH, target_w), dtype=np.float32)
padding_im[:, :, 0:resized_w] = resized_image

# Step 4: add batch dim
inp = padding_im[None, :, :, :]

# Run model
out = rec.run([rec_out], {rec_inp: inp})[0][0]
print(f"Model output: {out.shape[0]} timesteps, {out.shape[1]} classes")

# CTC decode (PaddleOCR style: blank = len(chars))
blank_id = len(chars)
prev = -1
text = ''
for t in range(out.shape[0]):
    mi = int(np.argmax(out[t]))
    if mi > 0 and mi != blank_id and mi < len(chars) and mi != prev:
        text += chars[mi]
    prev = mi if mi not in (0, blank_id) else -1

print(f"\nResult ({len(text)} chars): {text[:60]}")
for i in range(min(20, len(text))):
    c = text[i]
    print(f"  [{i}] U+{ord(c):04X} '{c}'")
