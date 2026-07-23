"""Debug detection on Chinese test image"""
import cv2, numpy as np

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h, w = img.shape[:2]
print(f"Image: {w}x{h}")

# 1. Try edge detection to see text regions
gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
edges = cv2.Canny(gray, 50, 150)

# Find text-like horizontal strips
hist = np.sum(edges > 0, axis=1)  # Sum edges per row
print(f"Edge histogram (rows with > 50 edges):")
for y in range(h):
    if hist[y] > 50:
        print(f"  y={y}: {int(hist[y])} edge pixels")

# 2. Show the crop that the detector found
# Run the actual detection
import onnxruntime as ort

det = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\det\inference.onnx')
det_inp = det.get_inputs()[0].name
det_out = det.get_outputs()[0].name

# Use our preprocessing (same as C++)
limit_side_len = 960
ratio = 1.0
if max(w, h) > limit_side_len:
    ratio = limit_side_len / max(w, h)
nw = max(32, int(w * ratio / 32) * 32)
nh = max(32, int(h * ratio / 32) * 32)
print(f"\nDetection resize: {w}x{h} -> {nw}x{nh} (scale={w/nw:.4f})")

resized = cv2.resize(img, (nw, nh))
mean = np.array([0.485, 0.456, 0.406], dtype=np.float32)
std = np.array([0.229, 0.224, 0.225], dtype=np.float32)
inp = (resized.astype(np.float32) / 255.0 - mean) / std
inp = np.transpose(inp, (2, 0, 1))[None, :, :, :]
prob = det.run([det_out], {det_inp: inp})[0][0, 0]

scale = w / nw
binary = (prob > 0.2).astype(np.uint8) * 255
num_labels, labels = cv2.connectedComponents(binary)

print(f"\nConnected components: {num_labels-1}")
for i in range(1, num_labels):
    ys, xs = np.where(labels == i)
    if len(ys) < 3:
        continue
    avg = prob[ys, xs].mean()
    if avg < 0.45:
        continue
    x0, x1 = int(xs.min() * scale), int(xs.max() * scale)
    y0, y1 = int(ys.min() * scale), int(ys.max() * scale)
    x0, x1 = max(0, x0), min(w, x1)
    y0, y1 = max(0, y0), min(h, y1)
    bw, bh = x1 - x0, y1 - y0
    if bw < 5 or bh < 5:
        continue
    print(f"  Box {i}: ({x0},{y0})-({x1},{y1}) = {bw}x{bh}, prob={avg:.3f}")
    
    if i == 1:
        crop = img[y0:y1, x0:x1]
        cv2.imwrite(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\crop_1.png', crop)
        print(f"    Saved crop to tests/ChineseTests/crop_1.png ({crop.shape[1]}x{crop.shape[0]})")
