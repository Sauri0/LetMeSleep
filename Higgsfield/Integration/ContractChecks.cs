using System;
using LetMeSleep.Content.Editor.Higgsfield;

public static class ContractChecks
{
    static int checks;
    static HiggsfieldImportContract Valid() => new HiggsfieldImportContract {
        mapId = "hf-isla-review-01", sourceFbx = "final.fbx", sourceFinal = true, sourceSha256 = new string('a', 64),
        requiredHumanSpawns = 2, requiredMosquitoSpawns = 1,
        humanSpawns = new[] { "Spawn_Human_01", "Spawn_Human_02" }, mosquitoSpawns = new[] { "Spawn_Mosquito_01" },
        playBoundsMin = new[] { -10f, -1f, -10f }, playBoundsMax = new[] { 10f, 10f, 10f },
        nodes = new[] {
            new HiggsfieldNodeRule { path = "Forest", kind = "foliage", descendants = true },
            new HiggsfieldNodeRule { path = "Forest/Tree01/Trunk", kind = "solid" },
            new HiggsfieldNodeRule { path = "Water_Ocean", kind = "water" },
            new HiggsfieldNodeRule { path = "Grass", kind = "foliage", descendants = true } },
        materials = new[] { new HiggsfieldSwatch { sourceName = "Green", rgb = new[] { .2f, .5f, .1f } } }
    };
    static void Expect(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static void Reject(Action<HiggsfieldImportContract> change, string label)
    {
        var c = Valid(); change(c);
        try { c.Validate(); } catch (InvalidOperationException) { checks++; return; }
        throw new Exception("Accepted invalid contract: " + label);
    }
    public static int Main()
    {
        var c = Valid(); c.Validate(); checks++;
        Expect(c.Resolve("Forest/Tree01/Trunk").kind == "solid", "Trunk override");
        Expect(c.Resolve("Forest/Tree01/Leaves").kind == "foliage", "Leaves excluded");
        Expect(c.Resolve("Grass/Blade").kind == "foliage", "Grass excluded");
        Expect(c.Resolve("Water_Ocean").kind == "water", "Ocean excluded");
        try { c.Resolve("ForestExtra/Trunk"); throw new Exception("Prefix crossed hierarchy boundary"); } catch (InvalidOperationException) { checks++; }
        Reject(x => x.sourceFinal = false, "partial source");
        Reject(x => x.sourceSha256 = "pending", "missing hash");
        Reject(x => x.mapId = "../alfa", "output traversal");
        Reject(x => x.importScale = float.NaN, "NaN scale");
        Reject(x => x.firstSurfaceId = 0, "zero identity");
        Reject(x => x.humanSpawns = new[] { "Spawn_Human_01", "Spawn_Human_01" }, "duplicate spawn");
        Reject(x => x.mosquitoSpawns = new[] { "Spawn_Human_01" }, "cross-role duplicate");
        Reject(x => x.requiredHumanSpawns = 16, "insufficient capacity");
        Reject(x => x.requiredMosquitoSpawns = 0, "zero capacity");
        Reject(x => x.playBoundsMax = x.playBoundsMin, "empty bounds");
        Reject(x => x.playBoundsMin[0] = float.NegativeInfinity, "infinite bounds");
        Reject(x => x.boundsMinEmpty = "Min", "unpaired bounds EMPTY");
        Reject(x => x.nodes[0].kind = "everything", "unknown classification");
        Reject(x => x.nodes[1].path = x.nodes[0].path, "ambiguous override");
        Reject(x => x.nodes[0].waveAmplitude = 1f, "excessive water displacement");
        Reject(x => x.materials[0].rgb[0] = float.NaN, "invalid swatch");
        Reject(x => x.materials[0].colorSpace = "guess", "unspecified colour encoding");
        Reject(x => x.materials = new[] { x.materials[0], x.materials[0] }, "ambiguous material");
        Console.WriteLine("PASS " + checks + " offline contract checks; no Unity/native import or geometry execution.");
        return 0;
    }
}
