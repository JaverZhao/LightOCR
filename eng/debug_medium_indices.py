import cv2, numpy as np, onnxruntime as ort, math, re, yaml

rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\rec\inference.onnx')
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx_medium\rec\inference.yml', 'rb') as f:
    chars = yaml.safe_load(f.read().decode('utf-8'))['PostProcess']['character_dict']

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
crop = img[18:39, 20:950]
h, w = crop.shape[:2]
tw = max(16, int(w * 48 / h))
r = cv2.resize(crop, (tw, 48))
inp = (r.astype(np.float32) / 255.0 - 0.5) / 0.5
inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
out = rec.run([rec.get_outputs()[0].name], {rec.get_inputs()[0].name: inp})[0][0]

bk = len(chars)
print(f"Output: {out.shape[0]} timesteps, {out.shape[1]} classes, blank at {bk}")

# Top classes per timestep (first 20)
prev = -1
text = ''
indices = []
for t in range(out.shape[0]):
    mi = int(np.argmax(out[t]))
    if mi > 0 and mi != bk and mi < len(chars) and mi != prev:
        text += chars[mi]
        indices.append(mi)
    prev = mi if mi not in (0, bk) else -1

print(f"Decoded {len(text)} chars")
print(f"First 20 indices: {indices[:20]}")

# Check medium vs small dict at key positions
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.yml', 'r', encoding='utf-8') as f:
    content = f.read()
match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content, re.DOTALL)
small_chars = []
for l in match.group(1).split('\n'):
    l = l.strip()
    if l.startswith('- '):
        ch = l[2:].strip("'\" \t")
        if ch:
            small_chars.append(ch)

print(f"\nSmall dict: {len(small_chars)} chars, Medium dict: {len(chars)} chars")
print(f"Small[2093]: {small_chars[2093] if 2093 < len(small_chars) else 'N/A'}")
print(f"Medium[2093]: {chars[2093] if 2093 < len(chars) else 'N/A'}")

# Check if 每 is at different index
for c in ['每', '完', '成', '修', '改']:
    si = small_chars.index(c) if c in small_chars else -1
    mi = chars.index(c) if c in chars else -1
    print(f"  '{c}': small={si}, medium={mi}, same={si == mi}")
