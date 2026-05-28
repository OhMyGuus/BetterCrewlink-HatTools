using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace BetterCrewLink.HatTools;

class BundleLoader
{
    readonly string _bundleDir;
    readonly Dictionary<string, AssetsFileInstance> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AssetsManager Manager { get; } = new();

    public BundleLoader(string bundleDir) => _bundleDir = bundleDir;

    // Load a bundle referenced by its catalog internal path (may use Windows backslashes).
    public AssetsFileInstance Load(string catalogInternalPath)
    {
        var filename = Path.GetFileName(catalogInternalPath.Replace('\\', '/'));
        return LoadFromPath(Path.Combine(_bundleDir, filename));
    }

    // Load a bundle by its full filesystem path.
    public AssetsFileInstance LoadFromPath(string fullPath)
    {
        if (!_cache.TryGetValue(fullPath, out var ai))
        {
            var bi = Manager.LoadBundleFile(fullPath, false);
            ai = Manager.LoadAssetsFileFromBundle(bi, 0, true);
            _cache[fullPath] = ai;
        }
        return ai;
    }

    // Returns a map of addressable asset path → pathID for all assets in the bundle's container.
    public Dictionary<string, long> GetAddressMap(AssetsFileInstance ai)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in ai.file.GetAssetsOfType(AssetClassID.AssetBundle).Take(1))
        {
            var f = Manager.GetBaseField(ai, info);
            foreach (var e in f["m_Container"]["Array"].Children)
                map[e[0].AsString] = e[1]["asset"]["m_PathID"].AsLong;
        }
        return map;
    }
}
