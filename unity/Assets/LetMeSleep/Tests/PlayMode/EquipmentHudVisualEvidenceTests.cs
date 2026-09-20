using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class EquipmentHudVisualEvidenceTests
    {
        private sealed class Actions : IMenuActions
        {
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
            public void PreviewCustomization(BasicCustomizationDraft draft) { }
            public void SaveCustomization(BasicCustomizationDraft draft) { }
            public void ApplySettings(AlfaSettingsDraft draft) { }
            public void SetGameplayInputBlocked(bool blocked) { }
            public void ResumeGame() { }
            public void ReturnToLobby() { }
            public void SetLobbyExploration(bool exploring) { }
            public void QuitGame() { }
        }

        private AlfaUiController ui;
        private Camera camera;
        private readonly StringBuilder receipt = new StringBuilder();

        [UnityTest]
        public IEnumerator RealCanvasRendersEquipmentHudAt720And1080WithoutOverflow()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-equipmentHudReview");
            if (option < 0 || option + 1 >= args.Length) Assert.Ignore("Requires an explicit equipment HUD evidence output directory.");
            string output = args[option + 1]; Directory.CreateDirectory(output);

            camera = new GameObject("EquipmentHudEvidenceCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .025f, .05f, 1f);
            ui = AlfaUiRuntime.Create(new Actions());
            var canvas = ui.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            ui.ShowGameplay(true);
            PresentChargeState();
            yield return null;
            Capture(output, "charge", 1280, 720); Capture(output, "charge", 1920, 1080);
            PresentSwapState();
            yield return null;
            Capture(output, "swap", 1280, 720); Capture(output, "swap", 1920, 1080);
            File.WriteAllText(Path.Combine(output, "equipment-hud-visual-evidence.txt"),
                "PASS Unity " + Application.unityVersion + "\nReal AlfaUiController canvas rendered feasible charge and replacement states at 1280x720 and 1920x1080.\n" +
                "Shows private hands/three slots, procedural item pictograms, long electric-racket label, resources, stamina, slipper charge and replacement confirmation.\n" +
                "Synthetic UI state only; this does not validate a full equipment playthrough or GPU performance.\n" + receipt);
        }

        private void PresentChargeState()
        {
            ui.PresentHud(new BloodHudUiState(AlfaRole.Human, 93, 8, 18,
                interaction: "Soltá clic · Lanzar pantufla",
                contextHint: "1–3 · elegir objeto   ·   0 · manos   ·   rueda · cambiar espacio",
                equipment: new EquipmentHudUiState(new[]
                {
                    new EquipmentSlotUiState("PANTUFLA", "REUTILIZABLE", AlfaUiIconKind.Slipper),
                    new EquipmentSlotUiState("RAQUETA ELÉCTRICA", "3 CARGAS", AlfaUiIconKind.ElectricRacket),
                    new EquipmentSlotUiState("AEROSOL", "2,9 s", AlfaUiIconKind.Aerosol)
                }, 0, .42f, .78f)));
        }

        private void PresentSwapState()
        {
            ui.PresentHud(new BloodHudUiState(AlfaRole.Human, 93, 8, 18,
                interaction: "E · Confirmar reemplazo",
                contextHint: "1–3 · elegir objeto   ·   0 · manos   ·   rueda · cambiar espacio",
                equipment: new EquipmentHudUiState(new[]
                {
                    new EquipmentSlotUiState("PANTUFLA", "REUTILIZABLE", AlfaUiIconKind.Slipper),
                    new EquipmentSlotUiState("RAQUETA ELÉCTRICA", "3 CARGAS", AlfaUiIconKind.ElectricRacket),
                    new EquipmentSlotUiState("AEROSOL", "2,9 s", AlfaUiIconKind.Aerosol)
                }, 1, .42f, swapOfferText: "E · REEMPLAZAR\nRAQUETA ELÉCTRICA POR MATAMOSCAS")));
        }

        private void Capture(string output, string state, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; camera.targetTexture = target; camera.aspect = width / (float)height;
            try
            {
                Canvas.ForceUpdateCanvases();
                var panel = ui.transform.Find("GameplayHudView/PrivateEquipment") as RectTransform;
                Assert.That(panel, Is.Not.Null); Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                foreach (var label in panel.GetComponentsInChildren<Component>(true).Where(component => component.GetType().FullName == "TMPro.TextMeshProUGUI"))
                {
                    label.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) })?.Invoke(label, new object[] { false, false });
                }
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32();
                File.WriteAllBytes(Path.Combine(output, "equipment-hud-" + state + "-" + width + "x" + height + ".png"), pixels.EncodeToPNG());
                var canvas = ui.GetComponent<Canvas>(); var root = (RectTransform)ui.transform;
                receipt.AppendLine(state + " " + width + "x" + height + ": RT=" + target.width + "x" + target.height +
                    " canvasPixelRect=" + canvas.pixelRect + " renderingDisplaySize=" + canvas.renderingDisplaySize +
                    " scaleFactor=" + canvas.scaleFactor.ToString("0.###") + " rootRect=" + root.rect);
                foreach (var label in panel.GetComponentsInChildren<Component>(true).Where(component => component.GetType().FullName == "TMPro.TextMeshProUGUI"))
                {
                    bool overflowing = (bool)label.GetType().GetProperty("isTextOverflowing").GetValue(label);
                    Assert.That(overflowing, Is.False, label.name + " overflows at " + width + "x" + height);
                }
                Assert.That(colors.Count(color => color.r > 80 || color.g > 80 || color.b > 80), Is.GreaterThan(width * height / 250), "HUD did not produce enough visible pixels.");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target);
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (ui) Object.Destroy(ui.gameObject);
            if (camera) Object.Destroy(camera.gameObject);
            if (EventSystem.current) Object.Destroy(EventSystem.current.gameObject);
            yield return null;
        }
    }
}
