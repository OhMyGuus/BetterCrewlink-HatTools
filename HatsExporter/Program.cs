using System.CommandLine;
using BetterCrewLink.HatTools;

var bundleDirOpt = new Option<string?>(
    ["--bundle-dir", "-b"],
    "Path to the Among Us Addressables bundle directory (Steam/StandaloneWindows).");

var outputDirOpt = new Option<string?>(
    ["--output-dir", "-o"],
    "Output directory for exported PNGs and JSON.");

var root = new RootCommand("Exports Among Us hat sprites to BetterCrewLink-Hats format.");
root.AddOption(bundleDirOpt);
root.AddOption(outputDirOpt);

root.SetHandler((bundleDir, outputDir) =>
{
    bundleDir ??= Path.Combine(
        AppContext.BaseDirectory,
        "..", "among-us-game-data", "Among Us_Data",
        "StreamingAssets", "aa", "Steam", "StandaloneWindows");
    outputDir ??= Path.Combine(AppContext.BaseDirectory, "..", "BetterCrewLink-Hats");

    Exporter.Run(bundleDir, outputDir);
}, bundleDirOpt, outputDirOpt);

return root.Invoke(args);
