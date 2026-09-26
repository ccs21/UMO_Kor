"""Build verified PC-only texture bundles without modifying original game data."""
import argparse
from concurrent.futures import ProcessPoolExecutor
import hashlib
import json
import re
from pathlib import Path

import UnityPy
from inspect_bundle import decrypt_bundle

CACHE_FORMAT = "unitypy-rgba32-lz4-v1"


def file_sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_cache(path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(payload, ensure_ascii=False, sort_keys=True) + "\n", encoding="utf-8")
    temporary.replace(path)


def current_cache(cache_path, output, source, master_sha):
    if not cache_path.exists():
        return None
    try:
        cache = json.loads(cache_path.read_text(encoding="utf-8"))
        if (cache.get("format") != CACHE_FORMAT or
                cache.get("master_sha256") != master_sha):
            return None
        source_stat = source.stat()
        if source_stat.st_size != cache.get("source_bytes"):
            return None
        refresh = False
        if source_stat.st_mtime_ns != cache.get("source_mtime_ns"):
            if file_sha256(source) != cache.get("source_sha256"):
                return None
            cache["source_mtime_ns"] = source_stat.st_mtime_ns
            refresh = True
        status = cache.get("result")
        if status == "not-needed":
            if output.exists():
                return None
            if refresh:
                write_cache(cache_path, cache)
            return "not-needed"
        if status != "converted" or not output.is_file():
            return None
        output_stat = output.stat()
        if output_stat.st_size != cache.get("output_bytes"):
            return None
        if output_stat.st_mtime_ns != cache.get("output_mtime_ns"):
            if file_sha256(output) != cache.get("output_sha256"):
                return None
            cache["output_mtime_ns"] = output_stat.st_mtime_ns
            refresh = True
        if refresh:
            write_cache(cache_path, cache)
        return "converted"
    except (OSError, ValueError, TypeError):
        return None


def prepare(path, data_root, master, master_sha=None, previous_status=None, previous_report_mtime_ns=0):
    relative = path.resolve().relative_to(data_root.resolve())
    if relative.parts[0] not in {"android", "dlc"}:
        raise ValueError("Only android and DLC bundle content is supported")
    # Some ly/sb/*.xab files are raw CRI AFS2 sound banks, not Unity bundles.
    # Never run image conversion or decryption on those audio containers.
    with path.open("rb") as source:
        if source.read(4) == b"AFS2":
            return {"file": relative.as_posix(), "status": "audio-not-needed"}
    output = data_root / "WindowsCache" / relative
    cache_path = output.with_suffix(output.suffix + ".cache.json")
    legacy_stamp = output.with_suffix(output.suffix + ".sha256")
    if master_sha is None:
        master_sha = file_sha256(master)
    cached_result = current_cache(cache_path, output, path, master_sha)
    if cached_result is not None:
        return {"file": relative.as_posix(), "status": "cached"}
    source_stat = path.stat()
    source_sha = file_sha256(path)
    master_mtime_ns = master.stat().st_mtime_ns
    if (previous_status == "not-needed" and not output.exists() and
            previous_report_mtime_ns >= max(source_stat.st_mtime_ns, master_mtime_ns)):
        write_cache(cache_path, {
            "format": CACHE_FORMAT,
            "source_sha256": source_sha,
            "source_bytes": source_stat.st_size,
            "source_mtime_ns": source_stat.st_mtime_ns,
            "master_sha256": master_sha,
            "result": "not-needed",
        })
        return {"file": relative.as_posix(), "status": "cached"}
    if output.exists() and legacy_stamp.exists():
        legacy_digest = legacy_stamp.read_text().strip()
        legacy_is_valid = re.fullmatch(r"[0-9a-fA-F]{64}", legacy_digest) is not None
        legacy_is_current = legacy_stamp.stat().st_mtime_ns >= max(source_stat.st_mtime_ns, master_mtime_ns)
        if legacy_is_valid and legacy_is_current:
            output_stat = output.stat()
            write_cache(cache_path, {
                "format": CACHE_FORMAT,
                "source_sha256": source_sha,
                "source_bytes": source_stat.st_size,
                "source_mtime_ns": source_stat.st_mtime_ns,
                "master_sha256": master_sha,
                "result": "converted",
                "output_bytes": output_stat.st_size,
                "output_mtime_ns": output_stat.st_mtime_ns,
                "output_sha256": file_sha256(output),
            })
            legacy_stamp.unlink()
            return {"file": relative.as_posix(), "status": "cached"}
    original = decrypt_bundle(path, master)
    legacy_digest = hashlib.sha256(original).hexdigest()
    if output.exists() and legacy_stamp.exists() and legacy_stamp.read_text().strip() == legacy_digest:
        write_cache(cache_path, {
            "format": CACHE_FORMAT,
            "source_sha256": source_sha,
            "source_bytes": source_stat.st_size,
            "source_mtime_ns": source_stat.st_mtime_ns,
            "master_sha256": master_sha,
            "result": "converted",
            "output_bytes": output.stat().st_size,
            "output_mtime_ns": output.stat().st_mtime_ns,
            "output_sha256": file_sha256(output),
        })
        legacy_stamp.unlink()
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
        if output.exists():
            output.unlink()
        if legacy_stamp.exists():
            legacy_stamp.unlink()
        write_cache(cache_path, {
            "format": CACHE_FORMAT,
            "source_sha256": source_sha,
            "source_bytes": source_stat.st_size,
            "source_mtime_ns": source_stat.st_mtime_ns,
            "master_sha256": master_sha,
            "result": "not-needed",
        })
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
    output_stat = output.stat()
    write_cache(cache_path, {
        "format": CACHE_FORMAT,
        "source_sha256": source_sha,
        "source_bytes": source_stat.st_size,
        "source_mtime_ns": source_stat.st_mtime_ns,
        "master_sha256": master_sha,
        "result": "converted",
        "output_bytes": len(rebuilt),
        "output_mtime_ns": output_stat.st_mtime_ns,
        "output_sha256": hashlib.sha256(rebuilt).hexdigest(),
    })
    if legacy_stamp.exists():
        legacy_stamp.unlink()
    return {"file": relative.as_posix(), "status": "converted", "textures": [x[0] for x in changed.values()], "bytes": len(rebuilt)}


def prepare_job(job):
    path, data_root, master, master_sha, previous_status, previous_report_mtime_ns = job
    try:
        return prepare(path, data_root, master, master_sha, previous_status, previous_report_mtime_ns)
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
        dlc_root = args.data_root / "dlc"
        if dlc_root.is_dir():
            paths.update(p.resolve() for p in dlc_root.rglob("*.xab"))
    for log in args.runtime_log:
        for match in re.finditer(r"\[UMO PC bundle\] (.+?\.xab) CABs=", log.read_text(encoding="utf-8-sig", errors="replace")):
            paths.add(Path(match.group(1)).resolve())
    report = []
    failures = 0
    output = args.data_root / "WindowsCache"
    previous_statuses = {}
    previous_report_mtime_ns = 0
    previous_report = output / "last-report.json"
    if previous_report.is_file():
        try:
            previous_rows = json.loads(previous_report.read_text(encoding="utf-8"))
            if (isinstance(previous_rows, list) and
                    all(isinstance(row, dict) and row.get("status") != "error" for row in previous_rows)):
                previous_statuses = {row["file"]: row["status"] for row in previous_rows
                                     if isinstance(row, dict) and "file" in row and "status" in row}
                previous_report_mtime_ns = previous_report.stat().st_mtime_ns
        except (OSError, ValueError, TypeError):
            pass
    master_sha = file_sha256(args.master)
    jobs = []
    for path in sorted(paths):
        relative = path.resolve().relative_to(args.data_root.resolve()).as_posix()
        jobs.append((path, args.data_root, args.master, master_sha,
                     previous_statuses.get(relative), previous_report_mtime_ns))
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
    output.mkdir(parents=True, exist_ok=True)
    (output / "last-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Done: bundles={len(paths)} failures={failures}", flush=True)
    raise SystemExit(1 if failures else 0)


if __name__ == "__main__":
    main()
