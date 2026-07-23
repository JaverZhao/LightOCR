"""Use PaddleOCR officially to get correct results"""
from paddleocr import PaddleOCR
import json

ocr = PaddleOCR(use_textline_orientation=False, lang='ch', engine='onnxruntime')

result = ocr.predict(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')

# Dump the result structure for debugging
with open(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\paddleocr_result.json', 'w', encoding='utf-8') as f:
    json.dump(result, f, ensure_ascii=False, indent=2)

# Print readable results
if isinstance(result, list):
    for page in result:
        if isinstance(page, list):
            for item in page:
                if isinstance(item, dict):
                    if 'text' in item:
                        print(item['text'])
                    elif 'rec_text' in item:
                        print(item['rec_text'])
                elif isinstance(item, list):
                    for sub in item:
                        if isinstance(sub, dict) and 'text' in sub:
                            print(sub['text'])
