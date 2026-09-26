"""Verify that optional DLC conversion survives OFF -> ON directory renaming."""
from pathlib import Path
import tempfile
import zipfile

from prepare_texture_cache import prepare


def main():
    repo = Path(__file__).resolve().parents[2]
    # Keep test artifacts local to this project, like the game's data.
    output = repo / "outputs"
    output.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="yami-cache-test-", dir=output) as temporary:
        root = Path(temporary)
        disabled = root / "dlc" / "_yamiQray_freyja"
        with zipfile.ZipFile(repo / "Unity/Assets/Resources/BundledDlc/yamiQray_freyja_1_Android.bytes") as archive:
            archive.extractall(disabled)
        relative = Path("bundles/ct/dv/co/01_080.xab")
        master = repo / "Data/RequestMaster.json"
        first = prepare(disabled / relative, root, master)
        assert first["status"] == "converted", first
        canonical = root / "WindowsCache/dlc/yamiQray_freyja" / relative
        assert canonical.is_file()
        before = canonical.read_bytes()
        enabled = root / "dlc/yamiQray_freyja"
        disabled.rename(enabled)
        second = prepare(enabled / relative, root, master)
        assert second["status"] == "cached", second
        assert canonical.read_bytes() == before
        print("YamiQray cache test passed: OFF conversion reused after ON toggle.")


if __name__ == "__main__":
    main()
