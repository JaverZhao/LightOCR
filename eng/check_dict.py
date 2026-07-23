import re
import sys

# Check ONNX model dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.yml', 'r', encoding='utf-8') as f:
    content = f.read()

match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content, re.DOTALL)
if match:
    dict_text = match.group(1)
    chars = []
    for line in dict_text.split('\n'):
        line = line.strip()
        if line.startswith('- '):
            ch = line[2:]
            ch = ch.strip("'\"")
            chars.append(ch)
    
    print(f"ONNX yml dict: {len(chars)} chars")
    print(f"  First 20: {[c for c in chars[:20]]}")
    for c in 'BOARDING':
        idx = chars.index(c) if c in chars else -1
        print(f"  '{c}': index {idx}")
    for c in '每完成一个任务输出':
        idx = chars.index(c) if c in chars else -1
        print(f"  '{c}': index {idx}")

# Check Paddle Inference model dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\rec\inference.yml', 'r', encoding='utf-8') as f:
    content2 = f.read()

match2 = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content2, re.DOTALL)
if match2:
    dict_text2 = match2.group(1)
    chars2 = []
    for line in dict_text2.split('\n'):
        line = line.strip()
        if line.startswith('- '):
            ch = line[2:]
            ch = ch.strip("'\"")
            chars2.append(ch)
    
    print(f"\nPaddle yml dict: {len(chars2)} chars")
    print(f"  Length match ONNX: {len(chars2) == len(chars)}")
    for c in 'BOARDING':
        idx = chars2.index(c) if c in chars2 else -1
        print(f"  '{c}': index {idx}")

# Check our extracted dict
with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt', 'r', encoding='utf-8') as f:
    extracted = f.read()
print(f"\nExtracted dict: {len(extracted)} chars")
for c in 'BOARDING每完成一个任务输出':
    idx = extracted.find(c)
    print(f"  '{c}': index {idx}")
