using System;
using LetMeSleep.Content.Characters.Editor;

internal static class NormalSupportChecks
{
    private static int checks;
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    public static void Main()
    {
        // Eyelid anchor is stationary but shares a deformed face. Chin is disconnected.
        var moving = new[] { false, true, false, false, false, false };
        var support = EyelidMorphSupport.Build(moving, new[] { 0, 1, 2, 3, 4, 5 });
        Require(support[0] && support[1] && support[2] && !support[3] && !support[4] && !support[5], "Eyelid/chin isolation");
        Require(!moving[0] && !moving[2], "Input movement flags changed");
        // One-ring normal support must not spread to the next unchanged triangle.
        support = EyelidMorphSupport.Build(new[] { true, false, false, false, false }, new[] { 0, 1, 2, 2, 3, 4 });
        Require(support[2] && !support[3] && !support[4], "Support flooded the static face");
        var normals = new[] { 11.25f, -2.5f, 3.75f, -99f, 44f };
        EyelidMorphSupport.Filter(normals, support);
        Require(normals[0] == 11.25f && normals[1] == -2.5f && normals[2] == 3.75f && normals[3] == 0 && normals[4] == 0, "Valid deltas not preserved exactly");
        support = EyelidMorphSupport.Build(new bool[3], new[] { 0, 1, 2 });
        Require(!support[0] && !support[1] && !support[2], "Static shape gained support");
        bool rejected = false;
        try { EyelidMorphSupport.Build(new bool[3], new[] { 0, 1, 3 }); } catch (ArgumentException) { rejected = true; }
        Require(rejected, "Invalid mesh index not rejected");
        Console.WriteLine("Normal topology checks passed: " + checks);
    }
}
