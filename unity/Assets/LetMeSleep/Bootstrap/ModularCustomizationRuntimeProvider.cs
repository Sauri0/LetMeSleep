using System;
using System.Linq;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed class ModularCustomizationRuntimeProvider : MonoBehaviour
    {
        public CharacterCustomizationCatalog Catalog;
        public LegacyCustomizationMapping[] LegacyMappings = Array.Empty<LegacyCustomizationMapping>();

        public bool TryResolve(GameObject humanPrefab, GameObject mosquitoPrefab,
            out ModularCustomizationRuntime runtime, out string reason)
        {
            runtime = null; reason = string.Empty;
            if (!humanPrefab || !mosquitoPrefab)
            { reason = "The application human and mosquito prefabs must be assigned."; return false; }
            if (!Catalog)
            { reason = "No production customization catalog is assigned."; return false; }
            if (!Catalog.TryCreateSnapshot(out var snapshot, out var errors) || !snapshot.RuntimeReady)
            { reason = errors == null || errors.Length == 0 ? "The customization catalog is not runtime ready." : string.Join("; ", errors); return false; }
            if (!LegacyAppearanceMapper.TryCreate(LegacyMappings, snapshot, out var mapper, out reason)) return false;

            var hosts = new[]
            {
                Host(humanPrefab, CustomizationRole.Human, "application human prefab"),
                Host(mosquitoPrefab, CustomizationRole.Mosquito, "application mosquito prefab")
            };
            if (hosts.Any(item => item == null))
            { reason = "The application character prefabs must expose modular customization hosts."; return false; }

            var defaults = snapshot.DefaultSelection();
            foreach (var host in hosts)
                if (!host.Assembler.CanApply(host.View, Catalog, defaults, host.Role, out reason))
                { reason = host.Name + ": " + reason; return false; }

            runtime = new ModularCustomizationRuntime(Catalog, snapshot, mapper, hosts);
            return true;
        }

        private static ModularCustomizationHost Host(GameObject prefab, CustomizationRole role, string name)
        {
            if (!prefab) return null;
            var view = prefab.GetComponentInChildren<CharacterView>(true);
            var assembler = view ? view.GetComponent<CharacterModularVisualAssembler>() : null;
            return view && assembler ? new ModularCustomizationHost(name, role, view, assembler) : null;
        }
    }

    internal sealed class ModularCustomizationHost
    {
        internal ModularCustomizationHost(string name, CustomizationRole role, CharacterView view,
            CharacterModularVisualAssembler assembler)
        { Name = name; Role = role; View = view; Assembler = assembler; }
        internal string Name { get; }
        internal CustomizationRole Role { get; }
        internal CharacterView View { get; }
        internal CharacterModularVisualAssembler Assembler { get; }
    }

    public sealed class ModularCustomizationRuntime
    {
        private readonly LegacyAppearanceMapper mapper;
        private readonly ModularCustomizationHost[] hosts;

        internal ModularCustomizationRuntime(CharacterCustomizationCatalog catalog,
            CustomizationCatalogSnapshot snapshot, LegacyAppearanceMapper mapper,
            ModularCustomizationHost[] hosts)
        { Catalog = catalog; Snapshot = snapshot; this.mapper = mapper; this.hosts = hosts; }

        public CharacterCustomizationCatalog Catalog { get; }
        public CustomizationCatalogSnapshot Snapshot { get; }

        public bool TryMigrate(BasicCustomizationDraft legacy, out AppearanceSelection selection, out string error)
            => mapper.TryMap(legacy, Snapshot, out selection, out error);

        public bool CanApply(AppearanceSelection selection, out string reason)
        {
            reason = string.Empty;
            if (!Snapshot.TryNormalize(selection, out var normalized, out reason)) return false;
            foreach (var host in hosts)
                if (!host.Assembler.CanApply(host.View, Catalog, normalized, host.Role, out reason))
                { reason = host.Name + ": " + reason; return false; }
            return true;
        }

        public bool CanApply(CharacterView view, CustomizationRole role, out string reason)
        {
            reason = string.Empty;
            var assembler = view ? view.GetComponent<CharacterModularVisualAssembler>() : null;
            return assembler && assembler.CanApply(view, Catalog, role, out reason);
        }

        public bool TryApply(CharacterView view, AppearanceSelection selection, CustomizationRole role, out string error)
        {
            error = string.Empty;
            var assembler = view ? view.GetComponent<CharacterModularVisualAssembler>() : null;
            return assembler && assembler.TryApply(view, Catalog, selection, role, out error);
        }
    }
}
