using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Text.Json.Nodes;

namespace BetterCrewLink.HatTools;

static class Exporter
{
    public static void Run(string bundleDir, string outputDir)
    {
        Directory.CreateDirectory(Path.Combine(outputDir, "NONE"));

        Console.WriteLine("Parsing catalog...");
        var catalog = CatalogReader.Load(Path.Combine(bundleDir, "..", "..", "catalog.json"));

        var loader = new BundleLoader(bundleDir);

        Console.WriteLine("Loading referencedatagroup bundle...");
        var refBundlePath = Directory.GetFiles(bundleDir, "referencedatagroup_assets_all_*.bundle").First();
        var refInst = loader.LoadFromPath(refBundlePath);
        Console.WriteLine($"MonoBehaviours in referencedatagroup: {refInst.file.GetAssetsOfType(AssetClassID.MonoBehaviour).Count}");

        var entries = CosmeticCollector.Collect(loader, refInst, catalog.GuidToViewData);

        var spriteExporter = new SpriteExporter(loader);
        var noneDir      = Path.Combine(outputDir, "NONE");
        var writtenPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var jsonHats     = new JsonObject();
        int processed = 0, errors = 0;

        foreach (var entry in entries)
        {
            try
            {
                var hvdInst = loader.Load(entry.BundlePath);
                var addressMap = loader.GetAddressMap(hvdInst);
                if (!addressMap.TryGetValue(entry.ViewDataAddress, out var hvdPathId))
                {
                    errors++;
                    continue;
                }

                var hvdField = loader.Manager.GetBaseField(hvdInst, hvdInst.file.GetAssetInfo(hvdPathId));

                var (imagePng, imagePtr, backImagePng, backImagePtr) =
                    ResolveSprites(loader, hvdInst, hvdField, entry);

                WriteSpritePng(spriteExporter, hvdInst, imagePtr,     imagePng,     entry, "image",      noneDir, writtenPaths);
                WriteSpritePng(spriteExporter, hvdInst, backImagePtr, backImagePng, entry, "back_image",  noneDir, writtenPaths);

                jsonHats[entry.ProductId] = BuildJsonEntry(entry, hvdField, imagePng, backImagePng);

                processed++;
                if (processed % 50 == 0) Console.WriteLine($"  {processed}/{entries.Count}...");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR {entry.ProductId}: {ex.Message}");
                errors++;
            }
        }

        Console.WriteLine($"Done. Processed: {processed}, Errors: {errors}");
        HatsJsonWriter.Write(outputDir, jsonHats);
    }

    // Determines which sprite pointer maps to image vs back_image according to layer logic.
    static (string? imagePng, AssetTypeValueField? imagePtr,
            string? backImagePng, AssetTypeValueField? backImagePtr)
    ResolveSprites(BundleLoader loader, AssetsFileInstance hvdInst,
                   AssetTypeValueField hvdField, CosmeticEntry entry)
    {
        if (entry.HatType != "hats")
        {
            var ptr = hvdField["IdleFrame"];
            bool has = ptr["m_PathID"].AsLong != 0;
            return (has ? entry.AssetName + ".png" : null, has ? ptr : null, null, null);
        }

        var mainPtr = hvdField["MainImage"];
        var backPtr = hvdField["BackImage"];
        bool hasMain = mainPtr["m_PathID"].AsLong != 0;
        bool hasBack = backPtr["m_PathID"].AsLong != 0;

        if (entry.InFront)
        {
            return (hasMain ? entry.AssetName + ".png" : null, hasMain ? mainPtr : null, null, null);
        }

        if (hasBack)
        {
            var backSprite = loader.Manager.GetExtAsset(hvdInst, backPtr);
            string? backPng = backSprite.baseField != null
                ? backSprite.baseField["m_Name"].AsString + ".png"
                : null;
            return (hasMain ? entry.AssetName + ".png" : null, hasMain ? mainPtr : null,
                    backPng, backPng != null ? backPtr : null);
        }

        // No back image: MainImage goes to back_image slot.
        return (null, null, hasMain ? entry.AssetName + ".png" : null, hasMain ? mainPtr : null);
    }

    static JsonObject BuildJsonEntry(CosmeticEntry entry, AssetTypeValueField hvdField,
                                     string? imagePng, string? backImagePng)
    {
        var obj = new JsonObject
        {
            ["hat_type"]   = entry.HatType,
            ["asset_name"] = entry.AssetName
        };
        if (imagePng     != null) obj["image"]      = imagePng;
        if (backImagePng != null) obj["back_image"] = backImagePng;
        if (hvdField["MatchPlayerColor"].AsBool) obj["multi_color"] = true;
        return obj;
    }

    static void WriteSpritePng(SpriteExporter exporter, AssetsFileInstance hvdInst,
                                AssetTypeValueField? spritePtr, string? pngName,
                                CosmeticEntry entry, string slot,
                                string noneDir, Dictionary<string, string> writtenPaths)
    {
        if (spritePtr == null || pngName == null) return;
        var path = Path.Combine(noneDir, pngName);
        if (writtenPaths.TryGetValue(path, out var prev))
            Console.Error.WriteLine($"COLLISION {slot} {entry.ProductId} → {pngName} (first: {prev})");
        else
            writtenPaths[path] = $"{entry.ProductId}.{slot}";
        exporter.Export(hvdInst, spritePtr, path, isSkin: entry.HatType == "skins");
    }
}
