"""Read-only inspection of encrypted UMO bundles for Windows compatibility."""
import argparse
import collections
import hashlib
import json
import re
from pathlib import Path

import UnityPy


def decrypt_bundle(path, master):
    data = path.read_bytes()
    if data.startswith(b"Unity"):
        return data
    entries = json.loads(master.read_text(encoding="utf-8-sig"))["master"]["s_ak"]["data"]
    entry = next(x for x in entries if re.search(x["f"], path.as_posix()))
    if entry["k"] == 0:
        return data
    seed = entry["k"]
    key = []
    for _ in range(1024):
        seed = (seed ^ (seed << 13)) & 0xffffffff
        seed ^= seed >> 17
        seed = (seed ^ (seed << 5)) & 0xffffffff
        key.append((seed >> 3) & 255)
    pos = len(data)
    result = bytearray(data)
    for i in range(len(result)):
        pos = (pos * 7 + 1) & 0xffffffff
        result[i] ^= key[pos % 1024]
    if not result.startswith(b"Unity"):
        raise ValueError("Bundle decryption failed: " + str(path))
    return bytes(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="+", type=Path)
    parser.add_argument("--master", type=Path, default=Path(__file__).resolve().parents[2] / "Data/RequestMaster.json")
    args = parser.parse_args()
    for path in args.files:
        data = decrypt_bundle(path, args.master)
        print("FILE", path, "bytes", len(data), "sha256", hashlib.sha256(data).hexdigest())
        print("RAW VERSION MARKERS", [(m.start(), data[m.start():m.start()+24].hex()) for m in re.finditer(rb"2018\.", data)])
        env = UnityPy.load(data)
        for name, bundle in env.files.items():
            print("BUNDLE", name, "flags", getattr(bundle, "dataflags", None))
        for asset in env.assets:
            print("CAB", asset.name, "version", asset.unity_version, "platform", asset.target_platform, "type_tree", asset._enable_type_tree)
            print("TYPES", dict(collections.Counter(o.type.name for o in asset.objects.values())))
        for obj in env.objects:
            try:
                tree = obj.read_typetree()
                name = tree.get("m_Name", "")
                extra = {k: tree[k] for k in ("m_AssemblyName", "m_ClassName", "m_NameSpace") if k in tree}
                print("OBJECT", obj.path_id, obj.type.name, obj.byte_size, name, extra)
            except Exception as e:
                print("OBJECT ERROR", obj.path_id, obj.type.name, str(e))


if __name__ == "__main__":
    main()
