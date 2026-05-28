using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace BetterCrewLink.HatTools;

static class CosmeticCollector
{
    public static List<CosmeticEntry> Collect(
        BundleLoader loader,
        AssetsFileInstance refInst,
        IReadOnlyDictionary<string, (string address, string bundlePath)> guidToViewData)
    {
        var entries = new List<CosmeticEntry>();
        int hatCount = 0, skinCount = 0, visorCount = 0;

        foreach (var info in refInst.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        {
            var field = loader.Manager.GetBaseField(refInst, info);
            var scriptName = GetScriptName(loader.Manager, refInst, field);
            if (scriptName is not ("HatData" or "SkinData" or "VisorData")) continue;

            var guid = field["ViewDataRef"]["m_AssetGUID"].AsString;
            if (string.IsNullOrEmpty(guid) || guid == "00000000000000000000000000000000") continue;
            if (!guidToViewData.TryGetValue(guid, out var viewInfo)) continue;

            entries.Add(new CosmeticEntry(
                ProductId:        field["ProductId"].AsString,
                HatType:          scriptName switch { "SkinData" => "skins", "VisorData" => "visors", _ => "hats" },
                AssetName:        field["m_Name"].AsString,
                ViewDataAddress:  viewInfo.address,
                BundlePath:       viewInfo.bundlePath,
                InFront:          scriptName == "HatData" && field["InFront"].AsBool));

            if (scriptName == "HatData") hatCount++;
            else if (scriptName == "SkinData") skinCount++;
            else visorCount++;
        }

        Console.WriteLine($"Collected: {hatCount} hats, {skinCount} skins, {visorCount} visors");
        return entries;
    }

    static string GetScriptName(AssetsManager mgr, AssetsFileInstance inst, AssetTypeValueField f)
    {
        var s = f["m_Script"];
        if (s.IsDummy) return "";
        var e = mgr.GetExtAsset(inst, s);
        return e.baseField?["m_Name"].AsString ?? "";
    }
}
