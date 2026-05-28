using System.Text.Json;
using System.Text.Json.Nodes;

namespace BetterCrewLink.HatTools;

static class HatsJsonWriter
{
    static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    public static void Write(string outputDir, JsonObject hatsData)
    {
        var formattedPath = Path.Combine(outputDir, "hats_formatted.json");
        var root = File.Exists(formattedPath)
            ? JsonNode.Parse(File.ReadAllText(formattedPath))!.AsObject()
            : new JsonObject();

        root["NONE"] = new JsonObject
        {
            ["defaultWidth"] = "130%",
            ["defaultTop"]   = "-78%",
            ["defaultLeft"]  = "-14%",
            ["hats"]         = hatsData
        };

        File.WriteAllText(formattedPath, root.ToJsonString(IndentedOptions));
        Console.WriteLine($"Written: {formattedPath}");

        var minPath = Path.Combine(outputDir, "hats.json");
        File.WriteAllText(minPath, root.ToJsonString());
        Console.WriteLine($"Written: {minPath}");
    }
}
