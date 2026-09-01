#!/usr/bin/env python3
"""Export UMO gettext sources into a stable translation catalog.

The generated JSON preserves real line breaks for workbook generation.  The TSV
uses escaped control characters so one catalog entry always occupies one line.
"""

from __future__ import annotations

import argparse
import ast
import csv
import json
import re
import subprocess
from pathlib import Path


JAPANESE_RE = re.compile(r"[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]")
KOREAN_RE = re.compile(r"[\uac00-\ud7a3\u1100-\u11ff\u3130-\u318f]")


def unquote_po(text: str) -> str:
    return ast.literal_eval(text.strip())


def parse_po(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    msgid: list[str] | None = None
    msgstr: list[str] | None = None
    active: list[str] | None = None

    def flush() -> None:
        nonlocal msgid, msgstr, active
        if msgid is not None and msgstr is not None:
            key = "".join(msgid)
            if key:
                if key in result:
                    raise ValueError(f"Duplicate msgid {key!r} in {path}")
                result[key] = "".join(msgstr)
        msgid = None
        msgstr = None
        active = None

    with path.open("r", encoding="utf-8-sig", newline=None) as stream:
        for raw_line in stream:
            line = raw_line.rstrip("\r\n")
            if not line:
                flush()
            elif line.startswith("msgid "):
                if msgid is not None:
                    flush()
                msgid = [unquote_po(line[6:])]
                active = msgid
            elif line.startswith("msgstr "):
                msgstr = [unquote_po(line[7:])]
                active = msgstr
            elif line.startswith('"') and active is not None:
                active.append(unquote_po(line))
        flush()
    return result


def escape_tsv(value: str) -> str:
    return value.replace("\\", "\\\\").replace("\r", "\\r").replace("\n", "\\n").replace("\t", "\\t")


def component_name(jp_path: Path, root: Path) -> str:
    relative = jp_path.relative_to(root)
    if relative.parts[0] == "JpLiteralStrings":
        return "string_literals"
    if relative.parts[0] == "Database":
        return relative.parts[1]
    return "/".join(relative.parts[:-2])


def build_catalog(root: Path) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    seen: set[tuple[str, str]] = set()

    jp_files = sorted(root.glob("**/po/jp.po"))
    if not jp_files:
        raise FileNotFoundError(f"No **/po/jp.po files found below {root}")

    for jp_path in jp_files:
        component = component_name(jp_path, root)
        ko_path = jp_path.with_name("ko.po")
        japanese = parse_po(jp_path)
        korean = parse_po(ko_path) if ko_path.exists() else {}

        for key, source in japanese.items():
            identity = (component, key)
            if identity in seen:
                raise ValueError(f"Duplicate catalog identity: {identity}")
            seen.add(identity)

            existing = korean.get(key, "")
            has_japanese = bool(JAPANESE_RE.search(source))
            has_korean = bool(existing and KOREAN_RE.search(existing))
            if not has_japanese:
                status = "영어/기호 유지"
            elif existing:
                status = "기존 번역 검수"
            else:
                status = "번역 필요"

            rows.append(
                {
                    "component": component,
                    "key": key,
                    "japanese": source,
                    "existing_korean": existing,
                    "new_korean": existing,
                    "status": status,
                    "has_japanese": has_japanese,
                    "has_existing_korean": bool(existing),
                    "has_korean_script": has_korean,
                    "source_file": jp_path.relative_to(root).as_posix(),
                }
            )
    return rows


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("translation_repo", type=Path)
    parser.add_argument("output_dir", type=Path)
    args = parser.parse_args()

    root = args.translation_repo.resolve()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    rows = build_catalog(root)

    json_path = output_dir / "umo_translation_catalog.json"
    json_path.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")

    columns = ["component", "key", "japanese", "existing_korean", "new_korean", "status", "source_file"]
    tsv_path = output_dir / "umo_translation_catalog.tsv"
    with tsv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream, delimiter="\t", lineterminator="\n", quoting=csv.QUOTE_MINIMAL)
        writer.writerow(columns)
        for row in rows:
            writer.writerow([escape_tsv(str(row[column])) for column in columns])

    japanese_tsv_path = output_dir / "umo_japanese_translation_required.tsv"
    with japanese_tsv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream, delimiter="\t", lineterminator="\n", quoting=csv.QUOTE_MINIMAL)
        writer.writerow(columns)
        for row in rows:
            if row["has_japanese"]:
                writer.writerow([escape_tsv(str(row[column])) for column in columns])

    source_commit = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()

    summary = {
        "total": len(rows),
        "contains_japanese": sum(bool(row["has_japanese"]) for row in rows),
        "english_or_symbols_only": sum(not bool(row["has_japanese"]) for row in rows),
        "existing_korean_nonempty": sum(bool(row["has_existing_korean"]) for row in rows),
        "translation_required": sum(row["status"] == "번역 필요" for row in rows),
        "components": len({str(row["component"]) for row in rows}),
        "source_commit": source_commit,
    }
    (output_dir / "umo_translation_summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
