"""Test with very precise cropping to avoid artifacts"""
import cv2, numpy as np, onnxruntime as ort, re, math

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

# Line 1: from edge histogram, text is at y=20-37
# Take a tighter crop with less margin
for label, y0, y1, x_margin in [
    ("Line1 tight", 20, 37, 0),
    ("Line1 wide", 18, 39, -20),
    ("Line1 extra", 19, 38, -10),
]:
    crop = img[max(0,y0):min(img.shape[0],y1), max(0,20-x_margin):min(img.shape[1],960+x_margin)]
    h, w = crop.shape[:2]
    if h < 5 or w < 5:
        continue
    
    # PaddleOCR preprocessing
    imgH = 48
    max_imgW = 3200
    wh_ratio = w / h
    max_wh_ratio = max(320/48, wh_ratio)
    target_w = int(imgH * max_wh_ratio)
    
    if target_w > max_imgW:
        resized = cv2.resize(crop, (max_imgW, imgH))
        resized_w = max_imgW
        target_w = max_imgW
    else:
        ratio = w / h
        if math.ceil(imgH * ratio) > target_w:
            resized_w = target_w
        else:
            resized_w = int(math.ceil(imgH * ratio))
        resized = cv2.resize(crop, (resized_w, imgH))
    
    inp = resized.astype(np.float32).transpose((2, 0, 1)) / 255
    inp -= 0.5
    inp /= 0.5
    padding = np.zeros((3, imgH, target_w), dtype=np.float32)
    padding[:, :, 0:resized_w] = inp
    inp = padding[None, :, :, :]
    
    out = rec.run([rec_out], {rec_inp: inp})[0][0]
    bk = len(chars)
    prev = -1
    text = ''
    for t in range(out.shape[0]):
        mi = int(np.argmax(out[t]))
        if mi > 0 and mi != bk and mi < len(chars) and mi != prev:
            text += chars[mi]
        prev = mi if mi not in (0, bk) else -1
    
    found_mei = text.find('每')
    print(f"{label:20s} ({w:4d}x{h:2d} -> {resized_w:4d}x48): text='{text[:50]}' [每 at pos {found_mei}]")

# Also try: use the EXACT coordinates without upscaling, with wider margins
print("\n--- Wider crops ---")
for label, y0, y1 in [("No margin  ", 20, 37), ("+2 top+bottom", 18, 39), ("+4 top+bottom", 16, 41)]:
    crop = img[y0:y1, :]  # Full width
    h, w = crop.shape[:2]
    imgH = 48
    wh_ratio = w / h
    max_wh_ratio = max(320/48, wh_ratio)
    target_w = int(imgH * max_wh_ratio)
    if target_w > 3200: target_w = 3200
    ratio = w / h
    resized_w = target_w if math.ceil(imgH * ratio) > target_w else int(math.ceil(imgH * ratio))
    resized = cv2.resize(crop, (min(resized_w, 3200), imgH))
    resized_w = resized.shape[1]
    inp = resized.astype(np.float32).transpose((2, 0, 1)) / 255
    inp -= 0.5; inp /= 0.5
    padding = np.zeros((3, imgH, target_w), dtype=np.float32)
    padding[:, :, 0:resized_w] = inp
    inp = padding[None, :, :, :]
    out = rec.run([rec_out], {rec_inp: inp})[0][0]
    bk = len(chars); prev = -1; text = ''
    for t in range(out.shape[0]):
        mi = int(np.argmax(out[t]))
        if mi > 0 and mi != bk and mi < len(chars) and mi != prev:
            text += chars[mi]
        prev = mi if mi not in (0, bk) else -1
    found_mei = text.find('每')
    print(f"{label:20s} ({w:4d}x{h:2d} -> {resized_w:4d}x48): text='{text[:50]}' [每 at pos {found_mei}]")
