"""Extract the original prologue atlases for translation review (no game writes)."""
import argparse
from pathlib import Path
import UnityPy
from inspect_bundle import decrypt_bundle

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("bundle", type=Path)
parser.add_argument("output", type=Path)
parser.add_argument("--master", type=Path, default=Path(__file__).resolve().parents[2] / "Data/RequestMaster.json")
args = parser.parse_args()
env = UnityPy.load(decrypt_bundle(args.bundle, args.master))
args.output.mkdir(parents=True, exist_ok=True)
for obj in env.objects:
    if obj.type.name != "Texture2D":
        continue
    texture = obj.read()
    if not texture.m_Name.startswith("cmn_tuto"):
        continue
    target = args.output / (texture.m_Name + ".png")
    if target.exists():
        raise FileExistsError(target)
    texture.image.save(target)
    print(target)
