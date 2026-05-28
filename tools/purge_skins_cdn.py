"""
Check each skin on the jsdelivr CDN and purge any that still have the broken
(too-high) placement. The fix moved skin center-y from ~249 to ~304, so any
image whose non-transparent pixel center is above Y=270 is stale.
"""

import argparse
import io
import json
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from PIL import Image

CDN   = "https://cdn.jsdelivr.net/gh/OhMyGuus/BetterCrewLink-Hats@master/NONE/"
PURGE = "https://purge.jsdelivr.net/gh/OhMyGuus/BetterCrewLink-Hats@master/NONE/"
# Skins with center-y below this threshold are the old broken version.
FIXED_THRESHOLD = 270


def skin_filenames(hats_json: str) -> list[str]:
    with open(hats_json) as f:
        d = json.load(f)
    return sorted(
        v["image"]
        for v in d["NONE"]["hats"].values()
        if v.get("hat_type") == "skins" and "image" in v
    )


def center_y(img_bytes: bytes) -> float:
    img = Image.open(io.BytesIO(img_bytes)).convert("RGBA")
    pixels = img.load()
    w, h = img.size
    rows = [y for y in range(h) for x in range(w) if pixels[x, y][3] > 10]
    if not rows:
        return 0.0
    return (min(rows) + max(rows)) / 2


def fetch(url: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "cdn-purge-tool/1.0"})
    with urllib.request.urlopen(req, timeout=15) as r:
        return r.read()


def purge(filename: str) -> int:
    req = urllib.request.Request(
        PURGE + filename, headers={"User-Agent": "cdn-purge-tool/1.0"}
    )
    with urllib.request.urlopen(req, timeout=15) as r:
        return r.status


def check_and_purge(filename: str) -> tuple[str, str]:
    try:
        data = fetch(CDN + filename)
        cy = center_y(data)
        if cy >= FIXED_THRESHOLD:
            return filename, f"ok (cy={cy:.0f})"
        status = purge(filename)
        return filename, f"PURGED (cy={cy:.0f}, purge_status={status})"
    except Exception as e:
        return filename, f"ERROR: {e}"


def main():
    parser = argparse.ArgumentParser(description="Purge stale skin PNGs from the jsDelivr CDN.")
    parser.add_argument(
        "--hats-json",
        default="../BetterCrewLink-Hats/hats_formatted.json",
        help="Path to hats_formatted.json (default: ../BetterCrewLink-Hats/hats_formatted.json)",
    )
    args = parser.parse_args()

    filenames = skin_filenames(args.hats_json)
    print(f"Checking {len(filenames)} skins...\n")

    ok = purged = errors = 0
    with ThreadPoolExecutor(max_workers=16) as pool:
        futures = {pool.submit(check_and_purge, f): f for f in filenames}
        for fut in as_completed(futures):
            name, result = fut.result()
            print(f"  {name}: {result}")
            if result.startswith("PURGED"):
                purged += 1
            elif result.startswith("ERROR"):
                errors += 1
            else:
                ok += 1

    print(f"\nDone. ok={ok}  purged={purged}  errors={errors}")


if __name__ == "__main__":
    main()
