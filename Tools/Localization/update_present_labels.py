"""Regenerate only the two reviewed present-date labels in the bundled archive."""
from pathlib import Path
from import_korean_translation import read_csharp_tar, write_csharp_tar, decode_bank, encode_bank, parse_po

root = Path(__file__).resolve().parents[2]
path = root / "Unity/Assets/Resources/Localizations/Database/ko.bytes"
archive = read_csharp_tar(path)
name = next(k for k in archive if k.endswith("message_menu_jp_00000000.bytes"))
bank = decode_bank(archive[name])
po = parse_po(root / "Localization/Database/menu/po/ko.po")
for key in ("pbox_text_10", "pbox_text_11"):
    assert key in bank and all("{" + str(n) + "}" in po[key] for n in range(5))
    bank[key] = po[key]
archive[name] = encode_bank(bank)
write_csharp_tar(path, archive)
assert read_csharp_tar(path) == archive
print("Verified: two present-date labels regenerated; other archive entries preserved.")
