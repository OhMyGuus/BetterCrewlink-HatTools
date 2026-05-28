# BetterCrewLink-HatTools

Exports Among Us hat, skin, and visor sprites from Unity Addressables bundles into the [BetterCrewLink-Hats](https://github.com/OhMyGuus/BetterCrewLink-Hats) format — a directory of 270×428 px PNGs and a `hats_formatted.json` / `hats.json` metadata file. The exporter reads any existing JSON at the output path and only replaces the `NONE` section, preserving all other mod sections.

## Download

Grab the latest self-contained binary for your platform from [Releases](https://github.com/OhMyGuus/BetterCrewlink-HatTools/releases/latest):

| Platform | File |
|---|---|
| Linux x64 | `hat-exporter-linux-x64` |
| Windows x64 | `hat-exporter-win-x64.exe` |

No .NET installation required — the binary is self-contained.

## Usage

```
hat-exporter [options]

Options:
  -b, --bundle-dir <path>   Path to the Among Us Addressables bundle directory
                            (default: ../among-us-game-data/Among Us_Data/StreamingAssets/aa/Steam/StandaloneWindows)
  -o, --output-dir <path>   Output directory for exported PNGs and JSON
                            (default: ../BetterCrewLink-Hats)
  --version                 Show version information
  -?, -h, --help            Show help and usage information
```

### Example

```bash
# Linux
chmod +x hat-exporter-linux-x64
./hat-exporter-linux-x64 \
  --bundle-dir "/path/to/Among Us/Among Us_Data/StreamingAssets/aa/Steam/StandaloneWindows" \
  --output-dir ./BetterCrewLink-Hats

# Windows
hat-exporter-win-x64.exe ^
  --bundle-dir "C:\Program Files (x86)\Steam\steamapps\common\Among Us\Among Us_Data\StreamingAssets\aa\Steam\StandaloneWindows" ^
  --output-dir .\BetterCrewLink-Hats
```

## Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/OhMyGuus/BetterCrewlink-HatTools.git
cd BetterCrewlink-HatTools
dotnet run --project HatsExporter -- --bundle-dir <path> --output-dir <path>
```

## Output format

```
<output-dir>/
  NONE/
    *.png              # Every image is exactly 270×428 px (full player canvas)
  hats_formatted.json  # Indented JSON — all mod sections
  hats.json            # Minified JSON — same content
```

Each hat entry in `hats_formatted.json`:

```jsonc
"hat_SomeHat": {
  "hat_type": "hats",          // "hats" | "skins" | "visors"
  "asset_name": "SomeHatHat",  // internal Unity asset name
  "image": "SomeHat.png",      // front/main sprite (omitted for back-only hats)
  "back_image": "SomeHat.png", // back sprite (omitted when absent)
  "multi_color": true          // only present when the hat uses player color
}
```

## Using in GitHub Actions (BetterCrewLink-Hats)

```yaml
- name: Download hat-exporter
  run: |
    curl -fsSL https://github.com/OhMyGuus/BetterCrewlink-HatTools/releases/latest/download/hat-exporter-linux-x64 \
      -o hat-exporter
    chmod +x hat-exporter

- name: Run hat-exporter
  run: ./hat-exporter --bundle-dir "$BUNDLE_DIR" --output-dir .
```

## Tools

[`tools/purge_skins_cdn.py`](tools/purge_skins_cdn.py) — checks each skin PNG on the jsDelivr CDN and purges stale cached files. Requires Python 3 and `Pillow`.

## License

[MIT](LICENSE)
