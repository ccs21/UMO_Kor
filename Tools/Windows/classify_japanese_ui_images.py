"""Automatically reduce extracted UMO textures to Japanese UI review candidates.

Every unique decoded image is inspected with inexpensive image heuristics.  OCR is
then run for UI-path images and other text-like images.  Exact duplicate textures
are grouped, while the output manifest keeps every original bundle/path mapping.
"""

import argparse
from concurrent.futures import ThreadPoolExecutor
import csv
import hashlib
import html
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps, ImageStat


JAPANESE = re.compile(r"[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff]")
IGNORE_NAMES = re.compile(
    r"(?:^|_)(?:normal|nrm|mask|spec|specular|metal|rough|noise|shadow|ao|depth)(?:$|_)",
    re.IGNORECASE,
)
LIKELY_ROOTS = {"ap", "ar", "ly", "mn", "msg", "re", "sc"}
MODEL_PATH = re.compile(r"/(?:dr|dv|st|ef|gm|vl)/", re.IGNORECASE)


def find_tesseract(explicit):
    candidates = []
    if explicit:
        candidates.append(Path(explicit))
    found = shutil.which("tesseract")
    if found:
        candidates.append(Path(found))
    candidates.extend([
        Path(r"C:\Program Files\Tesseract-OCR\tesseract.exe"),
        Path(r"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe"),
    ])
    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    raise SystemExit("Tesseract OCR을 찾지 못했습니다. 일본어 데이터와 함께 설치한 뒤 다시 실행하세요.")


def image_metrics(path):
    """Return broad, deliberately permissive text-likelihood measurements."""
    with Image.open(path) as source:
        image = source.convert("RGBA")
        width, height = image.size
        alpha = image.getchannel("A")
        rgb = Image.new("RGB", image.size, "white")
        rgb.paste(image.convert("RGB"), mask=alpha)
        thumb = ImageOps.grayscale(rgb)
        thumb.thumbnail((512, 512), Image.Resampling.LANCZOS)
        contrast = ImageStat.Stat(thumb).stddev[0]
        edges = thumb.filter(ImageFilter.FIND_EDGES)
        edge_mean = ImageStat.Stat(edges).mean[0]
        # Text generally has many local transitions but is not a full-frame noisy texture.
        text_like = width >= 24 and height >= 12 and contrast >= 18 and 4 <= edge_mean <= 62
        flat = contrast < 5
        digest = hashlib.sha1(thumb.resize((32, 32)).tobytes()).hexdigest()[:16]
        return {
            "contrast": round(contrast, 2),
            "edge_mean": round(edge_mean, 2),
            "text_like": text_like,
            "flat": flat,
            "visual_digest": digest,
        }


def prepare_ocr_image(source_path, target_path):
    with Image.open(source_path) as source:
        rgba = source.convert("RGBA")
        background = Image.new("RGB", rgba.size, "white")
        background.paste(rgba.convert("RGB"), mask=rgba.getchannel("A"))
        gray = ImageOps.autocontrast(ImageOps.grayscale(background))
        scale = max(1, min(4, 1400 // max(gray.width, gray.height, 1)))
        if scale > 1:
            gray = gray.resize((gray.width * scale, gray.height * scale), Image.Resampling.LANCZOS)
        gray = ImageEnhance.Contrast(gray).enhance(1.4)
        # A small white margin substantially improves sparse-text recognition.
        gray = ImageOps.expand(gray, border=12, fill=255)
        gray.save(target_path, "PNG")


def run_ocr(job):
    item, source_path, tesseract, tessdata, temp_root = job
    temp_path = Path(temp_root) / (item["decoded_rgba_sha256"][:24] + ".png")
    try:
        prepare_ocr_image(source_path, temp_path)
        command = [str(tesseract), str(temp_path), "stdout", "-l", "jpn+eng", "--psm", "11", "tsv"]
        if tessdata:
            command[3:3] = ["--tessdata-dir", str(tessdata)]
        result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
        tokens = []
        confidences = []
        for index, line in enumerate(result.stdout.splitlines()):
            if index == 0:
                continue
            columns = line.split("\t")
            if len(columns) < 12:
                continue
            token = columns[11].strip()
            try:
                confidence = float(columns[10])
            except ValueError:
                continue
            if token and confidence >= 0:
                tokens.append(token)
                confidences.append(confidence)
        text = " ".join(tokens)
        japanese_chars = len(JAPANESE.findall(text))
        confidence = round(sum(confidences) / len(confidences), 2) if confidences else -1
        item.update({
            "ocr_text": text,
            "ocr_confidence": confidence,
            "japanese_chars": japanese_chars,
            "ocr_error": result.stderr.strip() if result.returncode else "",
        })
    except Exception as error:
        item.update({"ocr_text": "", "ocr_confidence": -1, "japanese_chars": 0, "ocr_error": repr(error)})
    finally:
        temp_path.unlink(missing_ok=True)
    return item


def run_paddle_ocr(engine, item, source_path):
    try:
        result = engine.ocr(str(source_path), cls=False)
        tokens = []
        confidences = []
        # PaddleOCR 2.x returns one outer list per input image.
        lines = result[0] if result and isinstance(result[0], list) else result
        for line in lines or []:
            if not isinstance(line, (list, tuple)) or len(line) < 2:
                continue
            recognition = line[1]
            if not isinstance(recognition, (list, tuple)) or len(recognition) < 2:
                continue
            token = str(recognition[0]).strip()
            confidence = float(recognition[1]) * 100
            if token:
                tokens.append(token)
                confidences.append(confidence)
        text = " ".join(tokens)
        item.update({
            "ocr_text": text,
            "ocr_confidence": round(sum(confidences) / len(confidences), 2) if confidences else -1,
            "japanese_chars": len(JAPANESE.findall(text)),
            "ocr_error": "",
        })
    except Exception as error:
        item.update({"ocr_text": "", "ocr_confidence": -1, "japanese_chars": 0, "ocr_error": repr(error)})
    return item


def category(item):
    chars = item.get("japanese_chars", 0)
    confidence = item.get("ocr_confidence", -1)
    if chars >= 2 and confidence >= 35:
        return "Japanese_High"
    if chars >= 1:
        return "Japanese_Possible"
    if item.get("ocr_text") or item.get("text_like") or item.get("priority_ocr"):
        return "Text_Uncertain"
    return "No_Text"


def write_catalog(output_root, rows):
    shown = [row for row in rows if row["category"] != "No_Text"]
    cards = []
    for row in shown:
        image_path = Path(row["review_path"]).as_posix()
        cards.append(
            '<article data-cat="{}"><a href="{}"><img loading="lazy" src="{}"></a>'
            '<div><b>{}</b><br><span>{}</span><br><em>{}</em><br>{}</div></article>'.format(
                html.escape(row["category"]), html.escape(image_path), html.escape(image_path),
                html.escape(row["texture_name"]), html.escape(row["bundle"]),
                html.escape(row.get("ocr_text", "")), html.escape(row["category"]),
            )
        )
    page = """<!doctype html><meta charset=\"utf-8\"><title>UMO 일본어 이미지 자동 선별</title>
<style>body{font-family:'Malgun Gothic';background:#111827;color:#eee;margin:20px}nav{position:sticky;top:0;background:#111827;padding:12px;z-index:2}button{font-size:16px;padding:9px;margin:3px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:12px}article{background:#1f2937;padding:9px;overflow-wrap:anywhere}img{width:100%;height:220px;object-fit:contain;background:#fff}em{color:#fbbf24}</style>
<nav><b>UMO 일본어 이미지 자동 선별</b> · 총 __COUNT__개
<button onclick=\"f('')\">전체</button><button onclick=\"f('Japanese_High')\">일본어 확실</button><button onclick=\"f('Japanese_Possible')\">일본어 가능</button><button onclick=\"f('Text_Uncertain')\">문자 의심</button></nav>
<div class=\"grid\">__CARDS__</div><script>function f(x){document.querySelectorAll('article').forEach(e=>e.hidden=x&&e.dataset.cat!==x)}</script>"""
    (output_root / "UMO_일본어_이미지_자동선별.html").write_text(
        page.replace("__COUNT__", f"{len(shown):,}").replace("__CARDS__", "\n".join(cards)), encoding="utf-8"
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    root = Path(__file__).resolve().parents[2]
    parser.add_argument("--extraction-root", type=Path, default=root / "outputs/umo_image_extraction_20260907")
    parser.add_argument("--output", type=Path, default=root / "outputs/umo_image_ocr_20260907")
    parser.add_argument("--engine", choices=("paddle", "tesseract"), default="paddle")
    parser.add_argument("--tesseract", type=Path)
    parser.add_argument("--tessdata", type=Path)
    parser.add_argument("--workers", type=int, default=min(6, os.cpu_count() or 1))
    parser.add_argument("--limit", type=int, default=0, help="OCR only this many unique candidates for tuning")
    parser.add_argument("--deep-scan", action="store_true", help="Also inspect non-UI textures with pixel heuristics")
    args = parser.parse_args()

    extraction = args.extraction_root.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    tesseract = find_tesseract(args.tesseract) if args.engine == "tesseract" else None
    manifest_path = extraction / "Manifests/images.jsonl"
    all_rows = [json.loads(line) for line in manifest_path.open(encoding="utf-8")]

    unique = {}
    duplicates = {}
    for row in all_rows:
        digest = row["decoded_rgba_sha256"]
        duplicates.setdefault(digest, []).append(row)
        unique.setdefault(digest, dict(row))

    started = time.monotonic()
    candidates = []
    for index, item in enumerate(unique.values(), 1):
        source = extraction / item["all_images_path"]
        source_root = item["bundle"].split("/", 1)[0].lower()
        model_render = source_root == "mc" and MODEL_PATH.search("/" + item["bundle"] + "/")
        item["priority_ocr"] = (bool(item.get("ui_candidate")) or source_root in LIKELY_ROOTS) and not model_render
        item.update({"contrast": -1, "edge_mean": -1, "text_like": False, "flat": False, "visual_digest": ""})
        if args.deep_scan or item["priority_ocr"]:
            item.update(image_metrics(source))
        item["duplicate_count"] = len(duplicates[item["decoded_rgba_sha256"]])
        if not item["flat"] and not IGNORE_NAMES.search(item["texture_name"]):
            if item["priority_ocr"] or (args.deep_scan and item["text_like"]):
                candidates.append(item)
        if index % 5000 == 0 and args.deep_scan:
            print(f"빠른 선별 {index:,}/{len(unique):,} · OCR 후보 {len(candidates):,}", flush=True)

    candidates.sort(key=lambda x: (
        not x["priority_ocr"],
        0 if x["bundle"].split("/", 1)[0].lower() == "ly" else 1,
        -x.get("ui_score", 0),
        -x["width"] * x["height"],
    ))
    if args.limit:
        candidates = candidates[:args.limit]
    completed = []
    if args.engine == "paddle":
        from paddleocr import PaddleOCR
        print("일본어 OCR 모델을 불러오는 중...", flush=True)
        engine = PaddleOCR(use_angle_cls=False, lang="japan", show_log=False)
        for index, item in enumerate(candidates, 1):
            completed.append(run_paddle_ocr(engine, item, extraction / item["all_images_path"]))
            if index % 25 == 0 or index == len(candidates):
                print(f"OCR {index:,}/{len(candidates):,} ({index/max(len(candidates),1)*100:.1f}%)", flush=True)
    else:
        with tempfile.TemporaryDirectory(prefix="umo-ocr-") as temp_root:
            jobs = [(item, extraction / item["all_images_path"], tesseract, args.tessdata, temp_root) for item in candidates]
            with ThreadPoolExecutor(max_workers=args.workers) as executor:
                for index, item in enumerate(executor.map(run_ocr, jobs), 1):
                    completed.append(item)
                    if index % 100 == 0 or index == len(jobs):
                        print(f"OCR {index:,}/{len(jobs):,} ({index/max(len(jobs),1)*100:.1f}%)", flush=True)

    rows = []
    for item in completed:
        item["category"] = category(item)
        relative = Path(item["category"]) / Path(item["all_images_path"]).relative_to("All_Images")
        destination = output / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(extraction / item["all_images_path"], destination)
        item["review_path"] = relative.as_posix()
        item["all_mappings"] = [
            {key: row[key] for key in ("bundle", "asset_name", "asset_index", "path_id", "texture_name", "all_images_path")}
            for row in duplicates[item["decoded_rgba_sha256"]]
        ]
        rows.append(item)

    manifests = output / "Manifests"
    manifests.mkdir(exist_ok=True)
    with (manifests / "classified_images.jsonl").open("w", encoding="utf-8") as target:
        for row in rows:
            target.write(json.dumps(row, ensure_ascii=False) + "\n")
    fields = ["category", "ocr_text", "ocr_confidence", "japanese_chars", "bundle", "asset_name", "asset_index", "path_id", "texture_name", "width", "height", "duplicate_count", "all_images_path", "review_path"]
    with (manifests / "classified_images.csv").open("w", encoding="utf-8-sig", newline="") as target:
        writer = csv.DictWriter(target, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)
    write_catalog(output, rows)
    counts = {name: sum(row["category"] == name for row in rows) for name in ("Japanese_High", "Japanese_Possible", "Text_Uncertain", "No_Text")}
    summary = {
        "source_images": len(all_rows), "unique_images": len(unique), "ocr_candidates": len(candidates),
        "counts": counts, "elapsed_seconds": round(time.monotonic() - started, 2),
        "engine": args.engine, "tesseract": str(tesseract) if tesseract else "",
    }
    (output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print("DONE " + json.dumps(summary, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
