"""Extract every decodable Texture2D and collect likely UI assets separately.

The source bundles are read-only.  A per-bundle state file makes a long extraction
resumable, and the generated manifest preserves the information needed to locate
the original Texture2D again when translated artwork is injected later.
"""

import argparse
import csv
from concurrent.futures import ProcessPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sys
import time

import UnityPy

from inspect_bundle import decrypt_bundle


INVALID_FILENAME = re.compile(r'[<>:"/\\|?*\x00-\x1f]')
UI_WORDS = re.compile(
    r"(?:^|[_\-.])(ui|gui|menu|btn|button|icon|banner|logo|title|text|label|"
    r"window|frame|panel|popup|dialog|notice|help|tutorial|tuto|mission|present|"
    r"gacha|live|result|home|header|footer|tab|badge|rank|status|gauge|cursor|"
    r"arrow|loading|common|cmn|category|select|setting|shop|quest|event)(?:$|[_\-.])",
    re.IGNORECASE,
)
UI_ROOTS = {"ap", "ar", "ly", "mc", "mn", "msg", "re", "sc"}
CONTENT_UI_GROUPS = {
    "ba",  # banners
    "bn",  # banners
    "dc",  # downloadable/content cards
    "ep",  # episode screens
    "ev",  # event screens
    "gc",  # gacha content
    "im",  # item images
    "md",  # mode/help content
    "qu",  # quest content
    "sn",  # story/navigation content
    "to",  # tutorial/other content
    "tp",  # tips
}
MODEL_ROOTS = {"ad", "adv", "dr", "dv", "ef", "gm", "st", "vl"}


def safe_component(value, fallback="unnamed", limit=80):
    value = INVALID_FILENAME.sub("_", str(value)).strip(" .")
    value = re.sub(r"\s+", " ", value)
    if not value:
        value = fallback
    if len(value) > limit:
        digest = hashlib.sha1(value.encode("utf-8", errors="replace")).hexdigest()[:8]
        value = value[: limit - 9] + "_" + digest
    return value


def object_id_text(path_id):
    return ("m" + str(abs(path_id))) if path_id < 0 else str(path_id)


def ui_score(relative_bundle, texture_name, width, height, has_alpha, sprite_count):
    parts = relative_bundle.parts
    root = parts[0].lower() if parts else ""
    second = parts[1].lower() if len(parts) > 1 else ""
    searchable = "/".join(parts) + "/" + texture_name
    score = 0
    reasons = []

    if root in UI_ROOTS:
        score += 6
        reasons.append("UI 중심 번들 경로")
    if root == "ct" and second in CONTENT_UI_GROUPS:
        score += 4
        reasons.append("배너·화면·아이템 계열 콘텐츠 경로")
    if UI_WORDS.search(searchable):
        score += 6
        reasons.append("UI 관련 이름")
    if sprite_count:
        score += 3
        reasons.append("같은 번들에 Sprite 포함")
    if width >= 256 and height >= 32 and (width / max(height, 1) >= 2.5 or height / max(width, 1) >= 2.5):
        score += 2
        reasons.append("버튼·배너형 화면 비율")
    if has_alpha:
        score += 1
        reasons.append("투명 영역 포함")
    if root in MODEL_ROOTS:
        score -= 4
        reasons.append("3D·연출 중심 경로")
    if re.search(r"(?:^|_)(?:base|mask|normal|nrm|spec|specular|metal|rough|ao)(?:$|_)", texture_name, re.IGNORECASE):
        score -= 3
        reasons.append("모델 재질형 이름")

    return score, reasons


def make_output_relative(bundle_relative, asset_name, asset_index, path_id, texture_name):
    bundle_dir = bundle_relative.with_suffix("")
    asset_tag = hashlib.sha1(asset_name.encode("utf-8", errors="replace")).hexdigest()[:8]
    filename = (
        f"a{asset_index:02d}_{asset_tag}__t{object_id_text(path_id)}__"
        f"{safe_component(texture_name)}.png"
    )
    return bundle_dir / filename


def save_state(path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def output_files_present(state, output_root):
    for item in state.get("images", []):
        if not (output_root / item["all_images_path"]).is_file():
            return False
        if item.get("ui_candidate") and not (output_root / item["ui_candidate_path"]).is_file():
            return False
    return True


def extract_bundle(job):
    bundle_path_text, android_root_text, master_text, output_root_text, threshold = job
    bundle_path = Path(bundle_path_text)
    android_root = Path(android_root_text)
    master = Path(master_text)
    output_root = Path(output_root_text)
    relative = bundle_path.relative_to(android_root)
    state_path = output_root / "State" / relative.with_suffix(".json")

    if state_path.is_file():
        try:
            state = json.loads(state_path.read_text(encoding="utf-8"))
            stat = bundle_path.stat()
            if (
                state.get("source_size") == stat.st_size
                and state.get("source_mtime_ns") == stat.st_mtime_ns
                and not state.get("errors")
                and output_files_present(state, output_root)
            ):
                return {
                    "bundle": relative.as_posix(),
                    "status": "cached",
                    "images": len(state.get("images", [])),
                    "ui_candidates": sum(bool(x.get("ui_candidate")) for x in state.get("images", [])),
                    "errors": len(state.get("errors", [])),
                }
        except Exception:
            pass

    stat = bundle_path.stat()
    payload = {
        "bundle": relative.as_posix(),
        "source_size": stat.st_size,
        "source_mtime_ns": stat.st_mtime_ns,
        "images": [],
        "errors": [],
    }
    with bundle_path.open("rb") as source:
        signature = source.read(4)
    if signature == b"AFS2":
        payload["status"] = "audio-skipped"
        save_state(state_path, payload)
        return {"bundle": relative.as_posix(), "status": "audio-skipped", "images": 0, "ui_candidates": 0, "errors": 0}

    try:
        env = UnityPy.load(decrypt_bundle(bundle_path, master))
    except Exception as error:
        payload["status"] = "bundle-error"
        payload["errors"].append({"stage": "load", "error": repr(error)})
        save_state(state_path, payload)
        return {"bundle": relative.as_posix(), "status": "bundle-error", "images": 0, "ui_candidates": 0, "errors": 1}

    sprite_count = sum(1 for obj in env.objects if obj.type.name == "Sprite")
    asset_indices = {id(asset): index for index, asset in enumerate(env.assets)}
    for obj in env.objects:
        if obj.type.name != "Texture2D":
            continue
        try:
            texture = obj.read()
            width = int(getattr(texture, "m_Width", 0) or 0)
            height = int(getattr(texture, "m_Height", 0) or 0)
            # Unity's built-in legacy Font object commonly carries a 0x0
            # placeholder named "Font Texture".  It has no pixels to export
            # and asking UnityPy to decode it causes a bogus dependency lookup.
            if width <= 0 or height <= 0:
                continue
            image = texture.image
            if image is None:
                raise ValueError("UnityPy returned no decoded image")
            image = image.convert("RGBA")
            decoded_width, decoded_height = image.size
            if (decoded_width, decoded_height) != (width, height):
                width, height = decoded_width, decoded_height
            alpha_extrema = image.getchannel("A").getextrema()
            has_alpha = alpha_extrema[0] < 255
            texture_name = getattr(texture, "m_Name", "") or f"Texture2D_{obj.path_id}"
            asset = getattr(obj, "assets_file", None)
            asset_name = getattr(asset, "name", "") or "asset"
            asset_index = asset_indices.get(id(asset), 0)
            output_relative = make_output_relative(relative, asset_name, asset_index, obj.path_id, texture_name)
            all_relative = Path("All_Images") / output_relative
            all_path = output_root / all_relative
            all_path.parent.mkdir(parents=True, exist_ok=True)
            image.save(all_path, format="PNG", compress_level=6)

            score, reasons = ui_score(relative, texture_name, width, height, has_alpha, sprite_count)
            is_candidate = score >= threshold
            ui_relative = Path("UI_Candidates") / output_relative if is_candidate else None
            if ui_relative is not None:
                ui_path = output_root / ui_relative
                ui_path.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(all_path, ui_path)

            payload["images"].append({
                "bundle": relative.as_posix(),
                "asset_name": asset_name,
                "asset_index": asset_index,
                "path_id": obj.path_id,
                "texture_name": texture_name,
                "width": width,
                "height": height,
                "texture_format": int(getattr(texture, "m_TextureFormat", -1)),
                "mipmap_count": int(getattr(texture, "m_MipCount", 0) or 0),
                "has_alpha": has_alpha,
                "decoded_rgba_sha256": hashlib.sha256(image.tobytes()).hexdigest(),
                "all_images_path": all_relative.as_posix(),
                "ui_candidate": is_candidate,
                "ui_score": score,
                "ui_reasons": reasons,
                "ui_candidate_path": ui_relative.as_posix() if ui_relative else "",
            })
        except Exception as error:
            payload["errors"].append({
                "stage": "texture",
                "path_id": obj.path_id,
                "error": repr(error),
            })

    payload["status"] = "done" if not payload["errors"] else "done-with-errors"
    save_state(state_path, payload)
    return {
        "bundle": relative.as_posix(),
        "status": payload["status"],
        "images": len(payload["images"]),
        "ui_candidates": sum(bool(x["ui_candidate"]) for x in payload["images"]),
        "errors": len(payload["errors"]),
    }


def write_manifests(output_root):
    manifests = output_root / "Manifests"
    manifests.mkdir(parents=True, exist_ok=True)
    jsonl_path = manifests / "images.jsonl"
    csv_path = manifests / "images.csv"
    errors_path = manifests / "errors.jsonl"
    fields = [
        "bundle", "asset_name", "asset_index", "path_id", "texture_name",
        "width", "height", "texture_format", "mipmap_count", "has_alpha",
        "decoded_rgba_sha256", "all_images_path", "ui_candidate", "ui_score",
        "ui_reasons", "ui_candidate_path",
    ]
    bundle_count = image_count = ui_count = error_count = 0
    with jsonl_path.open("w", encoding="utf-8") as jsonl, csv_path.open("w", encoding="utf-8-sig", newline="") as csv_file, errors_path.open("w", encoding="utf-8") as errors:
        writer = csv.DictWriter(csv_file, fieldnames=fields)
        writer.writeheader()
        for state_path in sorted((output_root / "State").rglob("*.json")):
            state = json.loads(state_path.read_text(encoding="utf-8"))
            bundle_count += 1
            for item in state.get("images", []):
                image_count += 1
                ui_count += bool(item.get("ui_candidate"))
                jsonl.write(json.dumps(item, ensure_ascii=False) + "\n")
                row = dict(item)
                row["ui_reasons"] = " | ".join(row.get("ui_reasons", []))
                writer.writerow({key: row.get(key, "") for key in fields})
            for error in state.get("errors", []):
                error_count += 1
                errors.write(json.dumps({"bundle": state.get("bundle"), **error}, ensure_ascii=False) + "\n")
    return {
        "bundles": bundle_count,
        "images": image_count,
        "ui_candidates": ui_count,
        "errors": error_count,
        "images_jsonl": jsonl_path.as_posix(),
        "images_csv": csv_path.as_posix(),
        "errors_jsonl": errors_path.as_posix(),
    }


def write_guide(output_root, summary, android_root, threshold):
    text = f"""# UMO 이미지 전체 추출 결과

- 원본 데이터: `{android_root}`
- 처리한 번들: {summary['bundles']:,}개
- 추출한 Texture2D: {summary['images']:,}개
- UI 후보 사본: {summary['ui_candidates']:,}개
- 기록된 오류: {summary['errors']:,}개
- UI 후보 판정 기준점: {threshold}점

## 폴더

- `All_Images`: 디코딩할 수 있었던 모든 Texture2D PNG
- `UI_Candidates`: 경로·이름·Sprite 포함 여부·화면 비율 등으로 넓게 선별한 UI 후보 사본
- `Manifests/images.csv`: Excel에서 열 수 있는 원본 번들 대응표
- `Manifests/images.jsonl`: 자동 재주입 도구에서 사용할 상세 대응표
- `Manifests/errors.jsonl`: 읽지 못한 번들 또는 텍스처 목록
- `State`: 중단 후 이어받기를 위한 번들별 작업 기록

`UI_Candidates`는 자동 선별 결과이므로 UI가 아닌 이미지가 일부 포함되거나 이름이 모호한 UI가 빠질 수 있습니다.
실제 번역 이미지는 PNG 크기와 투명도를 유지해 편집해야 하며, 재주입 시 `Manifests`의 번들·asset·path_id를 사용합니다.
"""
    (output_root / "읽어주세요.md").write_text(text, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    root = Path(__file__).resolve().parents[2]
    parser.add_argument("--android-root", type=Path, default=root / "Unity/Build/Windows/UMO_Kor/Data/android")
    parser.add_argument("--master", type=Path, default=root / "Data/RequestMaster.json")
    parser.add_argument("--output", type=Path, default=root / "outputs/umo_images")
    parser.add_argument("--workers", type=int, choices=range(1, 9), default=min(6, os.cpu_count() or 1))
    parser.add_argument("--ui-threshold", type=int, default=5)
    parser.add_argument("--limit", type=int, default=0, help="Only process this many bundles (for a trial run)")
    parser.add_argument("--directory", type=Path, action="append", default=[])
    args = parser.parse_args()

    android_root = args.android_root.resolve()
    output_root = args.output.resolve()
    if args.directory:
        paths = set()
        for directory in args.directory:
            directory = directory.resolve()
            directory.relative_to(android_root)
            paths.update(directory.rglob("*.xab"))
        bundles = sorted(paths)
    else:
        bundles = sorted(android_root.rglob("*.xab"))
    if args.limit:
        bundles = bundles[: args.limit]
    output_root.mkdir(parents=True, exist_ok=True)

    jobs = [
        (str(path), str(android_root), str(args.master.resolve()), str(output_root), args.ui_threshold)
        for path in bundles
    ]
    started = time.monotonic()
    totals = {"images": 0, "ui_candidates": 0, "errors": 0}
    with ProcessPoolExecutor(max_workers=args.workers) as executor:
        for index, result in enumerate(executor.map(extract_bundle, jobs, chunksize=1), 1):
            for key in totals:
                totals[key] += result[key]
            elapsed = max(time.monotonic() - started, 0.001)
            rate = index / elapsed
            remaining = (len(bundles) - index) / rate if rate else 0
            print(
                f"{index}/{len(bundles)} ({index / max(len(bundles), 1) * 100:5.1f}%) "
                f"images={totals['images']} ui={totals['ui_candidates']} errors={totals['errors']} "
                f"eta={remaining / 60:.1f}m {result['status']} {result['bundle']}",
                flush=True,
            )

    summary = write_manifests(output_root)
    summary.update({
        "source": android_root.as_posix(),
        "output": output_root.as_posix(),
        "ui_threshold": args.ui_threshold,
        "elapsed_seconds": round(time.monotonic() - started, 3),
    })
    (output_root / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    write_guide(output_root, summary, android_root, args.ui_threshold)
    print("DONE " + json.dumps(summary, ensure_ascii=False), flush=True)
    return 1 if summary["errors"] else 0


if __name__ == "__main__":
    sys.exit(main())
