"""Check model metadata and try with known dict"""
import onnxruntime as ort
import numpy as np

# Check rec model
rec = ort.InferenceSession(r'J:\Javer_Workplace\dev\LightOCR\models\onnx\rec\inference.onnx')
meta = rec.get_modelmeta()
print(f"Model metadata:")
print(f"  Producer: {meta.producer_name}")
print(f"  Version: {meta.version}")
print(f"  Graph name: {meta.graph_name}")
print(f"  Custom metadata: {meta.custom_metadata_map}")

# Check input/output
inp = rec.get_inputs()[0]
out = rec.get_outputs()[0]
print(f"\nInput: {inp.name} {inp.shape} {inp.type}")
print(f"Output: {out.name} {out.shape} {out.type}")

# Try using the official PaddleOCR dict
import urllib.request
# Download the dict that comes with PP-OCRv6
url = "https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/main/ppocr/utils/en_dict.txt"
urllib.request.urlretrieve(url, r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocr_official_en_dict.txt')

with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocr_official_en_dict.txt', 'r', encoding='utf-8') as f:
    en_dict = f.read()
print(f"\nOfficial en_dict: {len(en_dict)} chars")
for c in 'BOARDING':
    print(f"  '{c}': {en_dict.find(c)}")

# Try the Chinese keys dict
url2 = "https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/main/ppocr/utils/ppocr_keys_v1.txt"
urllib.request.urlretrieve(url2, r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocr_keys_v1.txt')

with open(r'J:\Javer_Workplace\dev\LightOCR\models\dict\ppocr_keys_v1.txt', 'r', encoding='utf-8') as f:
    cn_dict = f.read()
print(f"\nppocr_keys_v1: {len(cn_dict)} chars")
for c in '每完成一个任务输出':
    idx = cn_dict.find(c)
    print(f"  '{c}': {idx}")
for c in 'BOARDING':
    idx = cn_dict.find(c)
    print(f"  '{c}': {idx}")
