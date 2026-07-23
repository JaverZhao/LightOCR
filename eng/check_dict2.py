import re

with open(r'J:\Javer_Workplace\dev\LightOCR\models\rec\inference.yml', 'r', encoding='utf-8') as f:
    content = f.read()

match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content, re.DOTALL)
if match:
    dict_text = match.group(1)
    chars = []
    for line in dict_text.split('\n'):
        line = line.strip()
        if line.startswith('- '):
            ch = line[2:]
            ch = ch.strip("'\" \t")
            if ch:
                chars.append(ch)
    
    print(f"Paddle model dict: {len(chars)} chars")
    print(f"First 20: {[c for c in chars[:20]]}")
    for c in 'BOARDING':
        idx = chars.index(c) if c in chars else -1
        print(f"  '{c}': index {idx}")
    
    # Test: if blank is last class, then chars[68] should be 'D' (class 68)
    # But from our earlier analysis, 'D' is at index 45 in ONNX dict
    if len(chars) > 68:
        print(f"  chars[68] = {repr(chars[68])}")
    if len(chars) > 43:
        print(f"  chars[43] = {repr(chars[43])}")
else:
    print("No character dict found in Paddle model inference.yml")

# Check the ONNX inference.yml too
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.yml', 'r', encoding='utf-8') as f:
    content2 = f.read()

match2 = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content2, re.DOTALL)
if match2:
    dict_text2 = match2.group(1)
    chars2 = []
    for line in dict_text2.split('\n'):
        line = line.strip()
        if line.startswith('- '):
            ch = line[2:]
            ch = ch.strip("'\" \t")
            if ch:
                chars2.append(ch)
    
    print(f"\nONNX model dict: {len(chars2)} chars")
    # Compare with Paddle dict
    print(f"  Dicts identical: {chars == chars2}")
    if chars != chars2:
        diff_count = sum(1 for i, (a, b) in enumerate(zip(chars, chars2)) if a != b)
        print(f"  Diff count (first {min(len(chars), len(chars2))}): {diff_count}")
        print(f"  First diff at index {next((i for i, (a, b) in enumerate(zip(chars, chars2)) if a != b), 'none')}")
