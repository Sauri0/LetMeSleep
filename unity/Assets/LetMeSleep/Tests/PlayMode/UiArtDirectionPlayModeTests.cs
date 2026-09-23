using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// v0.3 stage-1 art-direction contracts: create-tab room defaults reach the new room through the existing host
    /// rule actions, the connecting card replaces the inline status, "ready" undo is not a green check, selection
    /// carries no text marker and no label goes under the 21-unit floor (14 px at 1280x720).
    /// </summary>
    public sealed class UiArtDirectionPlayModeTests
    {
        private sealed class Actions : IMenuActions, IRoomMapActions, IRoomModeActions
        {
            public readonly List<string> Calls = new List<string>();
            public void CreateRoom(string playerName) => Calls.Add("create:" + playerName);
            public void JoinRoom(string playerName, string normalizedRoomCode) => Calls.Add("join:" + normalizedRoomCode);
            public void CancelOnline() => Calls.Add("cancel");
            public void CancelTraining() { }
            public void CopyRoomCode(string groupedRoomCode) { }
            public void SetReady(bool ready) => Calls.Add("ready:" + ready);
            public void SetHumanCount(int? humanCount) => Calls.Add("humans:" + (humanCount.HasValue ? humanCount.Value.ToString() : "auto"));
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
            public void SetRoomMap(string mapId) => Calls.Add("map:" + mapId);
            public void SetRoomMode(string modeId) => Calls.Add("mode:" + modeId);
            public void SetRoomDurationSeconds(int seconds) => Calls.Add("time:" + seconds);
        }

        private Actions actions;
        private AlfaUiController ui;

        [SetUp]
        public void SetUp()
        {
            actions = new Actions();
            ui = AlfaUiRuntime.Create(actions, new AlfaUiDependencies(persistentAcrossScenes: false));
            ui.SetRoomMaps(new[] { new TrainingMapOption("hf-isla-del-laguito-v2", "Isla del laguito"), new TrainingMapOption("hf-yate-a-la-deriva-v3", "Yate a la deriva") });
        }

        [TearDown]
        public void TearDown()
        {
            if (ui != null) Object.DestroyImmediate(ui.gameObject);
            if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
        }

        [UnityTest]
        public IEnumerator CreateTabDefaultsAreAppliedToTheNewRoomOneRuleAtATime()
        {
            ui.ShowCreateRoom("Branko");
            yield return null;
            Click("OnlineMapNext");          // CASA CON PATIO -> Isla del laguito
            Click("OnlineModeNext");         // SANGRE -> SUPERVIVENCIA
            Click("OnlineHumansNext");       // AUTO -> 1
            Click("OnlineHumansNext");       // 1 -> 2
            Assert.That(Label("OnlineMapName"), Is.EqualTo("ISLA DEL LAGUITO"));
            Click("OnlinePrimaryButton");
            Assert.That(actions.Calls, Is.EqualTo(new[] { "create:Branko" }));

            // The room opens with its defaults; each snapshot without pending rules releases the next choice.
            ui.PresentLobby(Lobby(RoomRules.AlfaMap, GameModes.Blood, null));
            Assert.That(actions.Calls.Last(), Is.EqualTo("map:hf-isla-del-laguito-v2"));
            ui.PresentLobby(Lobby("hf-isla-del-laguito-v2", GameModes.Blood, null, rulesPending: true));
            Assert.That(actions.Calls.Count, Is.EqualTo(2), "No rule is sent while the previous one is pending.");
            ui.PresentLobby(Lobby("hf-isla-del-laguito-v2", GameModes.Blood, null));
            Assert.That(actions.Calls.Last(), Is.EqualTo("mode:" + GameModes.Survival));
            ui.PresentLobby(Lobby("hf-isla-del-laguito-v2", GameModes.Survival, null));
            Assert.That(actions.Calls.Last(), Is.EqualTo("humans:2"));
            ui.PresentLobby(Lobby("hf-isla-del-laguito-v2", GameModes.Survival, 2));
            ui.PresentLobby(Lobby("hf-isla-del-laguito-v2", GameModes.Survival, 2));
            Assert.That(actions.Calls.Count, Is.EqualTo(4), "Every chosen rule is applied exactly once.");
        }

        [UnityTest]
        public IEnumerator JoiningNeverRewritesTheRoomRules()
        {
            ui.ShowCreateRoom("Branko");
            yield return null;
            Click("OnlineModeNext");
            ui.ShowJoinRoom("Branko");
            yield return null;
            var code = ui.GetComponentsInChildren<TMP_InputField>(true).First(item => item.name == "RoomCodeInput");
            code.text = "ABCDE-FGHIJ";
            Click("OnlinePrimaryButton");
            ui.PresentLobby(Lobby(RoomRules.AlfaMap, GameModes.Blood, null, owner: false));
            Assert.That(actions.Calls, Is.EqualTo(new[] { "join:ABCDEFGHIJ" }));
        }

        [UnityTest]
        public IEnumerator ConnectingCardReplacesTheInlineStatusWithoutRepeatingIt()
        {
            ui.ShowJoinRoom("Branko");
            ui.PresentOnline(new OnlineUiState(OnlineOperationPhase.Searching, canCancel: true));
            yield return null;
            Assert.That(Find("OnlineStatus").gameObject.activeInHierarchy, Is.False);
            Assert.That(Label("ConnectingTitle"), Is.EqualTo("CONECTANDO…"));
            Assert.That(Label("ConnectingMessage"), Is.EqualTo("Buscando la sala…"));
            var cancel = Find("OnlineCancelButton");
            Assert.That(cancel.IsChildOf(Find("OnlineConnectingCard")), Is.True, "CANCELAR lives inside the card.");
            ui.PresentOnline(new OnlineUiState());
            yield return null;
            Assert.That(Find("OnlineStatus").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator ReadyUndoIsNotAGreenCheckAndSelectionHasNoTextMarker()
        {
            ui.PresentLobby(Lobby(RoomRules.AlfaMap, GameModes.Blood, null, localReady: false));
            yield return null;
            Assert.That(Icon("LobbyReadyButton").Kind, Is.EqualTo(AlfaUiIconKind.Ready));
            ui.PresentLobby(Lobby(RoomRules.AlfaMap, GameModes.Blood, 2, localReady: true));
            yield return null;
            Assert.That(Label("LobbyReadyButton/Label"), Is.EqualTo("CANCELAR LISTO"));
            Assert.That(Icon("LobbyReadyButton").Kind, Is.EqualTo(AlfaUiIconKind.Close));
            Assert.That(ui.GetComponentsInChildren<TextMeshProUGUI>(true).Any(item => item.text.StartsWith(">")), Is.False);
        }

        [UnityTest]
        public IEnumerator NoLabelIsBuiltUnderTheLegibilityFloor()
        {
            ui.PresentLobby(Lobby(RoomRules.AlfaMap, GameModes.Blood, null));
            ui.ShowCreateRoom("Branko");
            yield return null;
            var small = ui.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(item => item.fontSize < 20.99f && !(item.enableAutoSizing && item.fontSizeMin >= 20.99f))
                .Select(item => item.name + "=" + item.fontSize).ToArray();
            Assert.That(small, Is.Empty);
        }

        private LobbyUiState Lobby(string mapId, string modeId, int? humans, bool rulesPending = false, bool owner = true, bool localReady = false) =>
            new LobbyUiState(owner, "ABCDE12345", new[] { new LobbyMemberUiState("m0", "Branko", localReady), new LobbyMemberUiState("m1", "Luna", false) },
                localReady, false, humans, false, "Todos deben marcar Listo para empezar.", mapId, "Casa con patio", rulesPending: rulesPending, modeId: modeId);

        private Transform Find(string path)
        {
            var parts = path.Split('/');
            var node = ui.GetComponentsInChildren<Transform>(true).First(item => item.name == parts[0]);
            for (var i = 1; i < parts.Length; i++) node = node.Find(parts[i]);
            return node;
        }

        private string Label(string path)
        {
            var node = Find(path);
            var text = node.GetComponent<TextMeshProUGUI>();
            return (text != null ? text : node.GetComponentInChildren<TextMeshProUGUI>(true)).text;
        }

        private AlfaUiIcon Icon(string buttonName) => Find(buttonName).Find("IconPlate/Icon").GetComponent<AlfaUiIcon>();

        private void Click(string buttonName) => Find(buttonName).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    }
}
