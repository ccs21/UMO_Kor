"""Apply nonempty revised translations, preserving unrelated banks and blank cells."""
import argparse
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path
import pandas as pd
from import_korean_translation import parse_po, write_po, read_csharp_tar, write_csharp_tar, decode_bank, encode_bank, normalize_translated_text, ARCHIVE_FILE

parser = argparse.ArgumentParser()
parser.add_argument("workbook", type=Path)
parser.add_argument("--apply", action="store_true")
parser.add_argument("--report", type=Path, help="Write the comparison report here without overwriting an earlier revision report")
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
df = pd.read_excel(args.workbook, dtype=str).fillna("")
assert {"component", "key", "new_korean", "source_file", "japanese"} <= set(df.columns)
assert not df.duplicated(["component", "key"]).any(), "Duplicate keys"
archive_path = root / "Unity/Assets/Resources/Localizations/Database/ko.bytes"
archive = read_csharp_tar(archive_path)
banks, catalogs, changes, warnings = {}, {}, [], []
changed_po = set()
for row in df.to_dict("records"):
    text = normalize_translated_text(row["new_korean"])
    if not text.strip():
        continue
    po = (root / "Localization" / row["source_file"].replace("jp.po", "ko.po")).resolve()
    assert po.is_relative_to(root / "Localization") and po.is_file(), po
    if po not in catalogs:
        catalogs[po] = parse_po(po)
    key, component = row["key"], row["component"]
    assert key in catalogs[po], (component, key)
    old = catalogs[po][key]
    if text == old:
        continue
    tokens = lambda s: sorted(re.findall(r"(?<!\{)\{\d+(?:[^{}]*)\}(?!\})", s))
    if tokens(row["japanese"]) != tokens(text):
        warnings.append({"component": component, "key": key, "source": row["japanese"], "new": text})
    changes.append({"component": component, "key": key, "old": old, "new": text})
    catalogs[po][key] = text
    changed_po.add(po)
    if component != "Assets":
        name = next(n for n in archive if n.lstrip("./") == ARCHIVE_FILE[component])
        if name not in banks:
            banks[name] = decode_bank(archive[name])
        assert key in banks[name], (component, key)
        banks[name][key] = text
report = args.report or root.parent / "outputs/translation-revision-report.json"
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({"rows": len(df), "sha256": hashlib.sha256(args.workbook.read_bytes()).hexdigest(), "changes": changes, "placeholder_warnings": warnings}, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"rows": len(df), "changes": len(changes), "components": dict(pd.Series([c['component'] for c in changes]).value_counts().items()), "placeholder_warnings": len(warnings)}, ensure_ascii=True, default=int))
assert not warnings, "Review changed placeholder mismatches before applying; see report"
if args.apply:
    for po, values in catalogs.items():
        if po in changed_po:
            write_po(po, values, values)
    for name, bank in banks.items():
        archive[name] = encode_bank(bank)
    write_csharp_tar(archive_path, archive)
    assert read_csharp_tar(archive_path) == archive
    for name, bank in banks.items():
        assert decode_bank(archive[name]) == bank
    print("Applied and verified. Workbook unchanged; blank entries preserved.")
