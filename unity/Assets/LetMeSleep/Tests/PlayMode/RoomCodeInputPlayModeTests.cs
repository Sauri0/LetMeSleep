using System.Collections;
using System.Linq;
using System.Reflection;
using LetMeSleep.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// Regression for ui-presentation-audio-1: the room code field inserted the group separator ahead of the caret,
    /// so typing or pasting "ABCDE-FGHIJ" produced "ABCDE-GHIJF" and joined the wrong room.
    /// </summary>
    public sealed class RoomCodeInputPlayModeTests
    {
        private const string SharedCode = "ABCDE-FGHIJ";

        private sealed class Actions : IMenuActions
        {
            public string JoinedCode;
            public int JoinCount;
            public void CreateRoom(string playerName) { }
            public void JoinRoom(string playerName, string normalizedRoomCode) { JoinCount++; JoinedCode = normalizedRoomCode; }
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

        private Actions actions;
        private AlfaUiController ui;

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
        }

        [Test]
        public void EditingFormatterKeepsTheCaretAfterTheSeparator()
        {
            string text = string.Empty;
            int caret = 0;
            foreach (char c in "abcde-fghij")
            {
                text = text.Insert(caret, c.ToString());
                caret++;
                text = AlfaRoomCode.FormatForEditing(text, caret, out caret);
            }
            Assert.That(text, Is.EqualTo(SharedCode));
            Assert.That(caret, Is.EqualTo(SharedCode.Length));

            // Inserting in the middle keeps the caret right after the inserted character.
            var edited = AlfaRoomCode.FormatForEditing("ABCDEXFGH", 6, out var editedCaret);
            Assert.That(edited, Is.EqualTo("ABCDE-XFGH"));
            Assert.That(editedCaret, Is.EqualTo(7));
            // Backspacing the separator is harmless: the text regroups and the caret lands before it.
            var regrouped = AlfaRoomCode.FormatForEditing("ABCDEFG", 5, out var regroupedCaret);
            Assert.That(regrouped, Is.EqualTo("ABCDE-FG"));
            Assert.That(regroupedCaret, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator TypingTheSharedCodeKeepsItsOrderAndJoinsThatRoom()
        {
            ui.ShowJoinRoom("Branko");
            yield return null;
            var input = CodeInput();

            foreach (char c in "abcde-fghij")
                input.ProcessEvent(Event.KeyboardEvent(c == '-' ? "-" : c.ToString()));
            Assert.That(input.text, Is.EqualTo(SharedCode), "Typed code must keep its order.");
            Assert.That(input.stringPosition, Is.EqualTo(SharedCode.Length), "Caret stays at the end while typing.");

            Button("OnlinePrimaryButton").onClick.Invoke();
            Assert.That(actions.JoinCount, Is.EqualTo(1));
            Assert.That(actions.JoinedCode, Is.EqualTo("ABCDEFGHIJ"));
        }

        [UnityTest]
        public IEnumerator PastingTheSharedCodeKeepsItsOrder()
        {
            ui.ShowJoinRoom("Branko");
            yield return null;
            var input = CodeInput();

            // Ctrl+V in TMP_InputField is Append(clipboard): one Append(char) per character, as exercised here.
            var append = typeof(TMP_InputField).GetMethod("Append", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            Assert.That(append, Is.Not.Null);
            append.Invoke(input, new object[] { SharedCode });
            Assert.That(input.text, Is.EqualTo(SharedCode), "Pasted code must keep its order.");

            // The real Ctrl+V shortcut too, when the OS clipboard is reachable from this process.
            var previousClipboard = GUIUtility.systemCopyBuffer;
            try
            {
                GUIUtility.systemCopyBuffer = "vwxyz-12345";
                if (GUIUtility.systemCopyBuffer == "vwxyz-12345")
                {
                    input.SetTextWithoutNotify(string.Empty);
                    input.stringPosition = 0;
                    input.ProcessEvent(Event.KeyboardEvent("^v"));
                    Assert.That(input.text, Is.EqualTo("VWXYZ-12345"), "Ctrl+V must keep the pasted order.");
                }
            }
            finally
            {
                GUIUtility.systemCopyBuffer = previousClipboard;
            }

            input.SetTextWithoutNotify(string.Empty);
            input.stringPosition = 0;
            append.Invoke(input, new object[] { " abcde fghij " });
            Assert.That(input.text, Is.EqualTo(SharedCode), "Spaces from chat apps are dropped and the code regroups.");
            Button("OnlinePrimaryButton").onClick.Invoke();
            Assert.That(actions.JoinedCode, Is.EqualTo("ABCDEFGHIJ"));
        }

        private TMP_InputField CodeInput()
        {
            var input = ui.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault(item => item.name == "RoomCodeInput");
            Assert.That(input, Is.Not.Null);
            Assert.That(input.gameObject.activeInHierarchy, Is.True, "The join tab shows the room code field.");
            return input;
        }

        private UnityEngine.UI.Button Button(string name)
        {
            var button = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(item => item.name == name);
            Assert.That(button, Is.Not.Null, name);
            return button;
        }
    }
}
