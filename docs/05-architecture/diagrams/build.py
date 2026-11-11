#!/usr/bin/env python3
"""Render an Archify diagram spec to a PNG that embeds in Markdown.

Archify's own output is a self-contained HTML page with an interactive SVG and
~300 KB of inlined fonts. That is the right artefact for a browser and the
wrong one for a document: GitHub will not render it inline, and the SVG alone
loses the CSS cascade it depends on (the theme variables live on <html>).

So this does what the rest of the repository already does for screenshots:
render, isolate the diagram in a minimal page that keeps the cascade, and
capture it at 2x. The JSON spec stays in the repository, so the picture is
regenerable rather than an artefact nobody can edit.

    python docs/05-architecture/diagrams/build.py docs/05-architecture/diagrams/01-architecture-baseline.architecture.json

Run from the repository root; source paths can point to any documentation folder.
Requires: the Archify checkout (set ARCHIFY to its directory) and Chrome.
"""
import json
import os
import re
import pathlib
import subprocess
import sys
import tempfile

ARCHIFY = pathlib.Path(
    os.environ.get("ARCHIFY", r"C://Users//tengt//AppData//Local//Temp//archify//archify")
)
CHROME = os.environ.get(
    "CHROME", r"C:\Program Files\Google\Chrome\Application\chrome.exe"
)
HERE = pathlib.Path(__file__).parent
SCALE = 2


def render(spec: pathlib.Path) -> pathlib.Path:
    """Run the Archify CLI and return the HTML it wrote.

    Renders into a temp directory, not the repository: the CLI writes through a
    hard link for atomic replacement, and refuses a target that already has
    more than one link name — which a checked-out repo can produce.
    """
    html = pathlib.Path(tempfile.mkdtemp(prefix="archify-")) / (spec.stem + ".html")
    dtype = json.loads(spec.read_text(encoding="utf-8"))["diagram_type"]
    subprocess.run(
        ["node", str(ARCHIFY / "bin" / "archify.mjs"), "render", dtype,
         str(spec), str(html), "--quality", "showcase"],
        check=True, capture_output=True,
    )
    return html


def cards_of(src: str) -> str:
    """Return the info-card block Archify renders beside the canvas, or ''.

    The cards are content, not chrome: they carry rules the arrows only point
    at ("a partial replay is 202 Unresolved: poll and reconcile"). Dropping
    them because they sit outside the <svg> loses half the figure.
    """
    start = src.find('<div class="cards">')
    if start < 0:
        return ""
    depth = 0
    for m in re.finditer(r"<(/?)div\b", src[start:]):
        depth += -1 if m.group(1) else 1
        if depth == 0:
            return src[start:start + m.end() + len("</div>")]
    return ""


def isolate(html: pathlib.Path) -> tuple[pathlib.Path, int, int]:
    """Extract the diagram and its cards into a page with no chrome."""
    src = html.read_text(encoding="utf-8", errors="replace")
    styles = re.findall(r"<style[^>]*>(.*?)</style>", src, re.S)
    svg = re.search(r"(<svg\b.*?</svg>)", src, re.S).group(1)
    cards = cards_of(src)

    view = re.search(r'viewBox="0 0 (\d+) (\d+)"', svg)
    w, h = (int(view.group(1)), int(view.group(2))) if view else (1020, 640)

    page = (
        '<!doctype html><html data-theme="light"><head><meta charset="utf-8">'
        + "".join(f"<style>{s}</style>" for s in styles)
        + f"<style>body{{margin:0;background:#fff;width:{w}px;box-sizing:border-box}}"
        + f"svg{{display:block;width:{w}px;height:auto}}"
        + ".cards{display:grid;grid-template-columns:1fr 1fr;gap:16px;"
        + "padding:20px 0 0;margin:0}</style>"
        + f"</head><body>{svg}{cards}</body></html>"
    )
    out = html.with_suffix(".isolated.html")
    out.write_text(page, encoding="utf-8")
    return out, w, h + (360 if cards else 0)


def shoot(page: pathlib.Path, png: pathlib.Path, w: int, h: int) -> None:
    subprocess.run(
        [CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
         f"--force-device-scale-factor={SCALE}", f"--window-size={w + 40},{h + 220}",
         "--virtual-time-budget=8000", f"--screenshot={png}", page.as_uri()],
        check=True, capture_output=True,
    )


def trim(png: pathlib.Path, margin: int = 24) -> None:
    """Crop the uniform white border the fixed-size window leaves behind."""
    from PIL import Image, ImageChops

    im = Image.open(png).convert("RGB")
    bg = Image.new("RGB", im.size, (255, 255, 255))
    box = ImageChops.difference(im, bg).getbbox()
    if not box:
        return
    l, t, r, b = box
    im.crop(
        (max(0, l - margin), max(0, t - margin),
         min(im.width, r + margin), min(im.height, b + margin))
    ).save(png)


def main(argv: list[str]) -> int:
    if not argv:
        print(__doc__)
        return 2
    for name in argv:
        spec = pathlib.Path(name)
        if not spec.is_absolute() and not spec.is_file():
            spec = HERE / spec
        if not spec.is_file():
            raise FileNotFoundError(f"Diagram source not found: {name}")
        spec = spec.resolve()
        html = render(spec)
        page, w, h = isolate(html)
        png = spec.with_suffix("").with_suffix(".png")
        shoot(page, png, w, h)
        trim(png)
        print(f"{png.name}  {png.stat().st_size // 1024} KB  ({w}x{h} at {SCALE}x)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
