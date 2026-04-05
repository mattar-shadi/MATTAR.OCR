#!/usr/bin/env python3
"""
huggingface_ocr.py – Hugging Face TrOCR helper for MATTAR.OCR
==============================================================

Called by ``HuggingFaceOcrService`` as a subprocess.

Usage::

    python huggingface_ocr.py <model_id> <image_path> [<image_path> ...]

Arguments
---------
model_id
    Hugging Face model repository ID.
    Default (when called with only one path arg for back-compat):
        ``microsoft/trocr-base-printed``
    Override examples:
        microsoft/trocr-large-printed   – higher accuracy, slower
        microsoft/trocr-base-handwritten – for handwritten text
image_path …
    One or more absolute or relative paths to PNG/JPEG image files.
    Passing all pages at once avoids reloading the model per page.

The model weights are downloaded and cached automatically by the
``transformers`` library on the first call.  Set ``HF_HOME`` (or the
legacy ``TRANSFORMERS_CACHE``) environment variable to control the cache
directory.

Output
------
The recognised text for all images is written to stdout, concatenated
in the order the images were provided.  A newline separates the text
from each page.

Exit codes
----------
0  – success.
1  – wrong number of arguments.
2  – runtime error (stack trace written to stderr).
"""

from __future__ import annotations

import os
import sys


def run_ocr(image_paths: list[str], model_id: str = "microsoft/trocr-base-printed") -> str:
    """Return the text recognised in each image in *image_paths* using *model_id*.

    The model and processor are loaded once and reused for all images, so
    passing multiple pages is significantly faster than separate invocations.
    """
    # Lazy imports so the CLI argument check runs before heavy imports.
    from PIL import Image  # type: ignore[import]
    from transformers import TrOCRProcessor, VisionEncoderDecoderModel  # type: ignore[import]

    processor = TrOCRProcessor.from_pretrained(model_id)
    model = VisionEncoderDecoderModel.from_pretrained(model_id)

    texts: list[str] = []
    for image_path in image_paths:
        image = Image.open(image_path).convert("RGB")
        pixel_values = processor(images=image, return_tensors="pt").pixel_values
        generated_ids = model.generate(pixel_values)
        text: str = processor.batch_decode(generated_ids, skip_special_tokens=True)[0]
        texts.append(text)

    return "\n".join(texts)


if __name__ == "__main__":
    # Expect: <model_id> <image_path> [<image_path> ...]
    if len(sys.argv) < 3:
        print(
            "Usage: python huggingface_ocr.py <model_id> <image_path> [<image_path> ...]",
            file=sys.stderr,
        )
        sys.exit(1)

    _model_id = sys.argv[1]
    _image_paths = sys.argv[2:]

    try:
        result = run_ocr(_image_paths, _model_id)
        # Use end="" to avoid appending an extra newline that would confuse the C# reader.
        print(result, end="")
    except Exception as exc:  # pylint: disable=broad-except
        print(f"Error: {exc}", file=sys.stderr)
        sys.exit(2)
