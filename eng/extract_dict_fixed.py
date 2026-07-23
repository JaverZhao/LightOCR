"""Extract dictionary from YAML, properly handling all characters including quotes"""
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
            raw = line[2:]
            # Handle single-quoted strings: '...'
            if raw.startswith("'") and raw.endswith("'"):
                ch = raw[1:-1]
            # Handle double-quoted strings: "..."
            elif raw.startswith('"') and raw.endswith('"'):
                ch = raw[1:-1]
            else:
                ch = raw
            # Handle escaped characters
            ch = ch.replace('\\n', '\n').replace('\\t', '\t').replace('\\r', '\r')
            chars.append(ch)
    
    with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt', 'w', encoding='utf-8') as f:
        f.write(''.join(chars))
    
    print(f"Extracted {len(chars)} characters")
    print(f"First 20: {[c for c in chars[:20]]}")
    for c in 'BOARDING':
        idx = chars.index(c) if c in chars else -1
        print(f"  '{c}': index {idx}")
