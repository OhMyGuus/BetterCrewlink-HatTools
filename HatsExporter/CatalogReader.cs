using AddressablesTools;

namespace BetterCrewLink.HatTools;

class CatalogReader
{
    const string BundledProvider = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";

    public IReadOnlyDictionary<string, (string address, string bundlePath)> GuidToViewData { get; }

    CatalogReader(Dictionary<string, (string, string)> guidToViewData) =>
        GuidToViewData = guidToViewData;

    public static CatalogReader Load(string catalogPath)
    {
        var ccd = AddressablesCatalogFileParser.FromJsonString(File.ReadAllText(catalogPath));
        var map = new Dictionary<string, (string address, string bundlePath)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, locs) in ccd.Resources)
        {
            if (key is not string s || s.Length != 32 || !IsHex(s)) continue;
            foreach (var loc in locs)
            {
                if (loc.ProviderId != BundledProvider) continue;
                var deps = loc.Dependencies;
                if ((deps == null || deps.Count == 0) && loc.DependencyKey != null)
                    deps = ccd.Resources.TryGetValue(loc.DependencyKey, out var dl) ? dl : null;
                map[s] = (loc.InternalId, deps?.Count > 0 ? deps[0].InternalId : "");
                break;
            }
        }

        Console.WriteLine($"  GUID→ViewData entries: {map.Count}");
        return new CatalogReader(map);
    }

    static bool IsHex(string s)
    {
        foreach (char c in s)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }
}
