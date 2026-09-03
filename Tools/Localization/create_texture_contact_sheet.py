#!/usr/bin/env python3
"""Create a labeled checkerboard contact sheet for editable texture sprites."""
from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def checkerboard(size: tuple[int, int], step: int = 12) -> Image.Image:
    image = Image.new("RGB", size, (230, 230, 230))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], step):
        for x in range(0, size[0], step):
            if (x // step + y // step) % 2:
                draw.rectangle((x, y, x + step - 1, y + step - 1), fill=(190, 190, 190))
    return image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--columns", type=int, default=3)
    args = parser.parse_args()

    files = sorted(args.source.glob("*.png"))
    cell_width, cell_height = 440, 190
    rows = math.ceil(len(files) / args.columns)
    sheet = Image.new("RGB", (cell_width * args.columns, cell_height * rows), (35, 35, 35))
    font = ImageFont.truetype(r"C:\Windows\Fonts\malgun.ttf", 17)
    draw = ImageDraw.Draw(sheet)

    for index, path in enumerate(files):
        col, row = index % args.columns, index // args.columns
        left, top = col * cell_width, row * cell_height
        preview = checkerboard((cell_width - 20, cell_height - 48))
        sprite = Image.open(path).convert("RGBA")
        scale = min((preview.width - 12) / sprite.width, (preview.height - 12) / sprite.height, 3)
        sprite = sprite.resize((max(1, int(sprite.width * scale)), max(1, int(sprite.height * scale))))
        preview.paste(sprite, ((preview.width - sprite.width) // 2, (preview.height - sprite.height) // 2), sprite)
        sheet.paste(preview, (left + 10, top + 38))
        draw.text((left + 10, top + 10), path.name, font=font, fill="white")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.output)
    print(f"sprites={len(files)}")


if __name__ == "__main__":
    main()
