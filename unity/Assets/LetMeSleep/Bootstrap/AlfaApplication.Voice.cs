using System;
using LetMeSleep.Core;
using LetMeSleep.UI;
using UnityEngine.InputSystem;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        private VoiceRuntimeCoordinator voiceRuntime;
        private InputAction pushToTalkRebindAction;
        private InputActionRebindingExtensions.RebindingOperation pushToTalkRebind;

        private void StartVoiceRoom()
        {
            StopVoiceRoom();
            if (transport == null || lobby == null || ui == null) return;
            voiceRuntime = new VoiceRuntimeCoordinator(gameObject, transport, lobby, Mixer, ui);
            ApplyVoicePreferences();
        }

        private void SyncVoiceContext(RoomView view = null)
        {
            if (voiceRuntime == null) return;
            voiceRuntime.Sync(view ?? room?.Current, activeConfig, activeRoster, game, lobbyMovement, LocalId);
        }

        private void ApplyVoicePreferences()
        {
            voiceRuntime?.Configure(settings.VoiceDevice, settings.PushToTalkBinding, settings.VoiceVolume);
        }

        private void StopVoiceRoom()
        {
            CancelPushToTalkRebind();
            voiceRuntime?.Dispose(); voiceRuntime = null;
            PresentVoiceBinding();
        }

        public void SetLocalVoiceMuted(bool muted) => voiceRuntime?.SetLocalMuted(muted);
        public void SetPeerVoiceMuted(string memberId, bool muted) => voiceRuntime?.SetPeerMuted(memberId, muted);

        // Rebinding works with or without a room: it runs on a temporary action and persists only the result.
        // It does not re-present the settings screen, so unapplied draft changes survive.
        public void BeginPushToTalkRebind(Action<string, string> completed)
        {
            if (quiescing) return;
            CancelPushToTalkRebind();
            var action = new InputAction("PushToTalkRebind", InputActionType.Button, settings.PushToTalkBinding);
            pushToTalkRebindAction = action;
            pushToTalkRebind = action.PerformInteractiveRebinding(0)
                .WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Joystick>")
                .WithControlsExcluding("<Touchscreen>")
                .WithControlsExcluding("<XRController>")
                .WithControlsExcluding("<Mouse>/leftButton") // golpe / acción principal
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => { EndPushToTalkRebind(); PresentVoiceBinding(); })
                .OnComplete(_ =>
                {
                    string path = action.bindings[0].effectivePath, previous = settings.PushToTalkBinding;
                    EndPushToTalkRebind();
                    settings.PushToTalkBinding = path;
                    if (!SavePreferences()) settings.PushToTalkBinding = previous; // saveError explains it in Ajustes.
                    ApplyVoicePreferences(); PresentVoiceBinding();
                    completed?.Invoke(settings.PushToTalkBinding, PushToTalkBindings.Label(settings.PushToTalkBinding));
                });
            voiceRuntime?.SetRebinding(true);
            PresentVoiceBinding();
            pushToTalkRebind.Start();
        }

        private void CancelPushToTalkRebind()
        {
            var operation = pushToTalkRebind;
            if (operation != null && operation.started && !operation.completed && !operation.canceled) operation.Cancel();
            EndPushToTalkRebind();
        }

        private void EndPushToTalkRebind()
        {
            var operation = pushToTalkRebind; var action = pushToTalkRebindAction;
            pushToTalkRebind = null; pushToTalkRebindAction = null;
            operation?.Dispose(); action?.Dispose();
            voiceRuntime?.SetRebinding(false);
        }

        // Outside a room there is no voice runtime: publish the persisted binding so Ajustes never shows a stale 'V'.
        private void PresentVoiceBinding()
        {
            if (quiescing || voiceRuntime != null || !ui) return;
            bool waiting = pushToTalkRebind != null;
            ui.PresentVoice(new VoiceUiState(false, false, false, true, string.Empty,
                waiting ? PushToTalkBindings.WaitingLabel : PushToTalkBindings.Label(settings.PushToTalkBinding),
                waiting ? PushToTalkBindings.WaitingNotice : string.Empty, null));
        }

        private void OnApplicationFocus(bool focused) => voiceRuntime?.SetApplicationFocused(focused);
        private void OnApplicationPause(bool paused) => voiceRuntime?.SetApplicationPaused(paused);
    }
}
