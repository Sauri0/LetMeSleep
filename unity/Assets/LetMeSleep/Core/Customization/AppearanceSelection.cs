using System;
using System.Collections.Generic;
using System.Linq;

namespace LetMeSleep.Core.Customization
{
    public enum CustomizationRole : byte { Human = 0, Mosquito = 1 }

    [Serializable]
    public sealed class AppearanceSlotSelection
    {
        public string SlotId;
        public string OptionId;

        public AppearanceSlotSelection() { }
        public AppearanceSlotSelection(string slotId, string optionId)
        {
            SlotId = slotId ?? string.Empty;
            OptionId = optionId ?? string.Empty;
        }

        public AppearanceSlotSelection Copy() => new AppearanceSlotSelection(SlotId, OptionId);
    }

    [Serializable]
    public sealed class AppearanceLoadout
    {
        public AppearanceSlotSelection[] Selections = Array.Empty<AppearanceSlotSelection>();

        public AppearanceLoadout() { }
        public AppearanceLoadout(IEnumerable<AppearanceSlotSelection> selections)
        {
            Selections = (selections ?? Enumerable.Empty<AppearanceSlotSelection>())
                .Select(item => item?.Copy()).ToArray();
        }

        public AppearanceLoadout Copy() => new AppearanceLoadout(Selections);

        public string OptionFor(string slotId)
        {
            if (slotId == null || Selections == null) return null;
            var match = Selections.FirstOrDefault(item => item != null && item.SlotId == slotId);
            return match?.OptionId;
        }

        public void SetOption(string slotId, string optionId)
        {
            if (string.IsNullOrEmpty(slotId)) throw new ArgumentException("Slot ID is required.", nameof(slotId));
            var result = new List<AppearanceSlotSelection>();
            bool replaced = false;
            foreach (var item in Selections ?? Array.Empty<AppearanceSlotSelection>())
            {
                if (item != null && item.SlotId == slotId)
                {
                    if (!replaced) result.Add(new AppearanceSlotSelection(slotId, optionId));
                    replaced = true;
                }
                else result.Add(item?.Copy());
            }
            if (!replaced) result.Add(new AppearanceSlotSelection(slotId, optionId));
            Selections = result.ToArray();
        }

        internal IEnumerable<AppearanceSlotSelection> CanonicalSelections() =>
            (Selections ?? Array.Empty<AppearanceSlotSelection>()).Where(item => item != null)
            .OrderBy(item => item.SlotId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.OptionId ?? string.Empty, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class AppearanceSelection
    {
        public const int CurrentSchema = 1;
        public int Schema = CurrentSchema;
        public AppearanceLoadout Human = new AppearanceLoadout();
        public AppearanceLoadout Mosquito = new AppearanceLoadout();

        public AppearanceSelection() { }
        public AppearanceSelection(AppearanceLoadout human, AppearanceLoadout mosquito, int schema = CurrentSchema)
        {
            Schema = schema;
            Human = human?.Copy() ?? new AppearanceLoadout();
            Mosquito = mosquito?.Copy() ?? new AppearanceLoadout();
        }

        public AppearanceLoadout For(CustomizationRole role)
        {
            if (role == CustomizationRole.Human) return Human;
            if (role == CustomizationRole.Mosquito) return Mosquito;
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        public AppearanceSelection Copy() => new AppearanceSelection(Human, Mosquito, Schema);

        public bool CanonicalEquals(AppearanceSelection other)
        {
            if (other == null || Schema != other.Schema) return false;
            return Equal(Human, other.Human) && Equal(Mosquito, other.Mosquito);
        }

        private static bool Equal(AppearanceLoadout left, AppearanceLoadout right)
        {
            if (left == null || right == null) return left == right;
            return left.CanonicalSelections().Select(Key)
                .SequenceEqual(right.CanonicalSelections().Select(Key), StringComparer.Ordinal);
        }

        private static string Key(AppearanceSlotSelection item) =>
            (item.SlotId ?? string.Empty) + "\u001f" + (item.OptionId ?? string.Empty);
    }
}
