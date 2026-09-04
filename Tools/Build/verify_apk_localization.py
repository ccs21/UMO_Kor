"""Check the APK's embedded Korean TextAsset against the committed language pack."""
import argparse
import hashlib
from pathlib import Path
from zipfile import ZipFile
import UnityPy

parser = argparse.ArgumentParser()
parser.add_argument("apk", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
expected = (root / "Unity/Assets/Resources/Localizations/Database/ko.bytes").read_bytes()
matches = []
with ZipFile(args.apk) as archive:
    for name in archive.namelist():
        if name.startswith("assets/bin/Data/") and name.endswith(".assets"):
            env = UnityPy.load(archive.read(name))
            for obj in env.objects:
                if obj.type.name != "TextAsset":
                    continue
                data = obj.read()
                if data.m_Name != "ko":
                    continue
                content = data.m_Script
                if isinstance(content, str):
                    content = content.encode("utf-8", "surrogateescape")
                if content == expected:
                    matches.append((name, obj.path_id))
assert matches, "APK does not contain the current Korean language pack"
print("PASS: APK embedded Korean pack matches source. SHA256=" + hashlib.sha256(expected).hexdigest())
print(matches)
