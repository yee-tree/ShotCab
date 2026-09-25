"""Render the project SVG into a multi-size Windows icon and a preview PNG."""

from pathlib import Path
from struct import pack

import cairosvg


ROOT = Path(__file__).resolve().parents[1]
SVG = ROOT / "assets" / "ShotCab-icon.svg"
ICON = ROOT / "assets" / "ShotCab-icon.ico"
PREVIEW = ROOT / "assets" / "ShotCab-icon-preview.png"
SIZES = (16, 24, 32, 48, 64, 128, 256)

images = [cairosvg.svg2png(url=str(SVG), output_width=size, output_height=size) for size in SIZES]
header = pack("<HHH", 0, 1, len(images))
offset = len(header) + 16 * len(images)
entries = []
for size, image in zip(SIZES, images):
    entries.append(pack("<BBBBHHII", size if size < 256 else 0, size if size < 256 else 0,
                        0, 0, 1, 32, len(image), offset))
    offset += len(image)

ICON.write_bytes(header + b"".join(entries) + b"".join(images))
PREVIEW.write_bytes(images[-1])
print(f"{ICON} ({ICON.stat().st_size} bytes)")
