using System;
using System.Text.RegularExpressions;

namespace LetMeSleep.Updater {
    // Product stages follow Branko's release order, not alphabetic/SemVer prerelease ordering.
    public sealed class GameVersion : IComparable<GameVersion> {
        static readonly string[] Stages = { "alfa", "beta", "omega", "delta", "gamma" };
        readonly Version number;
        readonly int stage;
        GameVersion(Version number, int stage) { this.number = number; this.stage = stage; }
        public static GameVersion Parse(string value) {
            var match = Regex.Match(value ?? "", @"^v?(\d+\.\d+\.\d+)(?:-(alfa|beta|omega|delta|gamma))?$");
            if (!match.Success) return null;
            Version parsed;
            if (!Version.TryParse(match.Groups[1].Value, out parsed) || parsed.ToString() != match.Groups[1].Value) return null;
            return new GameVersion(parsed, match.Groups[2].Success ? Array.IndexOf(Stages,match.Groups[2].Value) : Stages.Length);
        }
        public int CompareTo(GameVersion other) {
            if (ReferenceEquals(other,null)) return 1;
            int baseOrder = number.CompareTo(other.number); return baseOrder == 0 ? stage.CompareTo(other.stage) : baseOrder;
        }
        public override string ToString() { return number + (stage < Stages.Length ? "-" + Stages[stage] : ""); }
        public override bool Equals(object obj) { var other = obj as GameVersion; return !ReferenceEquals(other,null) && CompareTo(other) == 0; }
        public override int GetHashCode() { return number.GetHashCode() ^ stage; }
        public static bool operator ==(GameVersion a, GameVersion b) { return ReferenceEquals(a,b) || (!ReferenceEquals(a,null) && a.Equals(b)); }
        public static bool operator !=(GameVersion a, GameVersion b) { return !(a == b); }
        public static bool operator >(GameVersion a, GameVersion b) { return a.CompareTo(b) > 0; }
        public static bool operator <(GameVersion a, GameVersion b) { return a.CompareTo(b) < 0; }
        public static bool operator >=(GameVersion a, GameVersion b) { return a.CompareTo(b) >= 0; }
        public static bool operator <=(GameVersion a, GameVersion b) { return a.CompareTo(b) <= 0; }
    }
}
