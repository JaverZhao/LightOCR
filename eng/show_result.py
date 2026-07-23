import json
with open(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\result_pretty.json', 'r', encoding='utf-8') as f:
    data = json.load(f)
for line in data['lines']:
    text = line['text']
    idxs = [hex(ord(c)) for c in text[:15]]
    print(f"[{line['order']}] {len(text)} chars: {idxs}")
    # Check for specific chars
    for check in ['每', '完', '成', '修', '改', '文', '件', '清', '单']:
        pos = text.find(check)
        if pos >= 0:
            print(f"  -> '{check}' at pos {pos}")
