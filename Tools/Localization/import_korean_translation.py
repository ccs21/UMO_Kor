#!/usr/bin/env python3
"""Create Korean PO sources and a game-translation-ko compatible ko.bytes archive.

The archive layout intentionally mirrors DatabaseTextConverter.GenerateGameFiles2.
It starts from the shipped Korean DLC archive so non-catalog/English entries retain
their existing values, then replaces only catalog keys supplied by the translator.
"""
from __future__ import annotations

import argparse
import ast
import io
import shutil
import struct
from collections import OrderedDict, defaultdict
from pathlib import Path

import pandas as pd


SHEET_FILES = {
    "common": "message_common_jp_00000000.bytes",
    "menu": "message_menu_jp_00000000.bytes",
    "master": "message_master_jp_00000000.bytes",
    "master_scene": "message_master_jp_00000000.bytes",
    "master_sns": "message_master_jp_00000000.bytes",
    **{f"diva{i:03}": f"message_diva{i:03}_jp_00000000.bytes" for i in range(1, 11)},
}
SPECIAL_FILES = {
    name: f"{name}.bytes"
    for name in (
        "snsDb_text", "tipsDb_text", "vcItemDb_text", "tutoPictDb_text",
        "tutoMiniAdvDb_text", "anketoDb_text", "helpBrowserDb_text", "homeBgDb_text",
        "shopDb_text", "room_text", "music_text", "adv_text", "bingo_text",
        "events_text", "string_literals",
    )
}
ARCHIVE_FILE = {**SHEET_FILES, **SPECIAL_FILES}


def unquote(value: str) -> str:
    return ast.literal_eval(value.strip())


def parse_po(path: Path) -> OrderedDict[str, str]:
    out: OrderedDict[str, str] = OrderedDict()
    key = value = active = None
    def flush() -> None:
        nonlocal key, value, active
        if key is not None and value is not None and "".join(key):
            out["".join(key)] = "".join(value)
        key = value = active = None
    for raw in path.read_text(encoding="utf-8-sig").splitlines():
        if not raw:
            flush()
        elif raw.startswith("msgid "):
            if key is not None:
                flush()
            key = [unquote(raw[6:])]
            active = key
        elif raw.startswith("msgstr "):
            value = [unquote(raw[7:])]
            active = value
        elif raw.startswith('"') and active is not None:
            active.append(unquote(raw))
    flush()
    return out


def po_quote(value: str) -> str:
    return value.replace("\\", "\\\\").replace("\r", "\\r").replace("\n", "\\n").replace('"', '\\"')


def write_po(path: Path, source: OrderedDict[str, str], translations: dict[str, str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        'msgid ""', 'msgstr ""', '"Language: ko\\n"',
        '"Content-Type: text/plain; charset=UTF-8\\n"', '',
    ]
    for key in source:
        lines += [f'msgid "{po_quote(key)}"', f'msgstr "{po_quote(translations.get(key, ""))}"', '']
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


def decode_bank(data: bytes) -> OrderedDict[str, str]:
    count, = struct.unpack_from("<i", data, 0)
    result: OrderedDict[str, str] = OrderedDict()
    for i in range(count):
        value_off, value_len, key_off, key_len = struct.unpack_from("<iiii", data, 16 + 16 * i)
        value = data[value_off:value_off + value_len].decode("utf-16le")
        key = data[key_off:key_off + key_len].decode("utf-16le")
        result[key] = value
    return result


def encode_bank(values: OrderedDict[str, str]) -> bytes:
    count = len(values)
    stream = io.BytesIO()
    stream.write(struct.pack("<i", count) + b"\xff" * 12)
    stream.write(b"\0" * (16 * count))
    info = 16
    offset = 16 + 16 * count
    for key, value in values.items():
        for text in (value, key):
            raw = text.encode("utf-16le")
            stream.seek(0, io.SEEK_END)
            stream.write(raw)
            stream.seek(info)
            stream.write(struct.pack("<ii", offset, len(raw)))
            info += 8
            offset += len(raw)
    return stream.getvalue()


def read_csharp_tar(path: Path) -> OrderedDict[str, bytes]:
    raw = path.read_bytes()
    out: OrderedDict[str, bytes] = OrderedDict()
    pos = 0
    while pos + 512 <= len(raw):
        header = raw[pos:pos + 512]
        name = header[:100].split(b"\0", 1)[0].decode("utf-8")
        if not name:
            break
        size_text = header[124:136].split(b"\0", 1)[0].decode("ascii").strip() or "0"
        size = int(size_text, 8)
        start = pos + 512
        out[name] = raw[start:start + size]
        pos = start + ((size + 511) // 512) * 512
    return out


def write_csharp_tar(path: Path, files: OrderedDict[str, bytes]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as stream:
        for name, payload in files.items():
            header = bytearray(512)
            name_bytes = ("./" + name.lstrip("./")).encode("utf-8")[:100]
            header[:len(name_bytes)] = name_bytes
            header[100:107] = f"{511:07o}".encode("ascii")
            header[108:115] = f"{61:07o}".encode("ascii")
            header[116:123] = f"{61:07o}".encode("ascii")
            header[124:135] = f"{len(payload):011o}".encode("ascii")
            header[136:147] = f"{0:011o}".encode("ascii")
            header[156] = ord("0")
            header[148:156] = b" " * 8
            checksum = sum(header)
            header[148:154] = f"{checksum:06o}".encode("ascii")
            stream.write(header)
            stream.write(payload)
            stream.write(b"\0" * ((-len(payload)) % 512))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("xlsx", type=Path)
    parser.add_argument("translation_repo", type=Path)
    parser.add_argument("base_archive", type=Path)
    parser.add_argument("output_dir", type=Path)
    args = parser.parse_args()
    df = pd.read_excel(args.xlsx, sheet_name=0, dtype=str).fillna("")
    required = {"component", "key", "new_korean", "source_file"}
    if not required <= set(df.columns):
        raise ValueError(f"Missing columns: {sorted(required - set(df.columns))}")
    if df.duplicated(["component", "key"]).any():
        raise ValueError("Duplicate component/key rows in workbook")

    by_component: dict[str, dict[str, str]] = defaultdict(dict)
    for row in df.to_dict(orient="records"):
        by_component[row["component"]][row["key"]] = row["new_korean"]

    po_root = args.output_dir / "Localization"
    for component, values in by_component.items():
        source_file = next(row["source_file"] for row in df.to_dict(orient="records") if row["component"] == component)
        jp_path = args.translation_repo / source_file
        target = po_root / source_file.replace("jp.po", "ko.po")
        write_po(target, parse_po(jp_path), values)

    archive = read_csharp_tar(args.base_archive)
    updates: dict[str, dict[str, str]] = defaultdict(dict)
    skipped_assets = 0
    for component, values in by_component.items():
        if component == "Assets":
            skipped_assets += len(values)
            continue
        try:
            target_file = ARCHIVE_FILE[component]
        except KeyError as exc:
            raise ValueError(f"Unknown runtime component {component!r}") from exc
        updates[target_file].update(values)

    missing: list[str] = []
    updated = 0
    for target_file, translations in updates.items():
        archive_name = "./" + target_file
        if archive_name not in archive:
            raise ValueError(f"Base archive missing {archive_name}")
        bank = decode_bank(archive[archive_name])
        for key, text in translations.items():
            if key not in bank:
                missing.append(f"{target_file}:{key}")
            else:
                bank[key] = text
                updated += 1
        archive[archive_name] = encode_bank(bank)
    if missing:
        raise ValueError(f"{len(missing)} workbook keys not found in runtime banks; first: {missing[:10]}")

    out_archive = args.output_dir / "translation" / "ko.bytes"
    write_csharp_tar(out_archive, archive)
    shutil.copy2(args.base_archive.parent.parent / "dlc.json", args.output_dir / "dlc.json")
    (args.output_dir / "build_summary.txt").write_text(
        f"workbook_rows={len(df)}\nupdated_runtime_keys={updated}\nskipped_assets_keys={skipped_assets}\narchive_files={len(archive)}\n",
        encoding="utf-8",
    )
    print((args.output_dir / "build_summary.txt").read_text(encoding="utf-8"), end="")


if __name__ == "__main__":
    main()
