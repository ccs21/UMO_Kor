"""Build verified PC-only texture bundles without modifying original game data."""
import argparse
from concurrent.futures import ProcessPoolExecutor
import hashlib
import json
import re
from pathlib import Path

import UnityPy
from inspect_bundle import decrypt_bundle


def prepare(path, data_root, master):
    relative = path.resolve().relative_to(data_root.resolve())
    if relative.parts[0] != "android":
        raise ValueError("Only android bundle content is supported")
    # Some ly/sb/*.xab files are raw CRI AFS2 sound banks, not Unity bundles.
    # Never run image conversion or decryption on those audio containers.
    with path.open("rb") as source:
        if source.read(4) == b"AFS2":
            return {"file": relative.as_posix(), "status": "audio-not-needed"}
    original = decrypt_bundle(path, master)
    digest = hashlib.sha256(original).hexdigest()
    output = data_root / "WindowsCache" / relative
    stamp = output.with_suffix(output.suffix + ".sha256")
    if output.exists() and stamp.exists() and stamp.read_text().strip() == digest:
        return {"file": relative.as_posix(), "status": "cached"}
    env = UnityPy.load(original)
    changed = {}
    untouched = {}
    for asset in env.assets:
        for obj in asset.objects.values():
            key = (asset.name, obj.path_id)
            if obj.type.name == "Texture2D":
                texture = obj.read()
                # Android ETC/EAC/PVRTC/ASTC formats. Common desktop formats stay intact.
                if texture.m_TextureFormat >= 30:
                    decoded = texture.image.convert("RGBA")
                    changed[key] = (texture.m_Name, decoded.size, hashlib.sha256(decoded.tobytes()).hexdigest())
                    texture.set_image(decoded, target_format=4, mipmap_count=max(1, texture.m_MipCount or 1))
                    texture.save()
                    continue
            untouched[key] = hashlib.sha256(obj.get_raw_data()).hexdigest()
    if not changed:
        return {"file": relative.as_posix(), "status": "not-needed"}
    rebuilt = env.file.save(packer="lz4")
    verified = UnityPy.load(rebuilt)
    seen = set()
    for asset in verified.assets:
        for obj in asset.objects.values():
            key = (asset.name, obj.path_id)
            seen.add(key)
            if key in changed:
                name, size, pixel_hash = changed[key]
                texture = obj.read()
                assert texture.m_TextureFormat == 4
                decoded = texture.image.convert("RGBA")
                assert decoded.size == size and hashlib.sha256(decoded.tobytes()).hexdigest() == pixel_hash, name
            else:
                assert hashlib.sha256(obj.get_raw_data()).hexdigest() == untouched[key], key
    assert seen == set(changed) | set(untouched)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    temporary.write_bytes(rebuilt)
    temporary.replace(output)
    stamp.write_text(digest + "\n", encoding="ascii")
    return {"file": relative.as_posix(), "status": "converted", "textures": [x[0] for x in changed.values()], "bytes": len(rebuilt)}


def prepare_job(job):
    path, data_root, master = job
    try:
        return prepare(path, data_root, master)
    except Exception as error:
        return {"file": str(path), "status": "error", "error": str(error)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    root = Path(__file__).resolve().parents[2]
    parser.add_argument("--data-root", type=Path, default=root / "Unity/Build/Windows/UMO_Kor/Data")
    parser.add_argument("--master", type=Path, default=root / "Data/RequestMaster.json")
    parser.add_argument("--runtime-log", type=Path, action="append", default=[])
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--workers", type=int, choices=range(1, 9), default=1,
                        help="Parallel conversion processes (1-8); each writes a distinct cache path")
    parser.add_argument("--directory", type=Path, action="append", default=[],
                        help="Recursively include all .xab bundles in this directory")
    parser.add_argument("files", type=Path, nargs="*")
    args = parser.parse_args()
    paths = set(p.resolve() for p in args.files)
    for directory in args.directory:
        resolved = directory.resolve()
        resolved.relative_to((args.data_root / "android").resolve())
        if not resolved.is_dir():
            parser.error(f"Not a bundle directory: {directory}")
        paths.update(p.resolve() for p in resolved.rglob("*.xab"))
    if args.all:
        paths.update(p.resolve() for p in (args.data_root / "android").rglob("*.xab"))
    for log in args.runtime_log:
        for match in re.finditer(r"\[UMO PC bundle\] (.+?\.xab) CABs=", log.read_text(encoding="utf-8-sig", errors="replace")):
            paths.add(Path(match.group(1)).resolve())
    report = []
    failures = 0
    jobs = [(path, args.data_root, args.master) for path in sorted(paths)]
    executor = ProcessPoolExecutor(max_workers=args.workers) if args.workers > 1 else None
    try:
        results = executor.map(prepare_job, jobs, chunksize=1) if executor else map(prepare_job, jobs)
        for index, result in enumerate(results, 1):
            failures += result["status"] == "error"
            report.append(result)
            print(f"{index}/{len(paths)} " + json.dumps(result, ensure_ascii=False), flush=True)
    finally:
        if executor:
            executor.shutdown(wait=True)
    output = args.data_root / "WindowsCache"
    output.mkdir(parents=True, exist_ok=True)
    (output / "last-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Done: bundles={len(paths)} failures={failures}", flush=True)
    raise SystemExit(1 if failures else 0)


if __name__ == "__main__":
    main()
