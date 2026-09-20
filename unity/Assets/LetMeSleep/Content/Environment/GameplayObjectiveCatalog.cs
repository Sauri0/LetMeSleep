using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LetMeSleep.Content.Environment
{
    public enum GameplayObjectiveKind : byte { Repair, Clean, Switch }

    [DisallowMultipleComponent]
    public sealed class GameplayObjectiveCatalog : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string ObjectiveId;
            public GameplayObjectiveKind Kind;
            public string DisplayKey;
            public string ActionKey;
            public Vector3 LocalPosition;
            public Vector3 LocalApproachPoint;
            public float UseRadius = 1.25f;
            public uint WorkTicks = 90;
            public string RouteRegionId;
            public uint RouteBudgetTicks = 330;
            public string TargetPath;
        }

        [SerializeField] private string mapId = "";
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public string MapId => mapId;
        public IReadOnlyList<Entry> Entries => Array.AsReadOnly(entries ?? Array.Empty<Entry>());

        public void ValidateAuthoring(string expectedMapId)
        {
            if (string.IsNullOrWhiteSpace(expectedMapId) || !string.Equals(mapId, expectedMapId, StringComparison.Ordinal) ||
                entries == null || entries.Length == 0 || entries.Length > 24)
                throw new InvalidOperationException("Objective catalog identity/content is missing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || !ValidId(entry.ObjectiveId) || !ids.Add(entry.ObjectiveId) ||
                    !Enum.IsDefined(typeof(GameplayObjectiveKind), entry.Kind) || !ValidId(entry.DisplayKey) ||
                    !ValidId(entry.ActionKey) || !ValidId(entry.RouteRegionId) || !Finite(entry.LocalPosition) ||
                    !Finite(entry.LocalApproachPoint) || !Finite(entry.UseRadius) || entry.UseRadius < .1f ||
                    entry.UseRadius > 3 || Vector3.Distance(entry.LocalPosition, entry.LocalApproachPoint) > entry.UseRadius ||
                    entry.WorkTicks == 0 || entry.WorkTicks > 54000 || entry.RouteBudgetTicks > 54000 ||
                    entry.WorkTicks + entry.RouteBudgetTicks > 450 || !CanonicalPath(entry.TargetPath))
                    throw new InvalidOperationException("Invalid authored objective: " + entry?.ObjectiveId);
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(string authoredMapId, Entry[] authoredEntries)
        {
            mapId = authoredMapId;
            entries = authoredEntries ?? Array.Empty<Entry>();
            ValidateAuthoring(authoredMapId);
        }
#endif

        private static bool ValidId(string value) => !string.IsNullOrEmpty(value) && value.Length <= 96 &&
            value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && value.magnitude <= 10000;
        private static bool CanonicalPath(string path) => !string.IsNullOrWhiteSpace(path) && path == path.Trim() &&
            !path.Contains("\\") && path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != "..");
    }
}
