using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using LetMeSleep.Content.Editor.Higgsfield;

public static class RecipeFileChecks
{
    public static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("One recipe path required.");
        var recipe = JsonSerializer.Deserialize<HiggsfieldImportContract>(File.ReadAllText(args[0]),
            new JsonSerializerOptions { IncludeFields = true });
        if (recipe == null) throw new InvalidOperationException("Missing recipe.");
        recipe.Validate();
        foreach (var node in recipe.nodes)
            if (!ReferenceEquals(recipe.Resolve(node.path), node))
                throw new InvalidOperationException("Rule shadowed: " + node.path);
        Console.WriteLine(JsonSerializer.Serialize(new {
            status = "PASS_ACTUAL_CSHARP_CONTRACT_VALIDATE_AND_RESOLVE_ONLY", mapId = recipe.mapId,
            checkedPaths = recipe.nodes.Length, materials = recipe.materials.Length,
            humanSpawns = recipe.humanSpawns.Length, mosquitoSpawns = recipe.mosquitoSpawns.Length,
            classification = recipe.nodes.GroupBy(n => n.kind).ToDictionary(g => g.Key, g => g.Count()),
            nativeApplicationsStarted = false
        }));
        return 0;
    }
}
