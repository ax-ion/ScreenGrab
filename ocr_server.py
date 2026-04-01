"""
Persistent OCR server for ScreenGrab.
Communicates via stdin/stdout - receives image paths, returns JSON with word bounding boxes.
Stays running to avoid cold start on each capture.
"""
import os
import sys
import json
import warnings

warnings.filterwarnings("ignore")
os.environ["PYTORCH_ENABLE_MPS_FALLBACK"] = "1"

import easyocr


def main():
    reader = easyocr.Reader(['en'], gpu=False, verbose=False)

    sys.stdout.write("READY\n")
    sys.stdout.flush()

    for line in sys.stdin:
        image_path = line.strip()
        if not image_path:
            continue
        if image_path == "EXIT":
            break

        try:
            # detail=1 returns bounding boxes
            result = reader.readtext(image_path, detail=1, paragraph=False)
            words = []

            if result:
                raw_words = []
                for item in result:
                    box, text, confidence = item
                    if not text.strip():
                        continue

                    # box is [[x1,y1],[x2,y2],[x3,y3],[x4,y4]]
                    xs = [p[0] for p in box]
                    ys = [p[1] for p in box]
                    x = min(xs)
                    y = min(ys)
                    w = max(xs) - x
                    h = max(ys) - y

                    raw_words.append({
                        "text": text.strip(),
                        "x": round(float(x), 1),
                        "y": round(float(y), 1),
                        "width": round(float(w), 1),
                        "height": round(float(h), 1),
                        "confidence": round(float(confidence), 3)
                    })

                # Sort by Y then X
                raw_words.sort(key=lambda w: (w["y"], w["x"]))

                # Group into lines by Y proximity
                lines = []
                current_line = []
                last_y = None
                for word in raw_words:
                    if last_y is not None and abs(word["y"] - last_y) > word["height"] * 0.5:
                        if current_line:
                            current_line.sort(key=lambda w: w["x"])
                            lines.append(current_line)
                        current_line = []
                    current_line.append(word)
                    last_y = word["y"]
                if current_line:
                    current_line.sort(key=lambda w: w["x"])
                    lines.append(current_line)

                # Assign line indices
                for line_idx, line_words in enumerate(lines):
                    for word in line_words:
                        word["lineIndex"] = line_idx
                        words.append(word)

            output = json.dumps({"words": words, "error": None})
        except Exception as e:
            output = json.dumps({"words": [], "error": str(e)})

        sys.stdout.write(output + "\n")
        sys.stdout.flush()


if __name__ == "__main__":
    main()
