"""Try ALL available dictionaries systematically"""
import cv2, numpy as np, onnxruntime as ort, re, yaml

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
# Upscale 2x
img = cv2.resize(img, None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC)
h, w = img.shape[:2]

rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
rec_inp = rec.get_inputs()[0].name
rec_out = rec.get_outputs()[0].name
num_classes = rec.get_outputs()[0].shape[2]  # 18714 (variable, depends on dim param)

# Load ALL available dicts
dicts = {}

# 1. ONNX yml dict
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
dicts['ONNX yml'] = ''.join(onnx_chars)

# 2. Paddle model dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\rec\inference.yml', 'r', encoding='utf-8') as f:
    content = f.read()
match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content, re.DOTALL)
paddle_chars = []
for line in match.group(1).split('\n'):
    line = line.strip()
    if line.startswith('- '):
        ch = line[2:].strip("'\" \t")
        if ch:
            paddle_chars.append(ch)
dicts['Paddle yml'] = ''.join(paddle_chars)

# 3. Official medium model dict
with open(r'C:\Users\Administrator\.paddlex\official_models\PP-OCRv6_medium_rec_onnx\inference.yml', 'rb') as f:
    data = yaml.safe_load(f.read().decode('utf-8', errors='replace'))
dicts['Official medium'] = ''.join(data['PostProcess']['character_dict'])

# 4. ppocr_keys_v1
with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocr_keys_v1.txt', 'r', encoding='utf-8') as f:
    dicts['ppocr_keys_v1'] = f.read()

# 5. Third-party dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\rec_char_dict.txt', 'r', encoding='utf-8') as f:
    dicts['Third-party'] = f.read()

# Test each dict with the first line of the image
# Use a manual crop with proper padding
crop = img[34:84, 40:1920]  # y=34-84, x=40-1920 (upscaled coordinates)
ch, cw = crop.shape[:2]
th = 48
tw = max(16, int(cw * th / ch))
resized = cv2.resize(crop, (tw, th))
inp = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
out = rec.run([rec_out], {rec_inp: inp})[0][0]

print(f"Model output: {out.shape[0]} timesteps, {out.shape[1]} classes")
print(f"Crop: {cw}x{ch}\n")

for name, chars in dicts.items():
    blank_id = len(chars)
    prev = -1
    text = ''
    for t in range(out.shape[0]):
        mi = int(np.argmax(out[t]))
        if mi > 0 and mi != blank_id and mi < len(chars) and mi != prev:
            text += chars[mi]
        prev = mi if mi not in (0, blank_id) else -1
    
    # Check first expected char
    first = '每'
    first_idx = chars.find(first)
    print(f"{name:20s} ({len(chars):5d} chars): ", end='')
    if first_idx >= 0:
        # Check if this class ever appears in output
        appeared = any(int(np.argmax(out[t])) == first_idx for t in range(out.shape[0]))
        marker = '*' if appeared else ' '
        print(f"{marker} first='{first}'@{first_idx:5d} text='{text[:40]}'")
    else:
        print(f"  '每' NOT FOUND: text='{text[:40]}'")
