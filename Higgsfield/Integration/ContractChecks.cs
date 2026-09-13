using System;
using LetMeSleep.Content.Editor.Higgsfield;
using LetMeSleep.Content.Environment.Higgsfield;

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
        Expect(HiggsfieldWaveNames.TryPair(new[] { "Wave_A", "Wave_B" }, out var a, out var b) && a == "Wave_A" && b == "Wave_B", "Isla pair");
        Expect(HiggsfieldWaveNames.TryPair(new[] { "Key.Wave2", "Key.Wave1" }, out a, out b) && a == "Wave1" && b == "Wave2", "Campamento pair with FBX prefix and reversed order");
        foreach (var names in new[] {
            null, Array.Empty<string>(), new[] { "Wave1" }, new[] { "Wave_A", "Wave2" },
            new[] { "Wave1", "Other.Wave1" }, new[] { "Wave1", "Wave2", "Jaw" },
            new[] { "wave1", "Wave2" }, new[] { "FakeWave1", "Wave2" }, new[] { "Wave1", (string)null } })
            Expect(!HiggsfieldWaveNames.TryPair(names, out _, out _), "Reject partial, mixed, duplicate, extra or unknown morph set");
        Expect(HiggsfieldWaveNames.Find(new[] { "Wave_A", "Key.Wave_A" }, "Wave_A") == -1, "Reject ambiguous suffix");
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
        Expect(c.materials[0].EmissionLinear()[0] == 0, "Old recipe emission defaults to zero");
        var emissive = Valid(); emissive.materials[0].emissionRgb = new[] { 1f, .4f, .075f }; emissive.materials[0].emissionStrength = 2.3f;
        emissive.Validate();
        Expect(Math.Abs(emissive.materials[0].EmissionLinear()[1] - .92f) < .00001f, "Linear HDR product");
        HiggsfieldImportContract.ValidateEmissionOnlyChange(Valid(), emissive); checks++;
        Reject(x => x.materials[0].emissionStrength = -1, "negative emission");
        Reject(x => x.materials[0].emissionStrength = float.NaN, "NaN emission");
        Reject(x => x.materials[0].emissionStrength = 1, "missing emission RGB");
        Reject(x => x.materials[0].emissionRgb = new[] { 1f, -1f, 0f }, "negative emission RGB");
        Reject(x => { x.materials[0].emissionRgb = new[] { float.MaxValue, 0f, 0f }; x.materials[0].emissionStrength = 2; }, "emission overflow");
        foreach (var change in new Action<HiggsfieldImportContract>[] {
            x => x.mapId = "hf-another-map", x => x.sourceSha256 = new string('b', 64),
            x => x.materials[0].rgb[0] = .3f, x => x.nodes[0].path = "AnotherForest",
            x => x.humanSpawns[0] = "OtherSpawn" })
        {
            var changed = Valid(); change(changed);
            bool rejected = false;
            try { HiggsfieldImportContract.ValidateEmissionOnlyChange(Valid(), changed); } catch (InvalidOperationException) { rejected = true; }
            Expect(rejected, "Repair rejects non-emission changes");
        }
        Console.WriteLine("PASS " + checks + " offline contract checks; no Unity/native import or geometry execution.");
        return 0;
    }
}
