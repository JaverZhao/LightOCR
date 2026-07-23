# Compare the three dictionaries
paddle = open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt', 'r', encoding='utf-8').read()
onnx = open(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.yml', 'r', encoding='utf-8').read()
third = open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\rec_char_dict.txt', 'r', encoding='utf-8').read()

# Extract ONNX dict from yml
import re
match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', onnx, re.DOTALL)
onnx_chars = []
for line in match.group(1).split('\n'):
    line = line.strip()
    if line.startswith('- '):
        ch = line[2:].strip("'\" \t")
        if ch:
            onnx_chars.append(ch)
onnx_str = ''.join(onnx_chars)

print(f"Current dict (from Paddle yml): {len(paddle)} chars")
print(f"ONNX yml dict:                 {len(onnx_str)} chars")
print(f"Third-party dict:              {len(third)} chars")

# Check specific characters
for dict_name, chars in [("Paddle", paddle), ("ONNX", onnx_str), ("Third-party", third)]:
    print(f"\n{dict_name}:")
    for c in "BOARDING":
        idx = chars.find(c)
        print(f"  '{c}': {idx}")

# Check if the third-party dict is a subset of the full dict
third_subset = all(c in paddle for c in third[:1000] if c.isascii())
print(f"\nThird-party ASCII subset of Paddle: {third_subset}")

# Count differences
ascii_third = ''.join(c for c in third if c.isascii() and c.isprintable())
ascii_paddle = ''.join(c for c in paddle if c.isascii() and c.isprintable())
print(f"Third-party ASCII chars: {len(ascii_third)}")
print(f"Paddle ASCII chars: {len(ascii_paddle)}")

# Check if the model output 'D' (at some class) matches our dict at index 45
# If model output class 68 -> char at index 68 -> should be 'D'
# In paddle dict, chars[68] is 'c', not 'D'
for idx in range(40, 80):
    c1 = paddle[idx] if idx < len(paddle) else '?'
    c2 = onnx_str[idx] if idx < len(onnx_str) else '?'
    print(f"  idx {idx}: Paddle='{c1}' ONNX='{c2}'")
