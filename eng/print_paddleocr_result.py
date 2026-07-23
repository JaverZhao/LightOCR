from paddleocr import PaddleOCR

ocr = PaddleOCR(use_textline_orientation=False, lang='ch', engine='onnxruntime')
result = ocr.predict(r'J:\Javer_Workplace\dev\LightOCR\tests\ChineseTests\ChineseTests.png')

def extract_texts(obj, depth=0):
    if isinstance(obj, dict):
        if 'text' in obj:
            print(f"  {'  '*depth}Text: {obj['text']}")
        elif 'rec_text' in obj:
            print(f"  {'  '*depth}RecText: {obj['rec_text']}")
        for k, v in obj.items():
            if k in ('text', 'rec_text', 'dt_polys', 'rec_texts', 'boxes'):
                continue
            extract_texts(v, depth+1)
    elif isinstance(obj, (list, tuple)):
        for item in obj[:5]:
            extract_texts(item, depth+1)

extract_texts(result)
