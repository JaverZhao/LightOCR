import cv2, numpy as np, onnxruntime as ort, re, sys

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h2, w2 = img.shape[:2]
img2 = cv2.resize(img, (w2*2, h2*2), interpolation=cv2.INTER_CUBIC)
crop = img2[40:76, 40:w2*2-40]
cv2.imwrite(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\crop_precise.png', crop)
print(f"Crop: {crop.shape[1]}x{crop.shape[0]}", flush=True)

rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
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

h, w = crop.shape[:2]
tw = max(16, int(w * 48 / h))
r2 = cv2.resize(crop, (tw, 48))
inp = (r2.astype(np.float32) / 255.0 - 0.5) / 0.5
inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
out = rec.run([rec.get_outputs()[0].name], {rec.get_inputs()[0].name: inp})[0][0]

bk = len(chars)
prev = -1
txt = ''
for t in range(out.shape[0]):
    mi = int(np.argmax(out[t]))
    if mi > 0 and mi != bk and mi < len(chars) and mi != prev:
        txt += chars[mi]
    prev = mi if mi not in (0, bk) else -1

print(f"Result ({len(txt)} chars): {txt[:60]}", flush=True)

# Check expected chars
expected = "每完成一个任务输出修改文件清单"
for i, c in enumerate(expected):
    idx = txt.find(c) if len(txt) > i else -1
    print(f"  Expected '{c}' (U+{ord(c):04X}): found at pos {idx}") 
    if idx >= 0:
        print(f"    Character before: '{txt[max(0,idx-1)]}' after: '{txt[min(len(txt)-1,idx+1)]}'")
