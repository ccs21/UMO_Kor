"""Build the reviewed Korean image manifest from the PC translation workspace."""

import argparse
import hashlib
import json
from pathlib import Path
import re

from PIL import Image


NAME = re.compile(r"(.+)__(\d+)x(\d+)__([0-9a-f]{16})\.png$")


def runtime_hash(image_path):
    with Image.open(image_path) as source:
        image = source.convert("RGBA").transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        return hashlib.sha256(image.tobytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    root = Path(__file__).resolve().parents[2]
    parser.add_argument("--images", type=Path, default=root / "Localization/ImageOverrides/PC")
    parser.add_argument("--extraction-manifest", type=Path,
                        default=root / "outputs/umo_image_extraction_20260907/Manifests/images.jsonl")
    parser.add_argument("--output", type=Path,
                        default=root / "Localization/ImageOverrides/manifest.json")
    args = parser.parse_args()

    extracted = {}
    with args.extraction_manifest.open(encoding="utf-8") as source:
        for line in source:
            row = json.loads(line)
            key = (row["texture_name"], int(row["width"]), int(row["height"]))
            extracted.setdefault(key, []).append(row)

    entries = []
    for image_path in sorted(args.images.glob("*.png")):
        match = NAME.fullmatch(image_path.name)
        if not match:
            raise SystemExit("Unexpected reviewed image filename: " + image_path.name)
        texture_name, width, height, runtime_prefix = match.groups()
        key = (texture_name, int(width), int(height))
        candidates = extracted.get(key, [])
        hash_matches = []
        for row in candidates:
            original = args.extraction_manifest.parents[1] / row["all_images_path"]
            if original.is_file() and runtime_hash(original).startswith(runtime_prefix):
                hash_matches.append(row)

        if len(hash_matches) == 1:
            selected = hash_matches[0]
            strategy = "runtime-rgba-hash"
        elif len(candidates) == 1:
            selected = candidates[0]
            strategy = "unique-name-size"
        elif not candidates and texture_name in {
            "cmn_load_pack_base", "cmn_load_pack_mask",
            "cmn_pop01_pack_base", "cmn_pop01_pack_mask",
        }:
            selected = None
            strategy = "built-in-resource"
        else:
            raise SystemExit(
                f"Cannot identify one source texture for {image_path.name}: "
                f"candidates={len(candidates)} hash_matches={len(hash_matches)}"
            )

        translated_sha = hashlib.sha256(image_path.read_bytes()).hexdigest()
        entries.append({
            "file": image_path.name,
            "resourceId": "image_" + hashlib.sha256(image_path.name.encode("utf-8")).hexdigest()[:20],
            "textureName": texture_name,
            "width": int(width),
            "height": int(height),
            "runtimeRgbaSha256Prefix": runtime_prefix,
            "translatedPngSha256": translated_sha,
            "matchStrategy": strategy,
            "builtIn": selected is None,
            "bundles": [] if selected is None else [selected["bundle"]],
            "source": None if selected is None else {
                "assetName": selected["asset_name"],
                "assetIndex": selected["asset_index"],
                "pathId": selected["path_id"],
                "decodedRgbaSha256": selected["decoded_rgba_sha256"],
                "textureFormat": selected["texture_format"],
                "mipmapCount": selected["mipmap_count"],
            },
        })

    if len(entries) != 43:
        raise SystemExit(f"Expected 43 reviewed images, found {len(entries)}")
    if sum(item["builtIn"] for item in entries) != 4:
        raise SystemExit("Expected exactly four built-in resource overrides")

    payload = {"version": 1, "images": entries}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {args.output}: images={len(entries)} bundles={sum(bool(x['bundles']) for x in entries)}")


if __name__ == "__main__":
    main()
