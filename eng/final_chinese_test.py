"""Detailed Chinese character analysis using ONNX yml dict"""
import cv2, numpy as np, onnxruntime as ort, re

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
chars_str = ''.join(chars)

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
crop = img[17:42, 20:960]

rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
rec_inp = rec.get_inputs()[0].name
rec_out = rec.get_outputs()[0].name

h, w = crop.shape[:2]
ratio = 48 / h
tw = max(16, int(w * ratio))
resized = cv2.resize(crop, (tw, 48))
inp = (resized.astype(np.float32) / 255.0 - 0.5) / 0.5
inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
out = rec.run([rec_out], {rec_inp: inp})[0][0]

blank_id = out.shape[1] - 1
print(f"Output: {out.shape[0]} timesteps, {out.shape[1]} classes")
print(f"Blank: last class (index {blank_id})")

prev = -1
text = ''
indices = []
for t in range(out.shape[0]):
    mi = int(np.argmax(out[t]))
    if mi != 0 and mi != blank_id and mi < len(chars) and mi != prev:
        text += chars[mi]
        indices.append(mi)
    prev = mi if mi not in (0, blank_id) else -1

expected = "每完成一个任务输出修改文件清单关键设计决定构建命令及结果测试已知问题下一步"

print(f"\nDecoded ({len(text)} chars): {text[:50]}")
for i, (idx, c) in enumerate(zip(indices[:len(expected)], text[:len(expected)])):
    exp = expected[i]
    ok = "OK" if c == exp else f"EXP {exp}"
    print(f"  [{i:2d}] class={idx:5d} got='{c}' {ok}")

# Show model's top prediction for each expected char
print(f"\nModel output analysis for expected characters:")
for i, exp in enumerate(expected[:15]):
    target_idx = chars_str.find(exp)
    if target_idx >= 0:
        # Check if this class is ever the top-1 at any timestep
        found = False
        for t in range(out.shape[0]):
            mi = int(np.argmax(out[t]))
            if mi == target_idx:
                val = float(out[t, mi])
                print(f"  '{exp}' (class {target_idx}): found at t={t}, logit={val:.3f}")
                found = True
                break
        if not found:
            # Check top-5
            for t in range(out.shape[0]):
                top5 = np.argsort(out[t])[-5:]
                if target_idx in top5:
                    rank = 5 - list(top5).index(target_idx)
                    val = float(out[t, target_idx])
                    print(f"  '{exp}' (class {target_idx}): NOT top-1, best at t={t} rank={rank} val={val:.3f}")
                    break
            else:
                print(f"  '{exp}' (class {target_idx}): NOT in ANY timestep's top-5!")
    else:
        print(f"  '{exp}': NOT FOUND IN DICT!")
