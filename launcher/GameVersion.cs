using System;
using System.Text.RegularExpressions;

namespace LetMeSleep.Updater {
    // Product stages follow Branko's release order, not alphabetic/SemVer prerelease ordering.
    public sealed class GameVersion : IComparable<GameVersion> {
        static readonly string[] Stages = { "alfa", "beta", "omega", "delta", "gamma" };
        readonly Version number;
        readonly int stage;
        readonly int revision;
        GameVersion(Version number, int stage, int revision) { this.number = number; this.stage = stage; this.revision = revision; }
        public static GameVersion Parse(string value) {
            var match = Regex.Match(value ?? "", @"^v?(\d+\.\d+\.\d+)(?:-(alfa|beta|omega|delta|gamma)(?:\.([1-9]\d*))?)?$");
            if (!match.Success) return null;
            Version parsed;
            if (!Version.TryParse(match.Groups[1].Value, out parsed) || parsed.ToString() != match.Groups[1].Value) return null;
            int revision = 0;
            if (match.Groups[3].Success && !int.TryParse(match.Groups[3].Value, out revision)) return null;
            return new GameVersion(parsed, match.Groups[2].Success ? Array.IndexOf(Stages,match.Groups[2].Value) : Stages.Length, revision);
        }
        public int CompareTo(GameVersion other) {
            if (ReferenceEquals(other,null)) return 1;
            int baseOrder = number.CompareTo(other.number); if (baseOrder != 0) return baseOrder;
            int stageOrder = stage.CompareTo(other.stage); return stageOrder != 0 ? stageOrder : revision.CompareTo(other.revision);
        }
        public override string ToString() { return number + (stage < Stages.Length ? "-" + Stages[stage] : "") + (revision > 0 ? "." + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""); }
        public override bool Equals(object obj) { var other = obj as GameVersion; return !ReferenceEquals(other,null) && CompareTo(other) == 0; }
        public override int GetHashCode() { return number.GetHashCode() ^ stage ^ revision; }
        public static bool operator ==(GameVersion a, GameVersion b) { return ReferenceEquals(a,b) || (!ReferenceEquals(a,null) && a.Equals(b)); }
        public static bool operator !=(GameVersion a, GameVersion b) { return !(a == b); }
        public static bool operator >(GameVersion a, GameVersion b) { return a.CompareTo(b) > 0; }
        public static bool operator <(GameVersion a, GameVersion b) { return a.CompareTo(b) < 0; }
        public static bool operator >=(GameVersion a, GameVersion b) { return a.CompareTo(b) >= 0; }
        public static bool operator <=(GameVersion a, GameVersion b) { return a.CompareTo(b) <= 0; }
    }
}
