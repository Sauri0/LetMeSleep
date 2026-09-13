using System;

namespace LetMeSleep.Content.Environment.Higgsfield
{
    /// <summary>Only the two inspected authored naming schemes; no guessed or partial morph pairs.</summary>
    public static class HiggsfieldWaveNames
    {
        public static bool TryPair(string[] names, out string waveA, out string waveB)
        {
            waveA = waveB = null;
            if (names == null || names.Length != 2) return false;
            foreach (var pair in new[] { new[] { "Wave_A", "Wave_B" }, new[] { "Wave1", "Wave2" } })
            {
                int a = Find(names, pair[0]), b = Find(names, pair[1]);
                if (a < 0 || b < 0 || a == b) continue;
                waveA = pair[0]; waveB = pair[1]; return true;
            }
            return false;
        }

        public static int Find(string[] names, string name)
        {
            if (names == null || string.IsNullOrEmpty(name)) return -1;
            int match = -1;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name || (names[i] != null && names[i].EndsWith("." + name, StringComparison.Ordinal)))
                { if (match >= 0) return -1; match = i; }
            return match;
        }
    }
}
