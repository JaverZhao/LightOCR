import re
with open(r'C:\Users\Administrator\.paddlex\official_models\PP-OCRv6_medium_rec_onnx\inference.yml', 'r', encoding='utf-8') as f:
    content = f.read()

print(f"YAML size: {len(content)} chars")

# Look for character_dict
import yaml
try:
    data = yaml.safe_load(content)
    pp = data.get('PostProcess', {})
    if 'character_dict' in pp:
        chars = pp['character_dict']
        print(f"Found character_dict with {len(chars)} entries")
        # Save to file
        with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\official_medium_dict.txt', 'w', encoding='utf-8') as f:
            f.write(''.join(chars))
        for c in '每完成一个任务输出BOARDING修改文件清单关键设计决定构建命令及结果测试已知问题下一步':
            idx = chars.index(c) if c in chars else -1
            print(f"  '{c}': {idx}")
    else:
        print("No character_dict in PostProcess")
        print(f"PostProcess keys: {list(pp.keys()) if isinstance(pp, dict) else 'not dict'}")
except Exception as e:
    print(f"YAML parse error: {e}")
    # Try regex approach
    match = re.search(r'character_dict:\n(.*?)(?=\n\S|\Z)', content, re.DOTALL)
    if match:
        print("Found via regex, trying to extract...")
