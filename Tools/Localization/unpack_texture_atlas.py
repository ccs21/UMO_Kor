#!/usr/bin/env python3
"""Split a UMO base/mask texture atlas into editable transparent PNG sprites."""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("base", type=Path)
    parser.add_argument("mask", type=Path)
    parser.add_argument("uvlist", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    base = Image.open(args.base).convert("RGBA")
    mask = Image.open(args.mask).convert("RGB")
    lines = args.uvlist.read_text(encoding="utf-8-sig").splitlines()
    _, atlas_width, atlas_height = lines[0].split(",")
    width, height = int(atlas_width), int(atlas_height)
    if base.size != (width, height) or mask.size != (width, height):
        raise ValueError(f"Atlas size mismatch: uv={(width, height)}, base={base.size}, mask={mask.size}")

    args.output.mkdir(parents=True, exist_ok=True)
    for line in lines[1:]:
        if not line:
            continue
        name, x, y, sprite_width, sprite_height = line.split(",")
        x, y = int(x), int(y)
        sprite_width, sprite_height = int(sprite_width), int(sprite_height)
        top = height - y - sprite_height
        box = (x, top, x + sprite_width, top + sprite_height)
        sprite = base.crop(box)
        alpha = mask.crop(box).getchannel("R")
        sprite.putalpha(alpha)
        sprite.save(args.output / f"{name}.png")

    print(f"sprites={len(lines) - 1}")


if __name__ == "__main__":
    main()
