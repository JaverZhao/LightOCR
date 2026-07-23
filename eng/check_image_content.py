"""Check what the Chinese test image actually contains"""
import cv2

img = cv2.imread(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')
h, w = img.shape[:2]

# Check the first text line region
# From edge histogram: y=19 to y=38 (about 20px)
# But the actual text might start at y=0 (with a small margin at top)
# Let's look at the entire top portion
for y_start in range(0, h, 25):
    strip = img[y_start:y_start+25, :, :]
    # Check if this strip has text (non-white pixels)
    gray = cv2.cvtColor(strip, cv2.COLOR_BGR2GRAY)
    dark_pixels = (gray < 200).sum()
    total = gray.size
    if dark_pixels > total * 0.01:  # at least 1% dark pixels
        print(f"y={y_start}-{y_start+25}: {dark_pixels/total*100:.1f}% dark pixels")

# Find exact text rows  
gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
# Find rows with significant dark content
text_rows = []
for y in range(h):
    dark = (gray[y, :] < 200).sum()
    if dark > w * 0.1:  # >10% of row is dark
        text_rows.append(y)

# Group consecutive rows into text lines
lines = []
if text_rows:
    start = text_rows[0]
    prev = text_rows[0]
    for y in text_rows[1:]:
        if y - prev > 3:  # gap
            lines.append((start, prev))
            start = y
        prev = y
    lines.append((start, prev))

print(f"\nText lines found:")
for i, (y0, y1) in enumerate(lines):
    print(f"  Line {i}: y={y0}-{y1} (height={y1-y0+1})")
    # Show the content
    line_img = img[y0:y1+1, :, :]
    # Try to OCR with PaddleOCR quickly
    print(f"    Actual content at this position")
    
# Also check the first line
first_line = img[lines[0][0]:lines[0][1]+1, :, :]
print(f"\nFirst line image: {first_line.shape[1]}x{first_line.shape[0]}")
