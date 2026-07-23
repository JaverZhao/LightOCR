"""Extract the exact recognition alphabet paired with the deployed ONNX model."""
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[1]
MODEL_YAML = ROOT / "models" / "onnx" / "rec" / "inference.yml"
OUTPUTS = (
    ROOT / "models" / "onnx" / "ppocrv6_dict.txt",
    ROOT / "models" / "dict" / "ppocrv6_dict.txt",
)

metadata = yaml.safe_load(MODEL_YAML.read_text(encoding="utf-8"))
characters = metadata["PostProcess"]["character_dict"]
if not characters or any(not isinstance(char, str) or len(char) != 1 for char in characters):
    raise ValueError("PP-OCR character_dict must contain one Unicode code point per entry")

dictionary = "".join(characters)
for output in OUTPUTS:
    output.write_text(dictionary, encoding="utf-8", newline="")

print(f"Extracted {len(characters)} characters from {MODEL_YAML}")
for output in OUTPUTS:
    print(f"Wrote {output}")
