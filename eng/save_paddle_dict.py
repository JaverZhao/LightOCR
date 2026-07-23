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
    
    with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocrv6_dict.txt', 'w', encoding='utf-8') as f:
        f.write(''.join(chars))
    
    print(f"Written {len(chars)} chars from Paddle model dict")
    if 'B' in chars:
        print(f"B at index {chars.index('B')}")
    else:
        print("B NOT FOUND!")
else:
    print("No character dict found")
