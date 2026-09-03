"""Verify the C# converter changes only CAB target fields, not object/type data."""
import base64
import argparse
import json
import shutil
import subprocess
from pathlib import Path

import UnityPy
from inspect_bundle import decrypt_bundle

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--data-root", type=Path, default=root / "Unity/Build/Windows/UMO_Kor/Data/android")
args = parser.parse_args()
data_root = args.data_root
fixtures = sorted((data_root / "ly").glob("*.xab"))[:12]
if (data_root / "ly/046.xab").exists():
    fixtures.append(data_root / "ly/046.xab")
assert fixtures, "No test fixture bundles found"
payloads = [decrypt_bundle(path, root / "Data/RequestMaster.json") for path in fixtures]
inputs = payloads + [b"not a bundle", payloads[-1][:-1]]
cmd = [shutil.which("pwsh") or "powershell", "-NoProfile", "-File", str(Path(__file__).with_name("run_bundle_converter.ps1"))]
run = subprocess.run(cmd, input="\n".join(base64.b64encode(p).decode() for p in inputs) + "\n", capture_output=True, text=True, check=True)
results = [json.loads(line) for line in run.stdout.splitlines()]
assert len(results) == len(inputs), run.stderr
converted_payloads = []
for path, original, result in zip(fixtures, payloads, results):
    assert "error" not in result, result
    converted = base64.b64decode(result["data"])
    before, after = UnityPy.load(original), UnityPy.load(converted)
    assert len(before.assets) == len(after.assets)
    for old, new in zip(before.assets, after.assets):
        assert old.name == new.name
        assert new.target_platform == 19
        old_bytes, new_bytes = old.reader.bytes, new.reader.bytes
        assert len(old_bytes) == len(new_bytes)
        changes = [(i, a, b) for i, (a, b) in enumerate(zip(old_bytes, new_bytes)) if a != b]
        assert len(changes) == 1 and changes[0][1:] == (13, 19), changes
        assert old.objects.keys() == new.objects.keys()
        for oid in old.objects:
            assert old.objects[oid].get_raw_data() == new.objects[oid].get_raw_data()
    converted_payloads.append(converted)
    print("PASS", path.name, "CABs", result["count"], "objects", len(after.objects))
assert all("error" in r for r in results[-2:]), "Malformed inputs must be rejected"
# Already-converted bundles must pass through byte-for-byte unchanged.
rerun = subprocess.run(cmd, input="\n".join(base64.b64encode(p).decode() for p in converted_payloads) + "\n", capture_output=True, text=True, check=True)
for old, line in zip(converted_payloads, rerun.stdout.splitlines()):
    result = json.loads(line)
    assert result["count"] == 0 and base64.b64decode(result["data"]) == old
print("PASS malformed-input rejection and idempotence")
