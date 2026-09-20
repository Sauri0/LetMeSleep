#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class ModularCustomizationUiPlayModeTests
    {
        private sealed class Actions : IMenuActions, IModularCustomizationActions
        {
            public int BasicPreviewCount;
            public int ModularPreviewCount;
            public int ModularSaveCount;
            public AppearanceSelection LastModularPreview;
            public AppearanceSelection LastModularSave;
            public AlfaRole LastEditedRole;

            public void CreateRoom(string playerName) { }
            public void JoinRoom(string playerName, string normalizedRoomCode) { }
            public void CancelOnline() { }
            public void CancelTraining() { }
            public void CopyRoomCode(string groupedRoomCode) { }
            public void SetReady(bool ready) { }
            public void SetHumanCount(int? humanCount) { }
            public void StartRound() { }
            public void LeaveRoom() { }
            public void StartTraining(AlfaRole role, string modeId, string mapId) { }
            public void PreviewCustomization(BasicCustomizationDraft draft) { BasicPreviewCount++; }
            public void SaveCustomization(BasicCustomizationDraft draft) { }
            public void ApplySettings(AlfaSettingsDraft draft) { }
            public void SetGameplayInputBlocked(bool blocked) { }
            public void ResumeGame() { }
            public void ReturnToLobby() { }
            public void SetLobbyExploration(bool exploring) { }
            public void QuitGame() { }

            public void PreviewModularCustomization(AppearanceSelection draft, AlfaRole editedRole)
            {
                ModularPreviewCount++;
                LastModularPreview = draft.Copy();
                LastEditedRole = editedRole;
            }

            public void SaveModularCustomization(AppearanceSelection draft, AlfaRole editedRole)
            {
                ModularSaveCount++;
                LastModularSave = draft.Copy();
                LastEditedRole = editedRole;
            }
        }

        private Actions actions;
        private AlfaUiController ui;
        private Texture2D thumbnailTexture;
        private Sprite thumbnail;

        [SetUp]
        public void SetUp()
        {
            actions = new Actions();
            ui = AlfaUiRuntime.Create(actions, new AlfaUiDependencies(persistentAcrossScenes: false));
        }

        [TearDown]
        public void TearDown()
        {
            if (ui != null) Object.DestroyImmediate(ui.gameObject);
            if (thumbnail != null) Object.DestroyImmediate(thumbnail);
            if (thumbnailTexture != null) Object.DestroyImmediate(thumbnailTexture);
        }

        [UnityTest]
        public IEnumerator ModularOptionChangesKeepBothLoadoutsAndUseOnlyTheModularCallback()
        {
            var snapshot = Snapshot(includeHiddenHumanSlot: false);
            var published = snapshot.DefaultSelection();
            thumbnailTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            thumbnailTexture.SetPixels(new[] { Color.white, Color.cyan, Color.cyan, Color.white }); thumbnailTexture.Apply();
            thumbnail = Sprite.Create(thumbnailTexture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
            ui.PresentCustomization(new CustomizationUiState(snapshot, published, published, AlfaRole.Human,
                thumbnailResolver: (slotId, optionId) => slotId == "mosquito.base" ? thumbnail : null));
            ui.ShowCustomization();
            yield return null;

            Invoke(ui, "SelectModularCategory", "human.hair");
            Assert.That(Find(ui.transform, "ColorSwatch"), Is.Not.Null, "Colour options render their catalogued swatch.");
            Invoke(ui, "SetModularCustomizationOption", "human.hair", "long");
            Invoke(ui, "SetCustomizationRole", AlfaRole.Mosquito);
            Invoke(ui, "SetModularCustomizationOption", "mosquito.base", "body-b");

            Assert.That(actions.ModularPreviewCount, Is.EqualTo(3), "Changing the tab informs only the modular preview provider.");
            Assert.That(actions.BasicPreviewCount, Is.Zero);
            Assert.That(actions.LastEditedRole, Is.EqualTo(AlfaRole.Mosquito));
            AssertOption(actions.LastModularPreview, CustomizationRole.Human, "human.hair", "long");
            AssertOption(actions.LastModularPreview, CustomizationRole.Mosquito, "mosquito.base", "body-b");
            Assert.That(CustomizationTextNamed(ui, "Status").text, Does.Contain("llegará cuando estén listas"));
            Assert.That(Find(ui.transform, "Thumbnail").GetComponent<UnityEngine.UI.Image>().sprite, Is.SameAs(thumbnail),
                "Only the supplied, real thumbnail is rendered for a visual option.");

            Invoke(ui, "SaveCustomization");
            Assert.That(actions.ModularSaveCount, Is.EqualTo(1));
            AssertOption(actions.LastModularSave, CustomizationRole.Human, "human.hair", "long");
            AssertOption(actions.LastModularSave, CustomizationRole.Mosquito, "mosquito.base", "body-b");
        }

        [UnityTest]
        public IEnumerator RepaintDoesNotReplaceTheCompleteModularUndoBaseline()
        {
            var snapshot = Snapshot(includeHiddenHumanSlot: false);
            var baseline = snapshot.DefaultSelection();
            ui.PresentCustomization(new CustomizationUiState(snapshot, baseline, baseline, AlfaRole.Human));
            ui.ShowCustomization();
            yield return null;

            var repaintedDraft = baseline.Copy();
            repaintedDraft.Human.SetOption("human.hair", "long");
            repaintedDraft.Mosquito.SetOption("mosquito.base", "body-b");
            ui.PresentCustomization(new CustomizationUiState(snapshot, baseline, repaintedDraft, AlfaRole.Mosquito));
            Invoke(ui, "ResetCustomization");

            Assert.That(actions.ModularPreviewCount, Is.EqualTo(1));
            Assert.That(actions.LastEditedRole, Is.EqualTo(AlfaRole.Mosquito), "Undo keeps the screen tab separate from the saved selection.");
            AssertOption(actions.LastModularPreview, CustomizationRole.Human, "human.hair", "short");
            AssertOption(actions.LastModularPreview, CustomizationRole.Mosquito, "mosquito.base", "body-a");
        }

        [Test]
        public void DynamicCategoriesHideUnlabelledSlotsAndNeverExposeSlotIds()
        {
            var snapshot = Snapshot(includeHiddenHumanSlot: true);
            var selection = snapshot.DefaultSelection();
            ui.PresentCustomization(new CustomizationUiState(snapshot, selection, selection, AlfaRole.Human));
            ui.ShowCustomization();

            Assert.That(Find(ui.transform, "ModularCategory_1"), Is.Not.Null, "The labelled category is available.");
            Assert.That(Find(ui.transform, "ModularCategory_3"), Is.Null, "A slot without an approved label stays hidden.");
            Assert.That(ui.GetComponentsInChildren<TextMeshProUGUI>(true).Any(item => item.text.Contains("human.hidden")), Is.False);
            Assert.That(CustomizationTextNamed(ui, "CategoryTitle").text, Does.Contain("HUMANO"));
        }

        [Test]
        public void BasicCustomizationPresentationAndCallbacksRemainUntouched()
        {
            var skin = new[] { new NamedColorOption("warm", "CÁLIDO", Color.white) };
            var pajamas = new[] { new NamedColorOption("blue", "AZUL", Color.blue) };
            var mosquito = new[] { new NamedColorOption("red", "ROJO", Color.red) };
            var basic = new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
            ui.PresentCustomization(new CustomizationUiState(skin, pajamas, mosquito, basic));
            ui.ShowCustomization();

            Invoke(ui, "SetCustomizationRole", AlfaRole.Mosquito);

            Assert.That(Find(ui.transform, "HumanFields").gameObject.activeSelf, Is.False);
            Assert.That(Find(ui.transform, "MosquitoFields").gameObject.activeSelf, Is.True);
            Assert.That(Find(ui.transform, "ModularFields").gameObject.activeSelf, Is.False);
            Assert.That(actions.BasicPreviewCount, Is.EqualTo(1));
            Assert.That(actions.ModularPreviewCount, Is.Zero);
        }

        [Test]
        public void RetainedCustomizationIsReadOnlyButCanReturn()
        {
            var skin = new[] { new NamedColorOption("warm", "CÁLIDO", Color.white) };
            var pajamas = new[] { new NamedColorOption("blue", "AZUL", Color.blue) };
            var mosquito = new[] { new NamedColorOption("red", "ROJO", Color.red) };
            var basic = new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
            ui.PresentCustomization(new CustomizationUiState(skin, pajamas, mosquito, basic,
                message: "Esta personalización necesita contenido de esta versión.", isReadOnly: true));
            ui.ShowCustomization();

            Assert.That(Find(ui.transform, "HumanFields").gameObject.activeSelf, Is.False);
            Assert.That(Find(ui.transform, "MosquitoFields").gameObject.activeSelf, Is.False);
            Assert.That(Find(ui.transform, "ModularFields").gameObject.activeSelf, Is.False);
            Assert.That(Find(ui.transform, "CustomizationHumanButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(Find(ui.transform, "CustomizationMosquitoButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(Find(ui.transform, "CustomizationSaveButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(Find(ui.transform, "CustomizationResetButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(Find(ui.transform, "CustomizationBackButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
            Assert.That(CustomizationTextNamed(ui, "Status").text, Does.Contain("necesita contenido"));

            Invoke(ui, "SetCustomizationRole", AlfaRole.Mosquito);
            Invoke(ui, "SaveCustomization");
            Invoke(ui, "ResetCustomization");
            Assert.That(actions.BasicPreviewCount, Is.Zero);
            Assert.That(actions.ModularPreviewCount, Is.Zero);
            Assert.That(actions.ModularSaveCount, Is.Zero);

            Invoke(ui, "CloseCustomization");
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.MainMenu));
        }

        [UnityTest]
        public IEnumerator SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-modularCustomizationUiEvidence");
            if (option < 0 || option + 1 >= args.Length)
                Assert.Ignore("Requires an explicit modular customization evidence output directory.");
            string output = Path.GetFullPath(args[option + 1]);
            string validationRoot = Path.GetFullPath("N:/LetMeSleep/Validation/V020").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(output.StartsWith(validationRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Directory.CreateDirectory(output);

            var snapshot = EvidenceSnapshot();
            var selection = snapshot.DefaultSelection();
            ui.PresentCustomization(new CustomizationUiState(snapshot, selection, selection, AlfaRole.Human));
            ui.ShowCustomization();
            var camera = new GameObject("ModularCustomizationEvidenceCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.015f, .025f, .05f, 1f);
            var canvas = ui.GetComponent<Canvas>();
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            var receipt = new StringBuilder("PASS Unity " + Application.unityVersion + "\n");
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                yield return null;
                Invoke(ui, "SelectModularCategory", "human.style_2");
                ScrollToBottom(Find(ui.transform, "CategoryScroll"));
                yield return null;
                yield return new WaitForSecondsRealtime(.2f);
                CaptureModularCanvas(output, camera, canvas, receipt, 1280, 720, AlfaRole.Human, true);
                Invoke(ui, "SetCustomizationRole", AlfaRole.Mosquito);
                yield return null;
                yield return new WaitForSecondsRealtime(.2f);
                CaptureModularCanvas(output, camera, canvas, receipt, 1920, 1080, AlfaRole.Mosquito, false);
                receipt.Append("Synthetic catalog and local canvas only; no production catalogue, visual assembly, peer or WAN claim.\n");
                File.WriteAllText(Path.Combine(output, "modular-customization-ui-layout.txt"), receipt.ToString());
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                Object.Destroy(camera.gameObject);
            }
        }

        private void CaptureModularCanvas(string output, Camera camera, Canvas canvas, StringBuilder receipt, int width, int height,
            AlfaRole expectedRole, bool requiresColorSwatch)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target; camera.aspect = width / (float)height;
            try
            {
                Canvas.ForceUpdateCanvases();
                Transform view = Find(ui.transform, "CustomizationView");
                Transform options = Find(view, "OptionsPanel");
                var fields = Find(options, "ModularFields") as RectTransform;
                var footer = Find(options, "Actions") as RectTransform;
                Assert.That(fields, Is.Not.Null); Assert.That(footer, Is.Not.Null);
                Assert.That(CustomizationTextNamed(ui, "CategoryTitle").text,
                    Does.Contain(expectedRole == AlfaRole.Human ? "HUMANO" : "MOSQUITO"));
                if (expectedRole == AlfaRole.Human)
                    AssertScrollItemVisible(Find(options, "CategoryScroll"), Find(options, "ModularCategory_10"));
                else
                    AssertScrollItemVisible(Find(options, "CategoryScroll"), Find(options, "ModularCategory_11"));
                if (requiresColorSwatch)
                    Assert.That(Find(options, "ColorSwatch"), Is.Not.Null,
                        "The selected long-label category exposes its catalogued color state.");
                var fieldCorners = new Vector3[4]; var footerCorners = new Vector3[4];
                fields.GetWorldCorners(fieldCorners); footer.GetWorldCorners(footerCorners);
                Assert.That(fieldCorners[0].y, Is.GreaterThanOrEqualTo(footerCorners[1].y - .01f),
                    "The modular scroll area overlaps the footer at " + width + "x" + height);
                foreach (var label in options.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    label.ForceMeshUpdate(false, false);
                    Assert.That(label.isTextOverflowing, Is.False, label.name + " overflows at " + width + "x" + height);
                }
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, "modular-customization-" + width + "x" + height + ".png"), pixels.EncodeToPNG());
                receipt.AppendLine(width + "x" + height + ": role=" + expectedRole + " categories=10 labels=long scroll=" +
                    (Find(options, "CategoryScroll") != null && Find(options, "OptionsScroll") != null) +
                    " colorState=" + requiresColorSwatch);
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target);
            }
        }

        private static void ScrollToBottom(Transform scroll)
        {
            Assert.That(scroll, Is.Not.Null);
            var scrollRect = scroll.GetComponent<UnityEngine.UI.ScrollRect>();
            Assert.That(scrollRect, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertScrollItemVisible(Transform scroll, Transform item)
        {
            Assert.That(scroll, Is.Not.Null);
            Assert.That(item, Is.Not.Null, "The final category is present in the scroll content.");
            var viewport = scroll.GetComponent<UnityEngine.UI.ScrollRect>().viewport;
            Assert.That(viewport, Is.Not.Null);
            var viewportCorners = new Vector3[4]; var itemCorners = new Vector3[4];
            viewport.GetWorldCorners(viewportCorners); item.GetComponent<RectTransform>().GetWorldCorners(itemCorners);
            Assert.That(itemCorners[1].y, Is.LessThanOrEqualTo(viewportCorners[1].y + .01f), "Final category is below the visible viewport.");
            Assert.That(itemCorners[0].y, Is.GreaterThanOrEqualTo(viewportCorners[0].y - .01f), "Final category is above the visible viewport.");
        }

        private static CustomizationCatalogSnapshot EvidenceSnapshot()
        {
            var slots = new System.Collections.Generic.List<CustomizationSlotRecord>();
            var options = new System.Collections.Generic.List<CustomizationOptionRecord>();
            for (byte slotCode = 1; slotCode <= 10; slotCode++)
            {
                string slotId = slotCode == 1 ? "human.base" : "human.style_" + slotCode;
                string defaultOption = slotCode == 1 ? "base-a" : "option-a";
                slots.Add(new CustomizationSlotRecord
                {
                    Role = CustomizationRole.Human, SlotId = slotId,
                    Label = "CATEGORÍA " + slotCode + " CON NOMBRE MUY LARGO", WireSlotId = slotCode,
                    Required = slotCode == 1, IsBaseSlot = slotCode == 1, DefaultOptionId = defaultOption
                });
                if (slotCode == 1) options.Add(Visual(CustomizationRole.Human, slotId, defaultOption, 1));
                else
                {
                    options.Add(new CustomizationOptionRecord { Role = CustomizationRole.Human, SlotId = slotId, OptionId = "option-a",
                        WireOptionId = 1, Kind = CustomizationOptionKind.Color, Label = "OPCIÓN A CON NOMBRE MUY LARGO", HasSwatch = true, SwatchRgba = 0xC87952FF });
                    options.Add(new CustomizationOptionRecord { Role = CustomizationRole.Human, SlotId = slotId, OptionId = "option-b",
                        WireOptionId = 2, Kind = CustomizationOptionKind.Color, Label = "OPCIÓN B CON NOMBRE MUY LARGO", HasSwatch = true, SwatchRgba = 0x513624FF });
                }
            }
            slots.Add(new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.base", Label = "CUERPO",
                WireSlotId = 11, Required = true, IsBaseSlot = true, DefaultOptionId = "body-a" });
            options.Add(Visual(CustomizationRole.Mosquito, "mosquito.base", "body-a", 1));
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.ui.evidence", 1, slots, options, out var snapshot, out var errors), Is.True,
                string.Join("\n", errors));
            return snapshot;
        }

        private static CustomizationCatalogSnapshot Snapshot(bool includeHiddenHumanSlot)
        {
            var slots = new[]
            {
                new CustomizationSlotRecord { Role = CustomizationRole.Human, SlotId = "human.base", Label = "BASE", WireSlotId = 1,
                    Required = true, IsBaseSlot = true, DefaultOptionId = "base-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Human, SlotId = "human.hair", Label = "CABELLO", WireSlotId = 2,
                    DefaultOptionId = "short" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.base", Label = "CUERPO", WireSlotId = 4,
                    Required = true, IsBaseSlot = true, DefaultOptionId = "body-a" }
            }.ToList();
            var options = new[]
            {
                Visual(CustomizationRole.Human, "human.base", "base-a", 1),
                new CustomizationOptionRecord { Role = CustomizationRole.Human, SlotId = "human.hair", OptionId = "short", WireOptionId = 1,
                    Kind = CustomizationOptionKind.Color, Label = "CORTO", HasSwatch = true, SwatchRgba = 0xC87952FF },
                new CustomizationOptionRecord { Role = CustomizationRole.Human, SlotId = "human.hair", OptionId = "long", WireOptionId = 2,
                    Kind = CustomizationOptionKind.Color, Label = "LARGO", HasSwatch = true, SwatchRgba = 0x513624FF },
                Visual(CustomizationRole.Mosquito, "mosquito.base", "body-a", 1),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "body-b", 2)
            }.ToList();
            if (includeHiddenHumanSlot)
            {
                slots.Add(new CustomizationSlotRecord { Role = CustomizationRole.Human, SlotId = "human.hidden", Label = string.Empty,
                    WireSlotId = 3, DefaultOptionId = "internal" });
                options.Add(new CustomizationOptionRecord { Role = CustomizationRole.Human, SlotId = "human.hidden", OptionId = "internal",
                    WireOptionId = 1, Kind = CustomizationOptionKind.Color, Label = string.Empty, HasSwatch = true });
            }
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.ui.synthetic", 1, slots, options, out var snapshot, out var errors), Is.True,
                string.Join("\n", errors));
            return snapshot;
        }

        private static CustomizationOptionRecord Visual(CustomizationRole role, string slotId, string optionId, ushort wireOptionId) =>
            new CustomizationOptionRecord { Role = role, SlotId = slotId, OptionId = optionId, WireOptionId = wireOptionId,
                Kind = CustomizationOptionKind.SkinnedPart, Label = optionId.ToUpperInvariant(), AssetId = "synthetic-" + optionId, HasRuntimeAsset = true };

        private static void AssertOption(AppearanceSelection selection, CustomizationRole role, string slotId, string optionId)
        {
            Assert.That(selection, Is.Not.Null);
            Assert.That(selection.For(role).OptionFor(slotId), Is.EqualTo(optionId));
        }

        private static void Invoke(object target, string method, params object[] arguments)
        {
            var types = arguments.Select(value => value.GetType()).ToArray();
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, types, null);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, arguments);
        }

        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == name);

        private static TextMeshProUGUI CustomizationTextNamed(AlfaUiController controller, string name)
        {
            var view = Find(controller.transform, "CustomizationView");
            return view.GetComponentsInChildren<TextMeshProUGUI>(true).First(item => item.name == name);
        }
    }
}
#endif
