using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Characters;
using LetMeSleep.Content.Characters.Editor;
using LetMeSleep.Core.Customization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// v0.3.0 production modular customization (sketches PER-04..07, UI-06 screens 5-6), installed idempotently:
    /// <list type="number">
    /// <item>imports the part FBX exported by art_source/unity/characters/author_modular_parts.py (same rig and
    /// bind pose as the production characters, verified bone by bone),</item>
    /// <item>builds one CharacterCustomizationPart prefab per option (skinned on the host bones by path),</item>
    /// <item>writes the catalog asset (fixed slot/option table below; labels in Spanish, wire codes never
    /// reused),</item>
    /// <item>installs CharacterCustomizationHost + CharacterModularVisualAssembler on LMS_Human,
    /// LMS_Human_FirstPerson, LMS_HumanMenu and LMS_Mosquito,</item>
    /// <item>installs the ModularCustomizationRuntimeProvider (catalog + the 13 legacy colour mappings) on the
    /// application object of the build scene, and</item>
    /// <item>optionally bakes one thumbnail per option (a GPU render on the viewer layer).</item>
    /// </list>
    /// CharacterContentPipeline.RebuildAll calls <see cref="InstallAll"/> after the prefabs are rebuilt.
    /// </summary>
    public static class CharacterCustomizationContentBuilder
    {
        public const string Root = "Assets/LetMeSleep/Content/Characters/Customization";
        public const string CatalogPath = Root + "/LMS_CustomizationCatalog.asset";
        public const string ReceiptPath = Root + "/CustomizationBuildReceipt.json";
        public const string CatalogId = "lms.v030.characters";
        public const int CatalogRevision = 1;
        public const string HumanRigId = "lms.human.v030";
        public const string MosquitoRigId = "lms.mosquito.v030";
        public const string ScenePath = "Assets/Scenes/LetMeSleepHiggsfield.unity";
        public const int ViewerLayer = 30;
        private const string CharacterRoot = "Assets/LetMeSleep/Content/Characters";
        private const string ModelsRoot = Root + "/Models";
        private const string PartsRoot = Root + "/Parts";
        private const string ThumbnailsRoot = Root + "/Thumbnails";
        private static string SourceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../art_source/unity/characters"));

        public static readonly string[] HostPrefabs = { "LMS_Human", "LMS_Human_FirstPerson", "LMS_HumanMenu", "LMS_Mosquito" };

        // ------------------------------------------------------------------ catalog definition
        private sealed class OptionSpec
        {
            public string Id, Label, Hex;
            public int Wire;
            public CustomizationOptionKind Kind;
        }

        private sealed class SlotSpec
        {
            public CustomizationRole Role;
            public string Id, Label, Default;
            public int Wire;
            public bool Required, AllowsNone, IsBase;
            public OptionSpec[] Options;
        }

        private static OptionSpec Part(int wire, string id, string label) =>
            new OptionSpec { Wire = wire, Id = id, Label = label, Kind = CustomizationOptionKind.SkinnedPart };
        private static OptionSpec Swatch(int wire, string id, string label, string hex) =>
            new OptionSpec { Wire = wire, Id = id, Label = label, Hex = hex, Kind = CustomizationOptionKind.Color };
        private static OptionSpec None(string label) =>
            new OptionSpec { Wire = 0, Id = "none", Label = label, Kind = CustomizationOptionKind.None };

        private static SlotSpec Slot(CustomizationRole role, int wire, string id, string label, string defaultId,
            bool allowsNone, params OptionSpec[] options) => new SlotSpec
        {
            Role = role, Wire = wire, Id = id, Label = label, Default = defaultId, Required = !allowsNone,
            AllowsNone = allowsNone, Options = options
        };

        /// <summary>
        /// The production catalog. Wire slot codes 5 (facial hair), 13 (gloves), 19 (mosquito eyes: they need a new
        /// facial certification) and 21 (legs) stay reserved. Never renumber or reuse a code; retire an option by
        /// clearing its label. Colour defaults equal the authored palette (Human_Skin #C98B5A, Human_Shirt
        /// #DFC195, Human_Pajamas #2D4F9A, Human_Hair #3A2619, Mosquito_Shell #9E2228, Mosquito_Wing #CCC4F6), so
        /// the default modular look is the authored character.
        /// </summary>
        private static SlotSpec[] Definition()
        {
            var human = CustomizationRole.Human;
            var mosquito = CustomizationRole.Mosquito;
            var baseHuman = Slot(human, 1, "human.base", string.Empty, "base", false, Part(1, "base", "Cuerpo"));
            baseHuman.IsBase = true;
            var baseMosquito = Slot(mosquito, 15, "mosquito.base", string.Empty, "base", false, Part(1, "base", "Cuerpo"));
            baseMosquito.IsBase = true;
            return new[]
            {
                baseHuman,
                Slot(human, 2, "human.skin", "TONO DE PIEL", "calido", false,
                    Swatch(1, "muy-claro", "Muy claro", "#E6BC97"), Swatch(2, "claro", "Claro", "#E3AE82"),
                    Swatch(3, "calido", "Cálido", "#C98B5A"), Swatch(4, "bronce", "Bronce", "#9C6139"),
                    Swatch(5, "morena", "Morena", "#744528"), Swatch(6, "oscuro", "Oscuro", "#4E2C1C")),
                Slot(human, 3, "human.hair", "PELO", "corto", false,
                    Part(1, "corto", "Corto"), Part(2, "despeinado", "Despeinado"), Part(3, "rulos", "Rulos")),
                Slot(human, 4, "human.hair_color", "COLOR DE PELO", "castano", false,
                    Swatch(1, "castano", "Castaño", "#3A2619"), Swatch(2, "negro", "Negro", "#1B1614"),
                    Swatch(3, "castano-claro", "Castaño claro", "#76482A"), Swatch(4, "rubio", "Rubio", "#D6AA5A"),
                    Swatch(5, "pelirrojo", "Pelirrojo", "#A8481F"), Swatch(6, "canoso", "Canoso", "#B4B1AB")),
                Slot(human, 6, "human.headwear", "GORROS", "gorro-dormir", true,
                    None("Sin gorro"), Part(1, "gorro-dormir", "Gorro de dormir"), Part(2, "gorra", "Gorra roja"),
                    Part(3, "gorro-lana", "Gorro de lana")),
                Slot(human, 7, "human.glasses", "LENTES", "none", true,
                    None("Sin lentes"), Part(1, "redondos", "Redondos"), Part(2, "sol", "De sol")),
                Slot(human, 8, "human.top", "REMERA", "remera", false,
                    Part(1, "remera", "Remera"), Part(2, "buzo", "Buzo con capucha")),
                Slot(human, 9, "human.top_color", "COLOR DE REMERA", "crema", false,
                    Swatch(1, "crema", "Crema", "#DFC195"), Swatch(2, "blanco", "Blanco", "#ECE9E2"),
                    Swatch(3, "gris", "Gris", "#8B919D"), Swatch(4, "negro", "Negro", "#34373F"),
                    Swatch(5, "rojo", "Rojo", "#C8322E"), Swatch(6, "azul", "Azul", "#2F6FD6"),
                    Swatch(7, "verde", "Verde", "#3E9B4A"), Swatch(8, "amarillo", "Amarillo", "#F2C230"),
                    Swatch(9, "naranja", "Naranja", "#E8782C")),
                Slot(human, 10, "human.bottom", "PANTALÓN", "pijama", false,
                    Part(1, "pijama", "Pijama con lunares"), Part(2, "jean", "Jean")),
                Slot(human, 11, "human.pajama", "COLOR DE PIJAMA", "azul", false,
                    Swatch(1, "azul", "Azul", "#2D4F9A"), Swatch(2, "rojo", "Rojo", "#A62B29"),
                    Swatch(3, "verde", "Verde", "#296645"), Swatch(4, "violeta", "Violeta", "#66388F"),
                    Swatch(5, "mostaza", "Mostaza", "#B88A2E"), Swatch(6, "celeste", "Celeste", "#4F8FCC")),
                Slot(human, 12, "human.footwear", "CALZADO", "pantuflas", false,
                    Part(1, "pantuflas", "Pantuflas"), Part(2, "zapatillas", "Zapatillas")),
                Slot(human, 14, "human.back", "MOCHILA", "none", true,
                    None("Sin mochila"), Part(1, "mochila", "Mochila")),
                baseMosquito,
                Slot(mosquito, 16, "mosquito.color", "COLOR DEL CUERPO", "natural", false,
                    Swatch(1, "natural", "Natural", "#9E2228"), Swatch(2, "bosque", "Bosque", "#5C6A3E"),
                    Swatch(3, "desierto", "Desierto", "#A9814F"), Swatch(4, "urbano", "Urbano", "#4A5165"),
                    Swatch(5, "fantasia", "Fantasía", "#6552D9"), Swatch(6, "toxico", "Tóxico", "#5DB832"),
                    Swatch(7, "sangre", "Sangre", "#6B1419"), Swatch(8, "hielo", "Hielo", "#79B3DD")),
                Slot(mosquito, 17, "mosquito.wings", "ALAS", "clasicas", false,
                    Part(1, "clasicas", "Clásicas"), Part(2, "redondas", "Redondas"), Part(3, "largas", "Largas")),
                Slot(mosquito, 18, "mosquito.wing_color", "COLOR DE ALAS", "lavanda", false,
                    Swatch(1, "lavanda", "Lavanda", "#CCC4F6"), Swatch(2, "celeste", "Celeste", "#B9E2F7"),
                    Swatch(3, "rosa", "Rosa", "#F5C2DD"), Swatch(4, "menta", "Menta", "#BFF0D6"),
                    Swatch(5, "ambar", "Ámbar", "#F6DC9E"), Swatch(6, "humo", "Humo", "#9FA4B8")),
                Slot(mosquito, 20, "mosquito.proboscis", "PROBÓSCIDE", "estandar", false,
                    Part(1, "estandar", "Estándar"), Part(2, "corta", "Corta"), Part(3, "larga", "Larga"), Part(4, "curva", "Curva")),
                Slot(mosquito, 22, "mosquito.markings", "MARCAS", "none", true,
                    None("Sin marcas"), Part(1, "anillos", "Anillos"), Part(2, "lunares", "Lunares"), Part(3, "rayas", "Rayas")),
                Slot(mosquito, 23, "mosquito.accent_color", "COLOR DE MARCAS", "crema", false,
                    Swatch(1, "crema", "Crema", "#F2EBDD"), Swatch(2, "negro", "Negro", "#1D1618"),
                    Swatch(3, "amarillo", "Amarillo", "#F2C230"), Swatch(4, "naranja", "Naranja", "#EE7A2B"),
                    Swatch(5, "verde", "Verde", "#7ED23B"), Swatch(6, "celeste", "Celeste", "#6CC6F2"),
                    Swatch(7, "violeta", "Violeta", "#9A6BE0")),
                Slot(mosquito, 24, "mosquito.accessory", "ACCESORIO", "none", true,
                    None("Ninguno"), Part(1, "hoja", "Hoja"), Part(2, "flor", "Flor")),
            };
        }

        /// <summary>The 13 legacy colour ids of the basic mode (preferences schema 1, wire v1) and their options.</summary>
        public static LegacyCustomizationMapping[] LegacyMappings() => new[]
        {
            Legacy(LegacyCustomizationKind.Skin, "light", "human.skin", "claro"),
            Legacy(LegacyCustomizationKind.Skin, "warm", "human.skin", "calido"),
            Legacy(LegacyCustomizationKind.Skin, "tan", "human.skin", "bronce"),
            Legacy(LegacyCustomizationKind.Skin, "dark", "human.skin", "oscuro"),
            Legacy(LegacyCustomizationKind.Pajama, "blue", "human.pajama", "azul"),
            Legacy(LegacyCustomizationKind.Pajama, "red", "human.pajama", "rojo"),
            Legacy(LegacyCustomizationKind.Pajama, "green", "human.pajama", "verde"),
            Legacy(LegacyCustomizationKind.Pajama, "purple", "human.pajama", "violeta"),
            Legacy(LegacyCustomizationKind.Pajama, "yellow", "human.pajama", "mostaza"),
            Legacy(LegacyCustomizationKind.Mosquito, "red", "mosquito.color", "natural"),
            Legacy(LegacyCustomizationKind.Mosquito, "blue", "mosquito.color", "hielo"),
            Legacy(LegacyCustomizationKind.Mosquito, "green", "mosquito.color", "bosque"),
            Legacy(LegacyCustomizationKind.Mosquito, "purple", "mosquito.color", "fantasia"),
        };

        private static LegacyCustomizationMapping Legacy(LegacyCustomizationKind kind, string id, string slot, string option) =>
            new LegacyCustomizationMapping { Kind = kind, LegacyId = id, SlotId = slot, OptionId = option };

        // ------------------------------------------------------------------ parts.json
#pragma warning disable CS0649
        [Serializable] private sealed class Manifest
        {
            public int schema;
            public string species, rig_id, rig_signature_sha256;
            public BindBone[] bind_bones;
            public PartRecord[] parts;
            public PaletteEntry[] material_palette;
        }
        [Serializable] private sealed class BindBone { public string name, parent; public float[] head_blender_m, tail_blender_m; }
        [Serializable] private sealed class HostAudit { public string species; public BindBone[] bind_bones; }
        [Serializable] private sealed class PartRecord
        {
            public string role, slot, option, fbx, facial_impact, note, sha256;
            public RendererRecord[] renderers;
            public ColorRecord[] colors;
        }
        [Serializable] private sealed class RendererRecord
        {
            public string name, hidden_when_slot_selected;
            public int triangles;
            public string[] materials, bones;
            public bool first_person_head;
        }
        [Serializable] private sealed class ColorRecord { public string renderer, material, slot, shade_reference; public float alpha; }
        [Serializable] private sealed class PaletteEntry { public string name; public Color color; public float roughness; public bool @new; }
#pragma warning restore CS0649

        [Serializable] public sealed class Receipt
        {
            public string builder = "v030-modular-customization-1";
            public string catalogId, fingerprint;
            public int revision, slots, options, parts;
            public bool runtimeReady, providerInstalled;
            public string[] hosts, partPrefabs, thumbnails;
            public string scope = "Imported part meshes, catalog contract, host/provider installation and editor application of every option on every host prefab. No playtest, online or visual approval claim.";
        }

        // ------------------------------------------------------------------ entry points
        [MenuItem("Let Me Sleep/Content/Install Modular Customization")]
        public static void InstallAll() => Install(false);

        [MenuItem("Let Me Sleep/Content/Install Modular Customization and Bake Thumbnails")]
        public static void InstallAllWithThumbnails() => Install(true);

        /// <summary>Batch entry (-executeMethod): thumbnails need a graphics device (do not pass -nographics).</summary>
        public static void InstallFromCommandLine()
        {
            bool thumbnails = Environment.GetCommandLineArgs().Contains("-lmsCustomizationThumbnails");
            Install(thumbnails);
        }

        public static Receipt Install(bool bakeThumbnails)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            EnsureFolder(Root); EnsureFolder(ModelsRoot); EnsureFolder(PartsRoot); EnsureFolder(ThumbnailsRoot);
            var definition = Definition();
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var species in new[] { "Human", "Mosquito" })
            {
                var manifest = ReadManifest(species);
                ValidateRig(species, manifest);
                foreach (var record in manifest.parts)
                    prefabs[record.slot + "/" + record.option] = BuildPart(species, manifest, record);
            }
            foreach (var slot in definition)
                foreach (var option in slot.Options.Where(item => item.Kind == CustomizationOptionKind.SkinnedPart))
                    Require(prefabs.ContainsKey(slot.Id + "/" + option.Id), "Missing part export for " + slot.Id + "/" + option.Id);
            var catalog = WriteCatalog(definition, prefabs);
            var hosts = HostPrefabs.Select(InstallHost).ToArray();
            AssetDatabase.SaveAssets();
            string[] thumbnails = Array.Empty<string>();
            if (bakeThumbnails) thumbnails = BakeThumbnails(catalog);
            // Scene switches unload assets that only managed code references: reload the catalog.
            catalog = LoadCatalog();
            AssignThumbnails(catalog);
            var snapshot = VerifyEveryOption(catalog);
            bool provider = InstallProvider();
            catalog = LoadCatalog();
            var receipt = new Receipt
            {
                catalogId = catalog.CatalogId, revision = catalog.Revision, fingerprint = snapshot.Fingerprint,
                slots = snapshot.Slots.Count, options = snapshot.Slots.Sum(item => item.Options.Count),
                parts = prefabs.Count, runtimeReady = snapshot.RuntimeReady, providerInstalled = provider, hosts = hosts,
                partPrefabs = prefabs.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + " " + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Value))).ToArray(),
                thumbnails = catalog.Options.Where(item => item.Thumbnail).Select(item => item.SlotId + "/" + item.OptionId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
            WriteIfChanged(ReceiptPath, Encoding.UTF8.GetBytes(JsonUtility.ToJson(receipt, true) + "\n"));
            AssetDatabase.ImportAsset(ReceiptPath);
            AssetDatabase.SaveAssets();
            Debug.Log("LMS_MODULAR_CUSTOMIZATION_INSTALLED fingerprint=" + snapshot.Fingerprint + " network=" +
                      snapshot.NetworkFingerprint.ToString("x16", CultureInfo.InvariantCulture) + " slots=" + receipt.slots +
                      " options=" + receipt.options + " parts=" + receipt.parts + " thumbnails=" + receipt.thumbnails.Length +
                      (bakeThumbnails ? " baked=" + thumbnails.Length : string.Empty));
            return receipt;
        }

        // ------------------------------------------------------------------ source validation
        private static Manifest ReadManifest(string species)
        {
            string path = Path.Combine(SourceRoot, "modular", species.ToLowerInvariant(), "parts.json");
            Require(File.Exists(path), "Missing modular parts manifest: " + path);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            Require(manifest != null && manifest.schema == 1 && manifest.species == species && manifest.parts != null &&
                    manifest.parts.Length > 0, "Invalid modular parts manifest: " + path);
            Require(manifest.rig_id == (species == "Human" ? HumanRigId : MosquitoRigId), "Unexpected rig id in " + path);
            return manifest;
        }

        /// <summary>Every part is skinned on the production rig: same bones, parents and bind positions (0.1 mm).</summary>
        private static void ValidateRig(string species, Manifest manifest)
        {
            string path = Path.Combine(SourceRoot, species.ToLowerInvariant(), "audit.json");
            var host = JsonUtility.FromJson<HostAudit>(File.ReadAllText(path));
            Require(host != null && host.bind_bones != null && host.bind_bones.Length > 0, "Host audit has no bind bones: " + path);
            var hostBones = host.bind_bones.ToDictionary(item => item.name, StringComparer.Ordinal);
            Require(hostBones.Count == manifest.bind_bones.Length, species + " modular rig bone count differs from the production rig.");
            foreach (var bone in manifest.bind_bones)
            {
                Require(hostBones.TryGetValue(bone.name, out var other), species + " modular rig has an unknown bone " + bone.name);
                Require(other.parent == bone.parent, species + " modular rig parent differs for " + bone.name);
                for (int i = 0; i < 3; i++)
                    Require(Mathf.Abs(other.head_blender_m[i] - bone.head_blender_m[i]) < 1e-4f &&
                            Mathf.Abs(other.tail_blender_m[i] - bone.tail_blender_m[i]) < 1e-4f,
                        species + " modular rig bind pose differs for " + bone.name);
            }
        }

        // ------------------------------------------------------------------ parts
        private static GameObject BuildPart(string species, Manifest manifest, PartRecord record)
        {
            string source = Path.Combine(SourceRoot, "modular", species.ToLowerInvariant(), record.fbx);
            Require(File.Exists(source), "Missing part FBX: " + source);
            EnsureFolder(ModelsRoot + "/" + species); EnsureFolder(PartsRoot + "/" + species);
            string modelPath = ModelsRoot + "/" + species + "/" + record.fbx;
            ImportPartModel(source, modelPath, manifest);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Require(model != null, "Part model did not import: " + modelPath);
            var host = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "/Models/LMS_" + species + "_alpha.fbx");
            Require(host != null, "Production model missing for " + species);
            var hostBinds = BindMatrices(host);
            var modelSkins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            var root = new GameObject("Part_" + record.slot + "_" + record.option);
            try
            {
                var part = root.AddComponent<CharacterCustomizationPart>();
                part.Role = species == "Human" ? CustomizationRole.Human : CustomizationRole.Mosquito;
                part.SlotId = record.slot;
                part.Kind = CustomizationOptionKind.SkinnedPart;
                part.TargetRigId = manifest.rig_id;
                part.TargetVisualScale = Vector3.one * (species == "Human" ? 1f : .5f);
                part.FacialImpact = record.facial_impact == "OccludesExistingFace" ? CharacterFacialImpact.OccludesExistingFace : CharacterFacialImpact.None;
                var skins = new List<CharacterCustomizationPart.SkinnedRendererBinding>();
                var colours = new List<CharacterCustomizationPart.ColorChannelBinding>();
                var heads = new List<Renderer>();
                var conditional = new List<CharacterCustomizationPart.ConditionalRendererBinding>();
                var partBinds = BindMatrices(model);
                foreach (var pair in partBinds)
                    if (hostBinds.TryGetValue(pair.Key, out var hostMatrix))
                        Require(Close(hostMatrix, pair.Value, 2e-4f), "Part " + record.fbx + " bind pose of " + pair.Key + " differs from the production rig.");
                foreach (var rendererRecord in record.renderers)
                {
                    var matches = modelSkins.Where(item => item.name == rendererRecord.name).ToArray();
                    Require(matches.Length == 1, "Part " + record.fbx + " must contain exactly one skinned renderer " + rendererRecord.name);
                    var src = matches[0];
                    var child = new GameObject(rendererRecord.name);
                    child.transform.SetParent(root.transform, false);
                    var skin = child.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = src.sharedMesh;
                    skin.sharedMaterials = src.sharedMaterials.Select(material => SharedMaterial(material.name)).ToArray();
                    skin.localBounds = src.localBounds;
                    skin.quality = SkinQuality.Bone4;
                    skin.updateWhenOffscreen = false;
                    bool wing = rendererRecord.name.Contains("Wing");
                    skin.shadowCastingMode = wing ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    skin.receiveShadows = !wing;
                    skin.skinnedMotionVectors = true;
                    var paths = src.bones.Select(bone => RelativePath(model.transform, bone)).ToArray();
                    Require(paths.Length == src.sharedMesh.bindposes.Length && paths.Distinct().Count() == paths.Length,
                        "Part bones and bind poses disagree: " + record.fbx);
                    skins.Add(new CharacterCustomizationPart.SkinnedRendererBinding
                    {
                        Renderer = skin, RootBonePath = RelativePath(model.transform, src.rootBone), BonePaths = paths
                    });
                    if (rendererRecord.first_person_head) heads.Add(skin);
                    if (!string.IsNullOrEmpty(rendererRecord.hidden_when_slot_selected))
                        conditional.Add(new CharacterCustomizationPart.ConditionalRendererBinding
                        { Renderer = skin, HiddenWhenSlotSelected = rendererRecord.hidden_when_slot_selected });
                    foreach (var colour in (record.colors ?? Array.Empty<ColorRecord>()).Where(item => item.renderer == rendererRecord.name))
                    {
                        int index = Array.FindIndex(skin.sharedMaterials, material => material && material.name == colour.material);
                        Require(index >= 0, "Colour material " + colour.material + " missing on " + rendererRecord.name);
                        colours.Add(new CharacterCustomizationPart.ColorChannelBinding
                        {
                            Renderer = skin, MaterialIndex = index, ColorSlotId = colour.slot,
                            Shade = string.IsNullOrEmpty(colour.shade_reference) ? 0 :
                                ShadeBetween(SharedMaterial(colour.shade_reference).GetColor("_BaseColor"), skin.sharedMaterials[index].GetColor("_BaseColor")),
                            Alpha = colour.alpha > 0 ? colour.alpha : 0
                        });
                    }
                }
                part.SkinnedRenderers = skins.ToArray();
                part.ColorChannels = colours.ToArray();
                part.FirstPersonHeadRenderers = heads.ToArray();
                part.ConditionalRenderers = conditional.ToArray();
                string path = PartsRoot + "/" + species + "/" + record.slot + "__" + record.option + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Require(saved != null, "Could not save part prefab " + path);
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ImportPartModel(string source, string assetPath, Manifest manifest)
        {
            WriteIfChanged(assetPath, File.ReadAllBytes(source));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            Require(importer != null, "FBX did not produce a ModelImporter: " + assetPath);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name),
                    UpsertMaterial(material.name, manifest));
            importer.SaveAndReimport();
        }

        /// <summary>Production palette materials are shared by name; a new part material (hoodie strings aside,
        /// e.g. denim, sneakers, beanie, backpack, glasses, markings, leaf) is created with the production URP/Lit
        /// settings of CharacterContentBuilder (opaque, no emission).</summary>
        private static Material UpsertMaterial(string name, Manifest manifest)
        {
            string path = CharacterRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var entry = manifest.material_palette.FirstOrDefault(item => item.name == name);
            if (material != null && (entry == null || !entry.@new)) return material;
            Require(entry != null && entry.@new, "Production material missing for a part: " + name);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "URP Lit shader unavailable.");
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            var colour = entry.color; colour.a = 1;
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 1 - entry.roughness);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Surface", 0);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_BlendModePreserveSpecular", 0);
            material.SetFloat("_SpecularHighlights", 1);
            material.SetFloat("_EnvironmentReflections", 1);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            material.SetFloat("_ReceiveShadows", 1);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetFloat("_QueueOffset", 0);
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SharedMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CharacterRoot + "/Materials/" + name + ".mat");
            Require(material != null, "Shared material missing: " + name);
            return material;
        }

        private static float ShadeBetween(Color reference, Color shade)
        {
            float Luma(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
            float ratio = Luma(shade) / Mathf.Max(1e-4f, Luma(reference));
            return Mathf.Round(Mathf.Clamp(1 - ratio, 0, .9f) * 1000) / 1000;
        }

        /// <summary>Model-space bind matrix of every deforming bone (by path under the model root).</summary>
        private static Dictionary<string, Matrix4x4> BindMatrices(GameObject model)
        {
            var result = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            var rootInverse = model.transform.worldToLocalMatrix;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.sharedMesh) continue;
                var poses = skin.sharedMesh.bindposes;
                for (int i = 0; i < poses.Length && i < skin.bones.Length; i++)
                {
                    if (!skin.bones[i]) continue;
                    var matrix = rootInverse * skin.transform.localToWorldMatrix * poses[i].inverse;
                    string path = RelativePath(model.transform, skin.bones[i]);
                    if (!result.ContainsKey(path)) result.Add(path, matrix);
                }
            }
            return result;
        }

        private static bool Close(Matrix4x4 a, Matrix4x4 b, float tolerance)
        {
            for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > tolerance) return false;
            return true;
        }

        // ------------------------------------------------------------------ catalog
        private static CharacterCustomizationCatalog WriteCatalog(SlotSpec[] definition, Dictionary<string, GameObject> prefabs)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCustomizationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCustomizationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var previousThumbnails = (catalog.Options ?? Array.Empty<CharacterCustomizationCatalog.OptionDefinition>())
                .Where(item => item != null && item.Thumbnail)
                .GroupBy(item => item.SlotId + "/" + item.OptionId).ToDictionary(group => group.Key, group => group.First().Thumbnail);
            catalog.name = Path.GetFileNameWithoutExtension(CatalogPath);
            catalog.CatalogId = CatalogId;
            catalog.Revision = CatalogRevision;
            catalog.Slots = definition.Select(slot => new CharacterCustomizationCatalog.SlotDefinition
            {
                Role = slot.Role, SlotId = slot.Id, Label = slot.Label, WireSlotId = slot.Wire, Required = slot.Required,
                AllowsNone = slot.AllowsNone, IsBaseSlot = slot.IsBase, DefaultOptionId = slot.Default, CompatibilityFamily = string.Empty
            }).ToArray();
            var options = new List<CharacterCustomizationCatalog.OptionDefinition>();
            foreach (var slot in definition)
                foreach (var option in slot.Options)
                {
                    var item = new CharacterCustomizationCatalog.OptionDefinition
                    {
                        Role = slot.Role, SlotId = slot.Id, OptionId = option.Id, WireOptionId = option.Wire, Kind = option.Kind,
                        Label = option.Label, CompatibleBaseOptionIds = Array.Empty<string>(),
                        CompatibilityTags = Array.Empty<string>(), IncompatibleSlotIds = Array.Empty<string>()
                    };
                    if (option.Kind == CustomizationOptionKind.Color)
                    {
                        Require(ColorUtility.TryParseHtmlString(option.Hex, out var colour), "Bad swatch " + option.Hex);
                        item.HasSwatch = true;
                        item.Swatch = (Color32)colour;
                    }
                    if (option.Kind == CustomizationOptionKind.SkinnedPart)
                    {
                        var prefab = prefabs[slot.Id + "/" + option.Id];
                        item.RuntimeAsset = prefab;
                        item.AssetId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
                    }
                    if (previousThumbnails.TryGetValue(slot.Id + "/" + option.Id, out var thumbnail)) item.Thumbnail = thumbnail;
                    options.Add(item);
                }
            catalog.Options = options.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Require(CharacterCustomizationCatalogValidator.Validate(catalog, out var snapshot, out var errors),
                "Production catalog invalid: " + string.Join("; ", errors ?? Array.Empty<string>()));
            Require(snapshot.RuntimeReady, "Production catalog is not runtime ready.");
            Require(LegacyAppearanceMapper.TryCreate(LegacyMappings(), snapshot, out _, out var legacyError), legacyError);
            return catalog;
        }

        // ------------------------------------------------------------------ hosts
        private static string InstallHost(string prefabName)
        {
            string path = CharacterRoot + "/Prefabs/" + prefabName + ".prefab";
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, "Missing host prefab " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CharacterView>();
                Require(view != null && view.Animator != null && view.VisualRoot != null, "Host prefab has no CharacterView rig: " + path);
                bool human = prefabName.StartsWith("LMS_Human", StringComparison.Ordinal);
                var host = root.GetComponent<CharacterCustomizationHost>() ?? root.AddComponent<CharacterCustomizationHost>();
                if (!root.GetComponent<CharacterModularVisualAssembler>()) root.AddComponent<CharacterModularVisualAssembler>();
                var parts = view.VisualRoot.Find("CustomizationParts");
                if (parts == null)
                {
                    parts = new GameObject("CustomizationParts").transform;
                    parts.SetParent(view.VisualRoot, false);
                }
                parts.gameObject.layer = root.layer;
                var renderers = view.Animator.GetComponentsInChildren<Renderer>(true);
                Renderer Named(string name)
                {
                    var found = renderers.Where(item => item.name == name).ToArray();
                    Require(found.Length == 1, prefabName + " needs exactly one authored renderer " + name);
                    return found[0];
                }
                host.Role = human ? CustomizationRole.Human : CustomizationRole.Mosquito;
                host.View = view;
                host.RigId = human ? HumanRigId : MosquitoRigId;
                host.ExpectedVisualScale = view.VisualRoot.localScale;
                host.PartsRoot = parts;
                if (human)
                {
                    // The authored head (blink shape keys, eyes, sideburns, neck) and hands stay: only the body clothes
                    // and the nightcap are replaced by catalog parts.
                    host.OwnedBaseRenderers = new[] { Named("HumanBody"), Named("HumanNightcap") };
                    var channels = new List<CharacterCustomizationPart.ColorChannelBinding>();
                    foreach (var renderer in new[] { Named("HumanHead"), Named("HandSkin.L"), Named("HandSkin.R") })
                        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                        {
                            string material = renderer.sharedMaterials[i] ? renderer.sharedMaterials[i].name : string.Empty;
                            if (material == "Human_Skin")
                                channels.Add(new CharacterCustomizationPart.ColorChannelBinding { Renderer = renderer, MaterialIndex = i, ColorSlotId = "human.skin" });
                            if (material == "Human_Hair")
                                channels.Add(new CharacterCustomizationPart.ColorChannelBinding { Renderer = renderer, MaterialIndex = i, ColorSlotId = "human.hair_color" });
                        }
                    Require(channels.Count(item => item.ColorSlotId == "human.skin") == 3 && channels.Any(item => item.ColorSlotId == "human.hair_color"),
                        prefabName + " authored head/hands lack the skin or hair materials.");
                    host.ColorChannels = channels.ToArray();
                }
                else
                {
                    host.OwnedBaseRenderers = new[] { Named("MosquitoSkin"), Named("MosquitoMembranes"), Named("MosquitoVeins") };
                    host.ColorChannels = Array.Empty<CharacterCustomizationPart.ColorChannelBinding>();
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Require(saved != null, "Could not save host prefab " + path);
                return prefabName + " " + host.RigId;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ verification
        /// <summary>Every option, alone over the defaults, applies on every host prefab (editor instances).</summary>
        private static CustomizationCatalogSnapshot VerifyEveryOption(CharacterCustomizationCatalog catalog)
        {
            Require(catalog.TryCreateSnapshot(out var snapshot, out var errors), string.Join("; ", errors));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string prefabName in HostPrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "/Prefabs/" + prefabName + ".prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    var view = instance.GetComponent<CharacterView>();
                    var assembler = instance.GetComponent<CharacterModularVisualAssembler>();
                    var role = instance.GetComponent<CharacterCustomizationHost>().Role;
                    foreach (var slot in snapshot.Slots.Where(item => item.Role == role))
                        foreach (var option in slot.Options)
                        {
                            var selection = snapshot.DefaultSelection();
                            selection.For(role).SetOption(slot.SlotId, option.OptionId);
                            Require(assembler.TryApply(view, catalog, selection, role, out var error),
                                prefabName + " cannot apply " + slot.SlotId + "/" + option.OptionId + ": " + error);
                        }
                    Require(assembler.TryApply(view, catalog, snapshot.DefaultSelection(), role, out var defaultError), defaultError);
                    assembler.ClearAppliedParts();
                    Object.DestroyImmediate(instance);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var app = new GameObject("Modular customization provider check") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var provider = app.AddComponent<ModularCustomizationRuntimeProvider>();
                provider.Catalog = catalog;
                provider.LegacyMappings = LegacyMappings();
                Require(provider.TryResolve(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "/Prefabs/LMS_Human.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "/Prefabs/LMS_Mosquito.prefab"), out _, out var reason),
                    "Provider does not resolve: " + reason);
            }
            finally { Object.DestroyImmediate(app); }
            return snapshot;
        }

        // ------------------------------------------------------------------ scene provider
        private static CharacterCustomizationCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCustomizationCatalog>(CatalogPath);
            Require(catalog != null, "Catalog asset missing: " + CatalogPath);
            return catalog;
        }

        private static bool InstallProvider()
        {
            var previous = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var catalog = LoadCatalog();
            var apps = Object.FindObjectsByType<AlfaApplication>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Require(apps.Length == 1, "The build scene must contain exactly one AlfaApplication.");
            var app = apps[0];
            var provider = app.GetComponent<ModularCustomizationRuntimeProvider>();
            var mappings = LegacyMappings();
            bool changed = provider == null || provider.Catalog != catalog || app.ModularCustomizationProvider != provider ||
                           provider.LegacyMappings == null || provider.LegacyMappings.Length != mappings.Length ||
                           provider.LegacyMappings.Where((item, i) => item == null || item.Kind != mappings[i].Kind ||
                               item.LegacyId != mappings[i].LegacyId || item.SlotId != mappings[i].SlotId || item.OptionId != mappings[i].OptionId).Any();
            if (changed)
            {
                if (provider == null) provider = app.gameObject.AddComponent<ModularCustomizationRuntimeProvider>();
                provider.Catalog = catalog;
                provider.LegacyMappings = mappings;
                app.ModularCustomizationProvider = provider;
                EditorUtility.SetDirty(provider);
                EditorUtility.SetDirty(app);
                EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene), "Could not save " + ScenePath);
            }
            Require(provider.TryResolve(app.HumanPrefab, app.MosquitoPrefab, out _, out var reason),
                "Scene provider does not resolve with the scene prefabs: " + reason);
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            return true;
        }

        // ------------------------------------------------------------------ thumbnails
        private static void AssignThumbnails(CharacterCustomizationCatalog catalog)
        {
            bool changed = false;
            foreach (var option in catalog.Options)
            {
                if (option.Kind == CustomizationOptionKind.Color) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ThumbnailPath(option.SlotId, option.OptionId));
                if (sprite != null && option.Thumbnail != sprite) { option.Thumbnail = sprite; changed = true; }
            }
            if (changed) { EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); }
        }

        private static string ThumbnailPath(string slot, string option) => ThumbnailsRoot + "/" + slot + "__" + option + ".png";

        private struct Framing
        {
            public Vector3 Centre;
            public float Half, Yaw, Pitch;
            public Framing(float x, float y, float z, float half, float yaw, float pitch)
            { Centre = new Vector3(x, y, z); Half = half; Yaw = yaw; Pitch = pitch; }
        }

        /// <summary>Region of the slot in actor space (+Z forward, +Y up; the mosquito at its 0.5 visual scale).</summary>
        private static Framing FramingFor(string slot)
        {
            switch (slot)
            {
                case "human.hair": return new Framing(0, 1.585f, .01f, .255f, 25, 8);
                case "human.headwear": return new Framing(0, 1.60f, .01f, .27f, -28, 8);
                case "human.glasses": return new Framing(0, 1.535f, .04f, .205f, 20, 3);
                case "human.top": return new Framing(0, 1.03f, 0, .42f, 20, 5);
                case "human.bottom": return new Framing(0, .45f, 0, .48f, 20, 5);
                case "human.footwear": return new Framing(0, .10f, .03f, .21f, 32, 24);
                case "human.back": return new Framing(0, 1.04f, 0, .42f, 155, 8);
                case "mosquito.wings": return new Framing(0, .05f, -.02f, .118f, 128, 30);
                case "mosquito.proboscis": return new Framing(0, .028f, .055f, .08f, 72, 6);
                case "mosquito.markings": return new Framing(0, .02f, -.035f, .105f, 138, 30);
                case "mosquito.accessory": return new Framing(0, .062f, .022f, .066f, 30, 20);
                default: return new Framing(0, .9f, 0, .95f, 25, 6);
            }
        }

        private static Vector3 Orbit(float yaw, float pitch)
        {
            var horizontal = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float p = pitch * Mathf.Deg2Rad;
            return (horizontal * Mathf.Cos(p) + Vector3.up * Mathf.Sin(p)).normalized;
        }

        /// <summary>
        /// One 256 px transparent render per option (visual and none), the character in its idle pose with the
        /// defaults and that option (hair without a hat so the style shows), framed on the slot's region and lit
        /// by a warm key, on the viewer layer only. Requires a graphics device.
        /// </summary>
        public static string[] BakeThumbnails(CharacterCustomizationCatalog catalog)
        {
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Thumbnails need a graphics device (no -nographics).");
            Require(catalog.TryCreateSnapshot(out var snapshot, out var errors), string.Join("; ", errors));
            var written = new List<string>();
            var original = SceneManager.GetActiveScene();
            // A batch editor starts on an untitled scene that cannot host an additive one: replace it instead.
            bool additive = !string.IsNullOrEmpty(original.path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            catalog = LoadCatalog();
            var foreign = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(l => l.enabled && l.gameObject.scene != scene).ToArray();
            var ambientMode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            try
            {
                foreach (var other in foreign) other.enabled = false;
                SceneManager.SetActiveScene(scene);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.46f, .48f, .56f);
                var key = new GameObject("ThumbKey").AddComponent<Light>();
                key.type = LightType.Directional; key.intensity = 1.15f; key.color = new Color(1f, .93f, .82f);
                key.shadows = LightShadows.None; key.cullingMask = 1 << ViewerLayer;
                var fill = new GameObject("ThumbFill").AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = .45f; fill.color = new Color(.78f, .84f, 1f);
                fill.shadows = LightShadows.None; fill.cullingMask = 1 << ViewerLayer;
                var camera = new GameObject("ThumbCamera").AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.cullingMask = 1 << ViewerLayer;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, 0);
                camera.nearClipPlane = .01f; camera.farClipPlane = 20f;
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                foreach (var role in new[] { CustomizationRole.Human, CustomizationRole.Mosquito })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "/Prefabs/" +
                        (role == CustomizationRole.Human ? "LMS_Human" : "LMS_Mosquito") + ".prefab");
                    var actor = Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var view = actor.GetComponent<CharacterView>();
                        var assembler = actor.GetComponent<CharacterModularVisualAssembler>();
                        foreach (var t in actor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewerLayer;
                        var idle = view.Motions.First(m => m.StateName.EndsWith(".Idle", StringComparison.Ordinal));
                        var clip = view.Animator.runtimeAnimatorController.animationClips.First(c => c.name == idle.ClipName);
                        foreach (var slot in snapshot.Slots.Where(item => item.Role == role && !item.IsBaseSlot &&
                                     item.Options.Any(option => option.Kind != CustomizationOptionKind.Color)))
                            foreach (var option in slot.Options)
                            {
                                var selection = snapshot.DefaultSelection();
                                selection.For(role).SetOption(slot.SlotId, option.OptionId);
                                if (slot.SlotId == "human.hair") selection.For(role).SetOption("human.headwear", "none");
                                Require(assembler.TryApply(view, catalog, selection, role, out var error), error);
                                clip.SampleAnimation(view.Animator.gameObject, 0f);
                                view.RefreshAnchors();
                                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                { skin.updateWhenOffscreen = true; skin.forceMatrixRecalculationPerRender = true; }
                                var framing = FramingFor(slot.SlotId);
                                var offset = Orbit(framing.Yaw, framing.Pitch);
                                camera.orthographicSize = framing.Half;
                                camera.transform.position = framing.Centre + offset * 6f;
                                camera.transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
                                key.transform.rotation = Quaternion.LookRotation(-Orbit(framing.Yaw - 40f, 42f), Vector3.up);
                                fill.transform.rotation = Quaternion.LookRotation(-Orbit(framing.Yaw + 70f, 12f), Vector3.up);
                                string path = ThumbnailPath(slot.SlotId, option.OptionId);
                                RenderPng(camera, path);
                                written.Add(path);
                            }
                        assembler.ClearAppliedParts();
                    }
                    finally { Object.DestroyImmediate(actor); }
                }
            }
            finally
            {
                foreach (var other in foreign) if (other) other.enabled = true;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                if (additive)
                {
                    if (original.IsValid()) SceneManager.SetActiveScene(original);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            foreach (string path in written)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return written.ToArray();
        }

        private static void RenderPng(Camera camera, string path)
        {
            const int size = 256, supersample = 2;
            var target = new RenderTexture(size * supersample, size * supersample, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            var small = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
            small.Create();
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                Graphics.Blit(target, small);
                RenderTexture.active = small;
                image = new Texture2D(size, size, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.GetFullPath(path), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image) Object.DestroyImmediate(image);
                target.Release(); Object.DestroyImmediate(target);
                small.Release(); Object.DestroyImmediate(small);
            }
        }

        // ------------------------------------------------------------------ utilities
        private static string RelativePath(Transform root, Transform target)
        {
            Require(target != null && target.IsChildOf(root), "Bone outside the model root.");
            var segments = new Stack<string>();
            for (var current = target; current != null && current != root; current = current.parent) segments.Push(current.name);
            return string.Join("/", segments.ToArray());
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void WriteIfChanged(string assetPath, byte[] bytes)
        {
            string full = Path.GetFullPath(assetPath);
            if (File.Exists(full) && File.ReadAllBytes(full).SequenceEqual(bytes)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("LMS_MODULAR_CUSTOMIZATION_FAILED " + message);
        }
    }
}
